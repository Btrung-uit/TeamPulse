using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Domain.Activities;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class ResourceEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public ResourceEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ResourceHub_ValidatesMetadataPermissionsFiltersAndTenantBoundary()
    {
        using var leader = await RegisterAsync("Resource Leader");
        using var member = await RegisterAsync("Resource Member");
        using var advisor = await RegisterAsync("Resource Advisor");
        using var outsider = await RegisterAsync("Resource Outsider");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Resource Workspace");
        await AddMemberAsync(leader.Client, workspaceId, member.Email, "Member");
        await AddMemberAsync(leader.Client, workspaceId, advisor.Email, "Advisor");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Resource Project");
        var taskId = await CreateTaskAsync(leader.Client, workspaceId, projectId, member.UserId);
        var resourcesUrl = ResourcesUrl(workspaceId, projectId);

        var invalidUrl = await leader.Client.PostAsJsonAsync(
            resourcesUrl,
            SaveResource("Unsafe link", "GoogleDocs", "javascript:alert(1)", member.UserId));
        await AssertErrorAsync(invalidUrl, HttpStatusCode.BadRequest, "validation_failed");

        var invalidOwner = await leader.Client.PostAsJsonAsync(
            resourcesUrl,
            SaveResource(
                "Wrong owner",
                "GitHub",
                "https://github.com/example/repo",
                outsider.UserId,
                "example/repo"));
        await AssertErrorAsync(invalidOwner, HttpStatusCode.BadRequest, "owner_not_found");

        var missingRepositoryMapping = await leader.Client.PostAsJsonAsync(
            resourcesUrl,
            SaveResource(
                "Unmapped repository",
                "GitHub",
                "https://github.com/example/repo",
                member.UserId));
        await AssertErrorAsync(
            missingRepositoryMapping,
            HttpStatusCode.BadRequest,
            "validation_failed");

        var memberCreate = await member.Client.PostAsJsonAsync(
            resourcesUrl,
            SaveResource("Member write", "Demo", "https://example.com/demo", member.UserId));
        Assert.Equal(HttpStatusCode.Forbidden, memberCreate.StatusCode);

        var create = await leader.Client.PostAsJsonAsync(
            resourcesUrl,
            SaveResource(
                "Architecture notes",
                "GoogleDocs",
                "https://docs.google.com/document/d/doc-123",
                member.UserId,
                "doc-123",
                taskId));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var resource = await create.Content.ReadFromJsonAsync<ResourceResponse>();
        Assert.NotNull(resource);
        Assert.Equal("GoogleDocs", resource.Type);
        Assert.Equal(member.UserId, resource.Owner.UserId);
        Assert.Equal(taskId, resource.LinkedTask?.Id);

        var advisorResources = await advisor.Client.GetAsync(resourcesUrl);
        var advisorDashboard = await advisor.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/projects/{projectId}/dashboard");
        var advisorMutation = await advisor.Client.PostAsJsonAsync(
            resourcesUrl,
            SaveResource("Advisor write", "Demo", "https://example.com/demo", member.UserId));
        Assert.Equal(HttpStatusCode.OK, advisorResources.StatusCode);
        Assert.Equal(HttpStatusCode.OK, advisorDashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, advisorMutation.StatusCode);

        var memberList = await member.Client.GetFromJsonAsync<List<ResourceResponse>>(
            $"{resourcesUrl}?search=architecture&type=GoogleDocs");
        Assert.NotNull(memberList);
        Assert.Equal(resource.Id, Assert.Single(memberList).Id);

        var filteredOut = await member.Client.GetFromJsonAsync<List<ResourceResponse>>(
            $"{resourcesUrl}?type=Canva");
        Assert.NotNull(filteredOut);
        Assert.Empty(filteredOut);

        var update = await leader.Client.PutAsJsonAsync(
            $"{resourcesUrl}/{resource.Id}",
            SaveResource(
                "Architecture decision record",
                "GoogleDrive",
                "https://drive.google.com/file/d/file-456",
                leader.UserId,
                "file-456"));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<ResourceResponse>();
        Assert.NotNull(updated);
        Assert.Null(updated.LinkedTask);
        Assert.Equal("GoogleDrive", updated.Type);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
            Assert.Empty(await dbContext.TaskEvidence.IgnoreQueryFilters().ToListAsync());
            var actions = await dbContext.ActivityLogs
                .IgnoreQueryFilters()
                .Where(activity => activity.EntityId == resource.Id)
                .Select(activity => activity.Action)
                .ToListAsync();
            Assert.Contains(ActivityAction.ResourceCreated, actions);
            Assert.Contains(ActivityAction.ResourceUpdated, actions);
        }

        var outsiderWorkspaceId = await CreateWorkspaceAsync(
            outsider.Client, "Outsider Resource Workspace");
        var outsiderProjectId = await CreateProjectAsync(
            outsider.Client, outsiderWorkspaceId, "Outsider Resource Project");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await member.Client.GetAsync(
                ResourcesUrl(outsiderWorkspaceId, outsiderProjectId))).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await member.Client.GetAsync(
                ResourcesUrl(workspaceId, outsiderProjectId))).StatusCode);

        var delete = await leader.Client.DeleteAsync($"{resourcesUrl}/{resource.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty((await leader.Client.GetFromJsonAsync<List<ResourceResponse>>(resourcesUrl))!);
    }

    private async Task<RegisteredUser> RegisterAsync(string displayName)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        var email = $"{displayName.Replace(" ", string.Empty).ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
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
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
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

    private static async Task<Guid> CreateProjectAsync(
        HttpClient client,
        Guid workspaceId,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static async Task<Guid> CreateTaskAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        Guid assigneeUserId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{projectId}/tasks",
            new
            {
                code = "TP-701",
                title = "Linked resource task",
                description = "Resource test task",
                assigneeUserId,
                priority = "Medium",
                rowVersion = (string?)null,
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static object SaveResource(
        string title,
        string type,
        string url,
        Guid ownerUserId,
        string? externalId = null,
        Guid? linkedTaskId = null) =>
        new { title, type, url, externalId, ownerUserId, linkedTaskId };

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<ApiErrorResponse>())?.Code);
    }

    private static string ResourcesUrl(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}/resources";

    private sealed record RegisteredUser(Guid UserId, string Email, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);
    private sealed record AuthUserResponse(Guid Id);
    private sealed record IdResponse(Guid Id);
    private sealed record ResourceOwnerResponse(Guid UserId, string DisplayName, string Email);
    private sealed record ResourceTaskResponse(Guid Id, string Code, string Title);
    private sealed record ResourceResponse(
        Guid Id,
        string Type,
        ResourceOwnerResponse Owner,
        ResourceTaskResponse? LinkedTask);
    private sealed record ApiErrorResponse(string Code, string Message);
}
