using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class TenantIdentityBoundaryTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public TenantIdentityBoundaryTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantEndpoints_RejectAnonymousAndMalformedBearerTokens()
    {
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        using var anonymousClient = CreateClient();

        var listWorkspaces = await anonymousClient.GetAsync("/api/workspaces");
        var createWorkspace = await anonymousClient.PostAsJsonAsync(
            "/api/workspaces",
            new { name = "Unauthorized workspace" });
        var listMembers = await anonymousClient.GetAsync(
            $"/api/workspaces/{workspaceId}/members");
        var listProjects = await anonymousClient.GetAsync(
            $"/api/workspaces/{workspaceId}/projects");
        var createProject = await anonymousClient.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name = "Unauthorized project" });

        Assert.All(
            new[]
            {
                listWorkspaces,
                createWorkspace,
                listMembers,
                listProjects,
                createProject,
            },
            response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));

        using var malformedTokenClient = CreateClient();
        malformedTokenClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var projectDetails = await malformedTokenClient.GetAsync(
            $"/api/workspaces/{workspaceId}/projects/{projectId}");
        Assert.Equal(HttpStatusCode.Unauthorized, projectDetails.StatusCode);
    }

    [Fact]
    public async Task TwoWorkspaceScenario_IsolatesListsDetailsAndEveryProjectMutation()
    {
        using var leaderA = await RegisterAsync("Tenant A Leader");
        using var leaderB = await RegisterAsync("Tenant B Leader");
        var workspaceA = await CreateWorkspaceAsync(leaderA.Client, "Workspace A");
        var workspaceB = await CreateWorkspaceAsync(leaderB.Client, "Workspace B");
        var projectA = await CreateProjectAsync(
            leaderA.Client,
            workspaceA,
            "Project A");
        var projectB = await CreateProjectAsync(
            leaderB.Client,
            workspaceB,
            "Project B");

        var projectsVisibleToA = await leaderA.Client.GetFromJsonAsync<List<ProjectResponse>>(
            $"/api/workspaces/{workspaceA}/projects?includeArchived=true");

        Assert.NotNull(projectsVisibleToA);
        var visibleProject = Assert.Single(projectsVisibleToA);
        Assert.Equal(projectA.Id, visibleProject.Id);
        Assert.DoesNotContain(projectsVisibleToA, project => project.Id == projectB.Id);

        var crossWorkspaceDetails = await leaderA.Client.GetAsync(
            $"/api/workspaces/{workspaceB}");
        var crossWorkspaceMembers = await leaderA.Client.GetAsync(
            $"/api/workspaces/{workspaceB}/members");
        var crossWorkspaceList = await leaderA.Client.GetAsync(
            $"/api/workspaces/{workspaceB}/projects");
        var crossWorkspaceProject = await leaderA.Client.GetAsync(
            $"/api/workspaces/{workspaceB}/projects/{projectB.Id}");
        var crossWorkspaceCreate = await leaderA.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceB}/projects",
            new { name = "Injected by tenant A" });
        var crossWorkspaceUpdate = await leaderA.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspaceB}/projects/{projectB.Id}",
            new { name = "Changed by tenant A" });
        var crossWorkspaceArchive = await leaderA.Client.PostAsync(
            $"/api/workspaces/{workspaceB}/projects/{projectB.Id}/archive",
            content: null);
        var crossWorkspaceMemberMutation = await leaderA.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceB}/members",
            new { leaderA.Email, role = "Leader" });

        Assert.All(
            new[]
            {
                crossWorkspaceDetails,
                crossWorkspaceMembers,
                crossWorkspaceList,
                crossWorkspaceProject,
                crossWorkspaceCreate,
                crossWorkspaceUpdate,
                crossWorkspaceArchive,
                crossWorkspaceMemberMutation,
            },
            response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));

        var disguisedCrossTenantRead = await leaderA.Client.GetAsync(
            $"/api/workspaces/{workspaceA}/projects/{projectB.Id}");
        var disguisedCrossTenantUpdate = await leaderA.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspaceA}/projects/{projectB.Id}",
            new { name = "Cross-tenant overwrite" });
        var disguisedCrossTenantArchive = await leaderA.Client.PostAsync(
            $"/api/workspaces/{workspaceA}/projects/{projectB.Id}/archive",
            content: null);

        Assert.All(
            new[]
            {
                disguisedCrossTenantRead,
                disguisedCrossTenantUpdate,
                disguisedCrossTenantArchive,
            },
            response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));

        var unchangedProjectB = await leaderB.Client.GetFromJsonAsync<ProjectResponse>(
            $"/api/workspaces/{workspaceB}/projects/{projectB.Id}");
        Assert.NotNull(unchangedProjectB);
        Assert.Equal("Project B", unchangedProjectB.Name);
        Assert.False(unchangedProjectB.IsArchived);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Advisor")]
    public async Task ReadOnlyRoles_CanReadButCannotMutateProjectsOrMemberships(string role)
    {
        using var leader = await RegisterAsync($"{role} Boundary Leader");
        using var restrictedUser = await RegisterAsync($"{role} Boundary User");
        var workspaceId = await CreateWorkspaceAsync(
            leader.Client,
            $"{role} Boundary Workspace");
        await AddMemberAsync(leader.Client, workspaceId, restrictedUser.Email, role);
        var project = await CreateProjectAsync(
            leader.Client,
            workspaceId,
            $"{role} Visible Project");

        var list = await restrictedUser.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects");
        var details = await restrictedUser.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);

        var create = await restrictedUser.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name = "Forbidden create" });
        var update = await restrictedUser.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}",
            new { name = "Forbidden update" });
        var archive = await restrictedUser.Client.PostAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}/archive",
            content: null);
        var addMember = await restrictedUser.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members",
            new { leader.Email, role = "Leader" });
        var selfEscalation = await restrictedUser.Client.PatchAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members/{restrictedUser.UserId}/role",
            new { role = "Leader" });

        Assert.All(
            new[] { create, update, archive, addMember, selfEscalation },
            response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));

        var unchangedProject = await leader.Client.GetFromJsonAsync<ProjectResponse>(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}");
        Assert.NotNull(unchangedProject);
        Assert.Equal($"{role} Visible Project", unchangedProject.Name);
        Assert.False(unchangedProject.IsArchived);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var projectActivityCount = await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .CountAsync(activity => activity.EntityId == project.Id);
        Assert.Equal(1, projectActivityCount);
    }

    [Fact]
    public async Task ExistingToken_UsesCurrentDatabaseRoleAndActiveMembership()
    {
        using var originalLeader = await RegisterAsync("Original Leader");
        using var secondLeader = await RegisterAsync("Second Leader");
        var workspaceId = await CreateWorkspaceAsync(
            originalLeader.Client,
            "Dynamic Authorization Workspace");
        var originalProject = await CreateProjectAsync(
            originalLeader.Client,
            workspaceId,
            "Original Project");

        await AddMemberAsync(
            originalLeader.Client,
            workspaceId,
            secondLeader.Email,
            "Leader");

        var createWithPreMembershipToken = await secondLeader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name = "Created after membership" });
        Assert.Equal(HttpStatusCode.Created, createWithPreMembershipToken.StatusCode);

        var demoteOriginalLeader = await secondLeader.Client.PatchAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members/{originalLeader.UserId}/role",
            new { role = "Member" });
        Assert.Equal(HttpStatusCode.OK, demoteOriginalLeader.StatusCode);

        var readWithExistingToken = await originalLeader.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects/{originalProject.Id}");
        var mutateWithExistingToken = await originalLeader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name = "Should be forbidden after demotion" });
        Assert.Equal(HttpStatusCode.OK, readWithExistingToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mutateWithExistingToken.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
            var membership = await dbContext.WorkspaceMembers
                .IgnoreQueryFilters()
                .SingleAsync(candidate =>
                    candidate.WorkspaceId == workspaceId &&
                    candidate.UserId == originalLeader.UserId);
            membership.Deactivate();
            await dbContext.SaveChangesAsync();
        }

        var readAfterDeactivation = await originalLeader.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects");
        var identityAfterDeactivation = await originalLeader.Client.GetAsync("/api/auth/me");
        var visibleWorkspaces = await originalLeader.Client
            .GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");

        Assert.Equal(HttpStatusCode.Forbidden, readAfterDeactivation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, identityAfterDeactivation.StatusCode);
        Assert.DoesNotContain(visibleWorkspaces!, workspace => workspace.Id == workspaceId);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }

    private async Task<RegisteredUser> RegisterAsync(string displayName)
    {
        var client = CreateClient();
        var email =
            $"{displayName.Replace(" ", string.Empty).ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName,
            password = "StrongPass123",
        });
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new RegisteredUser(session.User.Id, email, client);
    }

    private static async Task<Guid> CreateWorkspaceAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/workspaces", new { name });
        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        Assert.NotNull(workspace);
        return workspace.Id;
    }

    private static async Task AddMemberAsync(
        HttpClient client,
        Guid workspaceId,
        string email,
        string role)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members",
            new { email, role });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProjectResponse> CreateProjectAsync(
        HttpClient client,
        Guid workspaceId,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(project);
        return project;
    }

    private sealed record RegisteredUser(Guid UserId, string Email, HttpClient Client)
        : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
        }
    }

    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);

    private sealed record AuthUserResponse(Guid Id);

    private sealed record WorkspaceResponse(Guid Id);

    private sealed record ProjectResponse(
        Guid Id,
        Guid WorkspaceId,
        string Name,
        bool IsArchived);
}
