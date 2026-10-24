using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Integrations;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class GitHubTaskAutomationTests : IClassFixture<TeamPulseApiFactory>
{
    private const string WebhookPath = "/api/integrations/github/webhook";
    private const string WebhookSecret = "TeamPulse_GitHub_automation_test_secret_2026";
    private const string RepositoryFullName = "Btrung-uit/TeamPulse";
    private readonly TeamPulseApiFactory _factory;

    public GitHubTaskAutomationTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PushCommit_AddsEvidenceWithoutCompletingTask_AndIsIdempotent()
    {
        await using var factory = CreateEnabledFactory();
        using var leader = await RegisterAsync(factory, "Push Leader");
        var setup = await CreateProjectSetupAsync(leader, "TP-601");
        var task = await ChangeStatusAsync(
            leader.Client,
            setup,
            setup.Task,
            "InProgress");
        var commitSha = "0123456789abcdef0123456789abcdef01234567";
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            repository = Repository(),
            commits = new object[]
            {
                new { id = commitSha, message = "TP-601 implement signed webhook ingestion" },
                new { id = "abcdefabcdefabcdefabcdefabcdefabcdefabcd", message = "TP-ABC malformed reference" },
            },
        });

        await SendWebhookAsync(leader.Client, "push-delivery-1", "push", payload);
        await WaitForDeliveryAsync(factory, "push-delivery-1");

        var storedTask = await FindTaskAsync(factory, task.Id);
        Assert.Equal(ProjectTaskStatus.EvidenceLinked, storedTask.Status);
        var evidence = await ListAutomationEvidenceAsync(factory, task.Id);
        var commitEvidence = Assert.Single(evidence);
        Assert.Equal(TaskEvidenceType.Commit, commitEvidence.Type);
        Assert.Equal(TaskEvidenceSource.GitHubAutomation, commitEvidence.Source);
        Assert.Null(commitEvidence.SubmittedByUserId);
        Assert.Equal($"commit:{commitSha}", commitEvidence.ExternalId);

        await SendWebhookAsync(leader.Client, "push-delivery-2", "push", payload);
        await WaitForDeliveryAsync(factory, "push-delivery-2");

        Assert.Single(await ListAutomationEvidenceAsync(factory, task.Id));
        var activities = await ListTaskActivitiesAsync(factory, task.Id);
        Assert.Contains(activities, activity =>
            activity.Source == ActivitySource.GitHubAutomation &&
            activity.SourceReference == "push-delivery-1" &&
            activity.Action == ActivityAction.TaskEvidenceAdded);
        Assert.DoesNotContain(activities, activity => activity.NewValue == "Done");
    }

    [Fact]
    public async Task PullRequestEvents_MoveTaskThroughReviewToDone()
    {
        await using var factory = CreateEnabledFactory();
        using var leader = await RegisterAsync(factory, "PR Lifecycle Leader");
        var setup = await CreateProjectSetupAsync(leader, "TP-602");

        await SendWebhookAsync(
            leader.Client,
            "pr-ready-delivery",
            "pull_request",
            PullRequestPayload("ready_for_review", "TP-602 add automation", merged: false, draft: false));
        await WaitForDeliveryAsync(factory, "pr-ready-delivery");
        Assert.Equal(
            ProjectTaskStatus.InReview,
            (await FindTaskAsync(factory, setup.Task.Id)).Status);

        await SendWebhookAsync(
            leader.Client,
            "pr-changes-delivery",
            "pull_request_review",
            PullRequestReviewPayload("TP-602 add automation", "changes_requested"));
        await WaitForDeliveryAsync(factory, "pr-changes-delivery");
        Assert.Equal(
            ProjectTaskStatus.NeedsChanges,
            (await FindTaskAsync(factory, setup.Task.Id)).Status);

        await SendWebhookAsync(
            leader.Client,
            "pr-ready-again-delivery",
            "pull_request",
            PullRequestPayload("ready_for_review", "TP-602 add automation", merged: false, draft: false));
        await WaitForDeliveryAsync(factory, "pr-ready-again-delivery");

        await SendWebhookAsync(
            leader.Client,
            "pr-merged-delivery",
            "pull_request",
            PullRequestPayload("closed", "TP-602 add automation", merged: true, draft: false));
        await WaitForDeliveryAsync(factory, "pr-merged-delivery");

        Assert.Equal(
            ProjectTaskStatus.Done,
            (await FindTaskAsync(factory, setup.Task.Id)).Status);
        var evidence = await ListAutomationEvidenceAsync(factory, setup.Task.Id);
        var pullRequestEvidence = Assert.Single(evidence);
        Assert.Equal(TaskEvidenceType.PullRequest, pullRequestEvidence.Type);
        Assert.Equal("pull_request:42", pullRequestEvidence.ExternalId);
        var activities = await ListTaskActivitiesAsync(factory, setup.Task.Id);
        Assert.Contains(activities, activity =>
            activity.SourceReference == "pr-merged-delivery" &&
            activity.PreviousValue == "InReview" &&
            activity.NewValue == "Done");
    }

    [Fact]
    public async Task ClosedUnmerged_ReturnsToInProgress_AndLeaderOverrideRemainsUserActivity()
    {
        await using var factory = CreateEnabledFactory();
        using var leader = await RegisterAsync(factory, "Override Leader");
        var setup = await CreateProjectSetupAsync(leader, "TP-603");

        await SendWebhookAsync(
            leader.Client,
            "override-ready-delivery",
            "pull_request",
            PullRequestPayload("ready_for_review", "TP-603 review flow", merged: false, draft: false));
        await WaitForDeliveryAsync(factory, "override-ready-delivery");
        await SendWebhookAsync(
            leader.Client,
            "override-closed-delivery",
            "pull_request",
            PullRequestPayload("closed", "TP-603 review flow", merged: false, draft: false));
        await WaitForDeliveryAsync(factory, "override-closed-delivery");

        var task = await GetTaskAsync(leader.Client, setup, setup.Task.Id);
        Assert.Equal("InProgress", task.Status);
        task = await ChangeStatusAsync(leader.Client, setup, task, "EvidenceLinked");
        task = await ChangeStatusAsync(leader.Client, setup, task, "InReview");
        await ChangeStatusAsync(leader.Client, setup, task, "NeedsChanges");

        var activities = await ListTaskActivitiesAsync(factory, setup.Task.Id);
        Assert.Contains(activities, activity =>
            activity.Source == ActivitySource.GitHubAutomation &&
            activity.SourceReference == "override-closed-delivery" &&
            activity.NewValue == "InProgress");
        Assert.Contains(activities, activity =>
            activity.Source == ActivitySource.User &&
            activity.ActorUserId == leader.UserId &&
            activity.PreviousValue == "InReview" &&
            activity.NewValue == "NeedsChanges");
    }

    [Fact]
    public async Task WaitingRoom_ProjectsChecksAndCreatesGitHubDeepLinkNotifications()
    {
        await using var factory = CreateEnabledFactory();
        using var leader = await RegisterAsync(factory, "Waiting Room Leader");
        var setup = await CreateProjectSetupAsync(leader, "TP-604");

        await SendWebhookAsync(
            leader.Client,
            "waiting-room-ready",
            "pull_request",
            PullRequestPayload("ready_for_review", "TP-604 waiting room", merged: false, draft: false));
        await WaitForDeliveryAsync(factory, "waiting-room-ready");

        var waiting = await leader.Client.GetFromJsonAsync<PullRequestFeedResponse>(
            PullRequestsUrl(setup) + "?filter=waiting");
        Assert.NotNull(waiting);
        var item = Assert.Single(waiting.Items);
        Assert.Equal("WaitingForReview", item.Status);
        Assert.Equal("Unknown", item.CheckStatus);
        Assert.Equal("octocat", item.AuthorLogin);
        Assert.Equal("reviewer", Assert.Single(item.RequestedReviewers));
        var linkedTask = Assert.Single(item.Tasks);
        Assert.Equal("TP-604", linkedTask.Code);
        Assert.Equal("Waiting Room Leader", linkedTask.Reviewer?.DisplayName);

        var notifications = await leader.Client.GetFromJsonAsync<NotificationFeedResponse>(
            $"/api/workspaces/{setup.WorkspaceId}/notifications?filter=reviews");
        Assert.NotNull(notifications);
        Assert.Contains(notifications.Items, notification =>
            notification.Kind == "PullRequestReviewRequested" &&
            notification.ExternalUrl == $"https://github.com/{RepositoryFullName}/pull/42");

        await SendWebhookAsync(
            leader.Client,
            "waiting-room-checks",
            "check_suite",
            CheckSuitePayload("failure"));
        await WaitForDeliveryAsync(factory, "waiting-room-checks");
        waiting = await leader.Client.GetFromJsonAsync<PullRequestFeedResponse>(
            PullRequestsUrl(setup) + "?filter=waiting");
        Assert.Equal("Failing", Assert.Single(waiting!.Items).CheckStatus);

        await SendWebhookAsync(
            leader.Client,
            "waiting-room-changes",
            "pull_request_review",
            PullRequestReviewPayload("TP-604 waiting room", "changes_requested"));
        await WaitForDeliveryAsync(factory, "waiting-room-changes");
        var changes = await leader.Client.GetFromJsonAsync<PullRequestFeedResponse>(
            PullRequestsUrl(setup) + "?filter=changes");
        Assert.Equal("ChangesRequested", Assert.Single(changes!.Items).Status);

        await SendWebhookAsync(
            leader.Client,
            "waiting-room-merged",
            "pull_request",
            PullRequestPayload("closed", "TP-604 waiting room", merged: true, draft: false));
        await WaitForDeliveryAsync(factory, "waiting-room-merged");
        var merged = await leader.Client.GetFromJsonAsync<PullRequestFeedResponse>(
            PullRequestsUrl(setup) + "?filter=merged");
        Assert.Equal("Merged", Assert.Single(merged!.Items).Status);

        notifications = await leader.Client.GetFromJsonAsync<NotificationFeedResponse>(
            $"/api/workspaces/{setup.WorkspaceId}/notifications?filter=reviews");
        Assert.Contains(notifications!.Items, notification =>
            notification.Kind == "PullRequestChecksFailed");
        Assert.Contains(notifications.Items, notification =>
            notification.Kind == "PullRequestChangesRequested");
        Assert.Contains(notifications.Items, notification =>
            notification.Kind == "PullRequestMerged");
    }

    private WebApplicationFactory<Program> CreateEnabledFactory() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integrations:GitHub:Webhook:Enabled", "true");
            builder.UseSetting("Integrations:GitHub:Webhook:Secret", WebhookSecret);
        });

    private static async Task<ProjectSetup> CreateProjectSetupAsync(
        RegisteredUser leader,
        string taskCode)
    {
        var workspaceResponse = await leader.Client.PostAsJsonAsync(
            "/api/workspaces",
            new { name = $"GitHub automation {taskCode}" });
        workspaceResponse.EnsureSuccessStatusCode();
        var workspaceId = (await workspaceResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var projectResponse = await leader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects",
            new { name = $"Project {taskCode}" });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await projectResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var taskResponse = await leader.Client.PostAsJsonAsync(
            TasksUrl(workspaceId, projectId),
            new
            {
                code = taskCode,
                title = $"Task {taskCode}",
                description = "GitHub automation integration test",
                assigneeUserId = leader.UserId,
                reviewerUserId = leader.UserId,
                priority = "Medium",
                rowVersion = (string?)null,
            });
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
        var task = await taskResponse.Content.ReadFromJsonAsync<TaskDetailsResponse>();
        Assert.NotNull(task);

        var resourceResponse = await leader.Client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{projectId}/resources",
            new
            {
                title = "Source repository",
                type = "GitHub",
                url = $"https://github.com/{RepositoryFullName}",
                externalId = RepositoryFullName,
                ownerUserId = leader.UserId,
                linkedTaskId = (Guid?)null,
            });
        Assert.Equal(HttpStatusCode.Created, resourceResponse.StatusCode);

        return new ProjectSetup(workspaceId, projectId, task);
    }

    private static async Task<RegisteredUser> RegisterAsync(
        WebApplicationFactory<Program> factory,
        string displayName)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        var email = $"{Guid.NewGuid():N}@example.com";
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
        return new RegisteredUser(session.User.Id, client);
    }

    private static async Task<TaskDetailsResponse> ChangeStatusAsync(
        HttpClient client,
        ProjectSetup setup,
        TaskDetailsResponse task,
        string status)
    {
        var response = await client.PatchAsJsonAsync(
            $"{TasksUrl(setup.WorkspaceId, setup.ProjectId)}/{task.Id}/status",
            new { status, rowVersion = task.RowVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TaskDetailsResponse>())!;
    }

    private static async Task<TaskDetailsResponse> GetTaskAsync(
        HttpClient client,
        ProjectSetup setup,
        Guid taskId)
    {
        var response = await client.GetAsync(
            $"{TasksUrl(setup.WorkspaceId, setup.ProjectId)}/{taskId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDetailsResponse>())!;
    }

    private static async Task SendWebhookAsync(
        HttpClient client,
        string deliveryId,
        string eventName,
        byte[] payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(payload),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-GitHub-Delivery", deliveryId);
        request.Headers.Add("X-GitHub-Event", eventName);
        request.Headers.Add("X-Hub-Signature-256", CreateSignature(payload));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static async Task WaitForDeliveryAsync(
        WebApplicationFactory<Program> factory,
        string deliveryId)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
            var status = await dbContext.GitHubWebhookDeliveries
                .AsNoTracking()
                .Where(delivery => delivery.DeliveryId == deliveryId)
                .Select(delivery => delivery.Status)
                .SingleOrDefaultAsync();
            if (status == GitHubWebhookDeliveryStatus.Processed)
            {
                return;
            }

            if (status == GitHubWebhookDeliveryStatus.Failed)
            {
                throw new InvalidOperationException($"GitHub delivery '{deliveryId}' failed.");
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"GitHub delivery '{deliveryId}' was not processed.");
    }

    private static async Task<ProjectTask> FindTaskAsync(
        WebApplicationFactory<Program> factory,
        Guid taskId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        return await dbContext.Tasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(task => task.Id == taskId);
    }

    private static async Task<List<TaskEvidence>> ListAutomationEvidenceAsync(
        WebApplicationFactory<Program> factory,
        Guid taskId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        return await dbContext.TaskEvidence
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(evidence =>
                evidence.TaskId == taskId &&
                evidence.Source == TaskEvidenceSource.GitHubAutomation)
            .ToListAsync();
    }

    private static async Task<List<ActivityLog>> ListTaskActivitiesAsync(
        WebApplicationFactory<Program> factory,
        Guid taskId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        return await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(activity => activity.EntityId == taskId)
            .ToListAsync();
    }

    private static byte[] PullRequestPayload(
        string action,
        string title,
        bool merged,
        bool draft) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            action,
            number = 42,
            repository = Repository(),
            pull_request = new
            {
                number = 42,
                title,
                body = "Automated lifecycle integration test",
                html_url = $"https://github.com/{RepositoryFullName}/pull/42",
                created_at = "2026-10-20T08:00:00Z",
                user = new { login = "octocat" },
                requested_reviewers = new[] { new { login = "reviewer" } },
                merged,
                draft,
            },
        });

    private static byte[] PullRequestReviewPayload(string title, string state) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            action = "submitted",
            repository = Repository(),
            pull_request = new
            {
                number = 42,
                title,
                body = "Automated lifecycle integration test",
                html_url = $"https://github.com/{RepositoryFullName}/pull/42",
            },
            review = new { state },
        });

    private static byte[] CheckSuitePayload(string conclusion) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            action = "completed",
            repository = Repository(),
            check_suite = new
            {
                status = "completed",
                conclusion,
                pull_requests = new[] { new { number = 42 } },
            },
        });

    private static object Repository() => new
    {
        id = 123456789,
        full_name = RepositoryFullName,
    };

    private static string CreateSignature(byte[] payload)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), payload);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string TasksUrl(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}/tasks";

    private static string PullRequestsUrl(ProjectSetup setup) =>
        $"/api/workspaces/{setup.WorkspaceId}/projects/{setup.ProjectId}" +
        "/integrations/github/pull-requests";

    private sealed record ProjectSetup(
        Guid WorkspaceId,
        Guid ProjectId,
        TaskDetailsResponse Task);

    private sealed record RegisteredUser(Guid UserId, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);
    private sealed record AuthUserResponse(Guid Id);
    private sealed record IdResponse(Guid Id);
    private sealed record TaskDetailsResponse(
        Guid Id,
        string Status,
        string RowVersion);
    private sealed record PullRequestFeedResponse(
        IReadOnlyList<PullRequestResponse> Items,
        int WaitingCount,
        int ChangesCount,
        int MergedCount);
    private sealed record PullRequestResponse(
        string Status,
        string CheckStatus,
        string? AuthorLogin,
        IReadOnlyList<string> RequestedReviewers,
        IReadOnlyList<PullRequestTaskResponse> Tasks);
    private sealed record PullRequestTaskResponse(
        string Code,
        PullRequestReviewerResponse? Reviewer);
    private sealed record PullRequestReviewerResponse(string DisplayName);
    private sealed record NotificationFeedResponse(
        IReadOnlyList<NotificationResponse> Items,
        int UnreadCount);
    private sealed record NotificationResponse(string Kind, string? ExternalUrl);
}
