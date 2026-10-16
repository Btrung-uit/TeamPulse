using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Domain.Activities;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class ProjectEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public ProjectEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProjectFlow_EnforcesWorkspaceAndRoleBoundaries_WithActivityHistory()
    {
        var leader = await RegisterAsync("Project Leader");
        var member = await RegisterAsync("Project Member");
        var advisor = await RegisterAsync("Project Advisor");
        var outsider = await RegisterAsync("Project Outsider");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Delivery Workspace");
        var outsiderWorkspaceId = await CreateWorkspaceAsync(
            outsider.Client,
            "Private Workspace");

        await AddMemberAsync(leader.Client, workspaceId, member.Email, "Member");
        await AddMemberAsync(leader.Client, workspaceId, advisor.Email, "Advisor");

        var createResponse = await leader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name = "TeamPulse MVP", description = "Core collaboration flow" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(created);
        Assert.Equal(workspaceId, created.WorkspaceId);

        var memberProjects = await member.Client.GetFromJsonAsync<List<ProjectResponse>>(
            $"/api/workspaces/{workspaceId}/projects");
        Assert.Contains(memberProjects!, project => project.Id == created.Id);

        var advisorRead = await advisor.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, advisorRead.StatusCode);

        var memberCreate = await member.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name = "Forbidden project" });
        Assert.Equal(HttpStatusCode.Forbidden, memberCreate.StatusCode);

        var advisorArchive = await advisor.Client.PostAsync(
            $"/api/workspaces/{workspaceId}/projects/{created.Id}/archive",
            content: null);
        Assert.Equal(HttpStatusCode.Forbidden, advisorArchive.StatusCode);

        var outsiderRead = await outsider.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects");
        Assert.Equal(HttpStatusCode.Forbidden, outsiderRead.StatusCode);

        var crossWorkspaceProject = await leader.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, crossWorkspaceProject.StatusCode);

        var outsiderProject = await CreateProjectAsync(
            outsider.Client,
            outsiderWorkspaceId,
            "Outsider project");
        var crossTenantRead = await leader.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects/{outsiderProject.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantRead.StatusCode);

        var updateResponse = await leader.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{created.Id}",
            new { name = "TeamPulse Portfolio", description = "Updated scope" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var archiveResponse = await leader.Client.PostAsync(
            $"/api/workspaces/{workspaceId}/projects/{created.Id}/archive",
            content: null);
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

        var activeProjects = await leader.Client.GetFromJsonAsync<List<ProjectResponse>>(
            $"/api/workspaces/{workspaceId}/projects");
        Assert.DoesNotContain(activeProjects!, project => project.Id == created.Id);

        var allProjects = await leader.Client.GetFromJsonAsync<List<ProjectResponse>>(
            $"/api/workspaces/{workspaceId}/projects?includeArchived=true");
        Assert.Contains(allProjects!, project => project.Id == created.Id && project.IsArchived);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var projectActivities = await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .Where(activity =>
                activity.WorkspaceId == workspaceId &&
                activity.EntityType == "Project" &&
                activity.EntityId == created.Id)
            .OrderBy(activity => activity.OccurredAtUtc)
            .ToListAsync();

        Assert.Equal(
            [
                ActivityAction.ProjectCreated,
                ActivityAction.ProjectUpdated,
                ActivityAction.ProjectArchived,
            ],
            projectActivities.Select(activity => activity.Action).ToArray());
    }

    [Fact]
    public async Task ArchivedProject_CannotBeUpdated_AndArchiveIsIdempotent()
    {
        var leader = await RegisterAsync("Archive Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Archive Workspace");
        var project = await CreateProjectAsync(leader.Client, workspaceId, "Archive me");

        var firstArchive = await leader.Client.PostAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}/archive",
            content: null);
        var secondArchive = await leader.Client.PostAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}/archive",
            content: null);
        var update = await leader.Client.PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{project.Id}",
            new { name = "Too late" });

        Assert.Equal(HttpStatusCode.OK, firstArchive.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondArchive.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var archiveCount = await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .CountAsync(activity =>
                activity.EntityId == project.Id &&
                activity.Action == ActivityAction.ProjectArchived);
        Assert.Equal(1, archiveCount);
    }

    private async Task<RegisteredUser> RegisterAsync(string displayName)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
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

    private sealed record RegisteredUser(Guid UserId, string Email, HttpClient Client);

    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);

    private sealed record AuthUserResponse(Guid Id);

    private sealed record WorkspaceResponse(Guid Id);

    private sealed record ProjectResponse(
        Guid Id,
        Guid WorkspaceId,
        string Name,
        string? Description,
        bool IsArchived);
}
