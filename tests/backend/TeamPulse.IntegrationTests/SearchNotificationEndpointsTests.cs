using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TeamPulse.IntegrationTests;

public sealed class SearchNotificationEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public SearchNotificationEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_StaysInsideWorkspace_AndFindsProjectsTasksAndMembers()
    {
        var leader = await RegisterAsync("Search Leader");
        var member = await RegisterAsync("Searchable Member");
        var outsider = await RegisterAsync("Outside User");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Search Workspace");
        await AddMemberAsync(leader.Client, workspaceId, member.Email);
        var projectId = await CreateProjectAsync(
            leader.Client,
            workspaceId,
            "Searchable Project");
        await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            "TP-501",
            "Searchable task",
            member.UserId);

        var results = await leader.Client.GetFromJsonAsync<SearchResponse>(
            $"/api/workspaces/{workspaceId}/search?query=Searchable");

        Assert.NotNull(results);
        Assert.Equal("Searchable Project", Assert.Single(results.Projects).Name);
        Assert.Equal("TP-501", Assert.Single(results.Tasks).Code);
        Assert.Equal(member.UserId, Assert.Single(results.Members).UserId);

        var forbidden = await outsider.Client.GetAsync(
            $"/api/workspaces/{workspaceId}/search?query=Searchable");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AssignmentNotification_IsRecipientSpecific_AndCanBeMarkedRead()
    {
        var leader = await RegisterAsync("Notification Leader");
        var assignee = await RegisterAsync("Notification Assignee");
        var otherMember = await RegisterAsync("Notification Observer");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Notification Workspace");
        await AddMemberAsync(leader.Client, workspaceId, assignee.Email);
        await AddMemberAsync(leader.Client, workspaceId, otherMember.Email);
        var projectId = await CreateProjectAsync(
            leader.Client,
            workspaceId,
            "Notification Project");
        await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            "TP-601",
            "Recipient-only task",
            assignee.UserId);

        var feed = await assignee.Client.GetFromJsonAsync<NotificationFeedResponse>(
            $"/api/workspaces/{workspaceId}/notifications?filter=all");
        Assert.NotNull(feed);
        Assert.Equal(1, feed.UnreadCount);
        var notification = Assert.Single(feed.Items);
        Assert.Equal("Assignment", notification.Kind);
        Assert.Equal(projectId, notification.ProjectId);
        Assert.False(notification.IsRead);

        var observerFeed = await otherMember.Client.GetFromJsonAsync<NotificationFeedResponse>(
            $"/api/workspaces/{workspaceId}/notifications?filter=all");
        Assert.NotNull(observerFeed);
        Assert.Empty(observerFeed.Items);
        Assert.Equal(0, observerFeed.UnreadCount);

        var markRead = await assignee.Client.PatchAsync(
            $"/api/workspaces/{workspaceId}/notifications/{notification.Id}/read",
            null);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);

        var unreadFeed = await assignee.Client.GetFromJsonAsync<NotificationFeedResponse>(
            $"/api/workspaces/{workspaceId}/notifications?filter=unread");
        Assert.NotNull(unreadFeed);
        Assert.Empty(unreadFeed.Items);
        Assert.Equal(0, unreadFeed.UnreadCount);
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
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotNull(result);
        return result.Id;
    }

    private static async Task AddMemberAsync(
        HttpClient client,
        Guid workspaceId,
        string email)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members",
            new { email, role = "Member" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotNull(result);
        return result.Id;
    }

    private static async Task CreateTaskAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        string code,
        string title,
        Guid assigneeUserId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{projectId}/tasks",
            new
            {
                code,
                title,
                assigneeUserId,
                priority = "Medium",
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed record RegisteredUser(Guid UserId, string Email, HttpClient Client);
    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);
    private sealed record AuthUserResponse(Guid Id);
    private sealed record IdResponse(Guid Id);
    private sealed record SearchResponse(
        IReadOnlyList<ProjectSearchResponse> Projects,
        IReadOnlyList<TaskSearchResponse> Tasks,
        IReadOnlyList<MemberSearchResponse> Members);
    private sealed record ProjectSearchResponse(Guid Id, string Name);
    private sealed record TaskSearchResponse(Guid Id, string Code);
    private sealed record MemberSearchResponse(Guid UserId);
    private sealed record NotificationFeedResponse(
        IReadOnlyList<NotificationResponse> Items,
        int UnreadCount);
    private sealed record NotificationResponse(
        Guid Id,
        string Kind,
        Guid ProjectId,
        bool IsRead);
}
