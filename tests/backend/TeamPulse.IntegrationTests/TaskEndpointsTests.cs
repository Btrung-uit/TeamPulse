using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Domain.Activities;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class TaskEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public TaskEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TaskCrud_SupportsFiltersUpdatesAndImmutableActivityHistory()
    {
        using var leader = await RegisterAsync("Task CRUD Leader");
        using var member = await RegisterAsync("Task CRUD Member");
        using var reviewer = await RegisterAsync("Task CRUD Reviewer");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Task CRUD Workspace");
        await AddMemberAsync(leader.Client, workspaceId, member.Email, "Member");
        await AddMemberAsync(leader.Client, workspaceId, reviewer.Email, "Member");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Task CRUD Project");
        var sprintId = await CreateSprintAsync(leader.Client, workspaceId, projectId, "Sprint API");
        var dueOn = new DateOnly(2026, 11, 15);

        var created = await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            new SaveTaskRequest(
                "tp-022",
                "Build task API",
                "Initial contract",
                member.UserId,
                reviewer.UserId,
                sprintId,
                "high",
                dueOn,
                8,
                null));

        Assert.Equal("TP-022", created.Code);
        Assert.Equal("Todo", created.Status);
        Assert.Equal("High", created.Priority);
        Assert.Equal(member.UserId, created.Assignee?.UserId);
        Assert.Equal(reviewer.UserId, created.Reviewer?.UserId);
        Assert.Equal(sprintId, created.Sprint?.Id);

        var duplicate = await leader.Client.PostAsJsonAsync(
            TasksUrl(workspaceId, projectId),
            new SaveTaskRequest(
                "TP-022",
                "Duplicate",
                null,
                null,
                null,
                null,
                "Medium",
                null,
                null,
                null));
        await AssertErrorAsync(duplicate, HttpStatusCode.Conflict, "task_code_conflict");

        await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            new SaveTaskRequest(
                "TP-023",
                "Second task",
                null,
                null,
                null,
                null,
                "Low",
                null,
                null,
                null));

        var filtered = await leader.Client.GetFromJsonAsync<PagedTaskResponse>(
            $"{TasksUrl(workspaceId, projectId)}?page=1&pageSize=1&search=TP-022" +
            $"&status=todo&priority=high&assigneeUserId={member.UserId}" +
            $"&sprintId={sprintId}&dueBefore=2026-11-16");
        Assert.NotNull(filtered);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(1, filtered.TotalPages);
        Assert.Equal(created.Id, Assert.Single(filtered.Items).Id);

        var update = await leader.Client.PutAsJsonAsync(
            $"{TasksUrl(workspaceId, projectId)}/{created.Id}",
            new SaveTaskRequest(
                created.Code,
                "Build complete task API",
                "Updated contract",
                reviewer.UserId,
                member.UserId,
                null,
                "Urgent",
                dueOn.AddDays(2),
                13,
                created.RowVersion));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<TaskDetailsResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Build complete task API", updated.Title);
        Assert.Equal(reviewer.UserId, updated.Assignee?.UserId);
        Assert.Null(updated.Sprint);

        var changeCode = await leader.Client.PutAsJsonAsync(
            $"{TasksUrl(workspaceId, projectId)}/{created.Id}",
            new SaveTaskRequest(
                "TP-999",
                updated.Title,
                updated.Description,
                updated.Assignee?.UserId,
                updated.Reviewer?.UserId,
                updated.Sprint?.Id,
                updated.Priority,
                updated.DueOn,
                updated.EstimateHours,
                updated.RowVersion));
        await AssertErrorAsync(changeCode, HttpStatusCode.Conflict, "task_code_immutable");

        var details = await leader.Client.GetFromJsonAsync<TaskDetailsResponse>(
            $"{TasksUrl(workspaceId, projectId)}/{created.Id}");
        Assert.NotNull(details);
        Assert.Equal("TP-022", details.Code);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var actions = await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .Where(activity => activity.WorkspaceId == workspaceId && activity.EntityId == created.Id)
            .OrderBy(activity => activity.OccurredAtUtc)
            .Select(activity => activity.Action)
            .ToListAsync();

        Assert.Contains(ActivityAction.TaskCreated, actions);
        Assert.Equal(2, actions.Count(action => action == ActivityAction.TaskAssigned));
        Assert.Equal(2, actions.Count(action => action == ActivityAction.TaskDeadlineChanged));
        Assert.Equal(2, actions.Count(action => action == ActivityAction.TaskSprintChanged));
        Assert.Contains(ActivityAction.TaskUpdated, actions);
    }

    [Fact]
    public async Task StatusFlow_EnforcesAssigneeReviewerLeaderAndAdvisorBoundaries()
    {
        using var leader = await RegisterAsync("Status Leader");
        using var assignee = await RegisterAsync("Status Assignee");
        using var reviewer = await RegisterAsync("Status Reviewer");
        using var otherMember = await RegisterAsync("Status Other");
        using var advisor = await RegisterAsync("Status Advisor");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Status Workspace");
        await AddMemberAsync(leader.Client, workspaceId, assignee.Email, "Member");
        await AddMemberAsync(leader.Client, workspaceId, reviewer.Email, "Member");
        await AddMemberAsync(leader.Client, workspaceId, otherMember.Email, "Member");
        await AddMemberAsync(leader.Client, workspaceId, advisor.Email, "Advisor");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Status Project");
        var task = await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            BasicTask("TP-101", assignee.UserId, reviewer.UserId));

        task = await ChangeStatusSuccessAsync(
            leader.Client,
            workspaceId,
            projectId,
            task,
            "Blocked");
        Assert.Equal("Todo", task.StatusBeforeBlocked);
        task = await ChangeStatusSuccessAsync(
            leader.Client,
            workspaceId,
            projectId,
            task,
            "Todo");
        Assert.Null(task.StatusBeforeBlocked);

        var invalid = await ChangeStatusAsync(
            leader.Client,
            workspaceId,
            projectId,
            task.Id,
            "Done",
            task.RowVersion);
        await AssertErrorAsync(invalid, HttpStatusCode.Conflict, "invalid_status_transition");

        var deniedOther = await ChangeStatusAsync(
            otherMember.Client,
            workspaceId,
            projectId,
            task.Id,
            "InProgress",
            task.RowVersion);
        await AssertErrorAsync(deniedOther, HttpStatusCode.Forbidden, "task_mutation_forbidden");

        var deniedAdvisor = await ChangeStatusAsync(
            advisor.Client,
            workspaceId,
            projectId,
            task.Id,
            "InProgress",
            task.RowVersion);
        await AssertErrorAsync(deniedAdvisor, HttpStatusCode.Forbidden, "task_mutation_forbidden");

        task = await ChangeStatusSuccessAsync(assignee.Client, workspaceId, projectId, task, "InProgress");
        task = await ChangeStatusSuccessAsync(assignee.Client, workspaceId, projectId, task, "EvidenceLinked");
        task = await ChangeStatusSuccessAsync(assignee.Client, workspaceId, projectId, task, "InReview");

        var memberCannotFinish = await ChangeStatusAsync(
            assignee.Client,
            workspaceId,
            projectId,
            task.Id,
            "Done",
            task.RowVersion);
        await AssertErrorAsync(
            memberCannotFinish,
            HttpStatusCode.Forbidden,
            "task_mutation_forbidden");

        task = await ChangeStatusSuccessAsync(reviewer.Client, workspaceId, projectId, task, "NeedsChanges");
        task = await ChangeStatusSuccessAsync(leader.Client, workspaceId, projectId, task, "InReview");
        task = await ChangeStatusSuccessAsync(leader.Client, workspaceId, projectId, task, "Done");
        Assert.Equal("Done", task.Status);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        Assert.Equal(
            8,
            await dbContext.ActivityLogs
                .IgnoreQueryFilters()
                .CountAsync(activity =>
                    activity.EntityId == task.Id &&
                    activity.Action == ActivityAction.TaskStatusChanged));
    }

    [Fact]
    public async Task Mutations_WithStaleRowVersion_ReturnClearConflictWithoutChanges()
    {
        using var leader = await RegisterAsync("Conflict Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Conflict Workspace");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Conflict Project");
        var task = await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            BasicTask("TP-201", leader.UserId, null));
        var stale = Convert.ToBase64String([1, 2, 3]);

        var update = await leader.Client.PutAsJsonAsync(
            $"{TasksUrl(workspaceId, projectId)}/{task.Id}",
            new SaveTaskRequest(
                task.Code,
                "Should not persist",
                task.Description,
                task.Assignee?.UserId,
                task.Reviewer?.UserId,
                task.Sprint?.Id,
                task.Priority,
                task.DueOn,
                task.EstimateHours,
                stale));
        var status = await ChangeStatusAsync(
            leader.Client,
            workspaceId,
            projectId,
            task.Id,
            "InProgress",
            stale);
        var archive = await leader.Client.PostAsJsonAsync(
            $"{TasksUrl(workspaceId, projectId)}/{task.Id}/archive",
            new ArchiveTaskRequest(stale));

        await AssertErrorAsync(update, HttpStatusCode.Conflict, "task_conflict");
        await AssertErrorAsync(status, HttpStatusCode.Conflict, "task_conflict");
        await AssertErrorAsync(archive, HttpStatusCode.Conflict, "task_conflict");

        var unchanged = await leader.Client.GetFromJsonAsync<TaskDetailsResponse>(
            $"{TasksUrl(workspaceId, projectId)}/{task.Id}");
        Assert.NotNull(unchanged);
        Assert.Equal("Task TP-201", unchanged.Title);
        Assert.Equal("Todo", unchanged.Status);
        Assert.False(unchanged.IsArchived);
    }

    [Fact]
    public async Task Archive_IsIdempotentHiddenByDefaultAndRetainsHistory()
    {
        using var leader = await RegisterAsync("Archive Leader");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Archive Workspace");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Archive Project");
        var task = await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            BasicTask("TP-301", leader.UserId, null));
        task = await ChangeStatusSuccessAsync(leader.Client, workspaceId, projectId, task, "InProgress");

        var archiveUrl = $"{TasksUrl(workspaceId, projectId)}/{task.Id}/archive";
        var first = await leader.Client.PostAsJsonAsync(
            archiveUrl,
            new ArchiveTaskRequest(task.RowVersion));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var archived = await first.Content.ReadFromJsonAsync<TaskDetailsResponse>();
        Assert.NotNull(archived);
        Assert.True(archived.IsArchived);
        Assert.NotNull(archived.ArchivedAtUtc);

        var second = await leader.Client.PostAsJsonAsync(
            archiveUrl,
            new ArchiveTaskRequest(Convert.ToBase64String([9])));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var defaultList = await leader.Client.GetFromJsonAsync<PagedTaskResponse>(
            TasksUrl(workspaceId, projectId));
        var archivedList = await leader.Client.GetFromJsonAsync<PagedTaskResponse>(
            $"{TasksUrl(workspaceId, projectId)}?includeArchived=true");
        Assert.NotNull(defaultList);
        Assert.NotNull(archivedList);
        Assert.DoesNotContain(defaultList.Items, item => item.Id == task.Id);
        Assert.Contains(archivedList.Items, item => item.Id == task.Id && item.IsArchived);

        var updateArchived = await leader.Client.PutAsJsonAsync(
            $"{TasksUrl(workspaceId, projectId)}/{task.Id}",
            new SaveTaskRequest(
                task.Code,
                "Archived update",
                task.Description,
                task.Assignee?.UserId,
                task.Reviewer?.UserId,
                task.Sprint?.Id,
                task.Priority,
                task.DueOn,
                task.EstimateHours,
                archived.RowVersion));
        await AssertErrorAsync(updateArchived, HttpStatusCode.Conflict, "task_archived");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var history = await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .Where(activity => activity.EntityId == task.Id)
            .Select(activity => activity.Action)
            .ToListAsync();
        Assert.Contains(ActivityAction.TaskCreated, history);
        Assert.Contains(ActivityAction.TaskStatusChanged, history);
        Assert.Equal(1, history.Count(action => action == ActivityAction.TaskArchived));
    }

    [Fact]
    public async Task TaskRoutes_EnforceWorkspaceRoleAndProjectTenantBoundaries()
    {
        using var leaderA = await RegisterAsync("Boundary Leader A");
        using var leaderB = await RegisterAsync("Boundary Leader B");
        using var member = await RegisterAsync("Boundary Member");
        var workspaceA = await CreateWorkspaceAsync(leaderA.Client, "Boundary Workspace A");
        var workspaceB = await CreateWorkspaceAsync(leaderB.Client, "Boundary Workspace B");
        await AddMemberAsync(leaderA.Client, workspaceA, member.Email, "Member");
        var projectA = await CreateProjectAsync(leaderA.Client, workspaceA, "Boundary Project A");
        var projectB = await CreateProjectAsync(leaderB.Client, workspaceB, "Boundary Project B");
        var taskA = await CreateTaskAsync(
            leaderA.Client,
            workspaceA,
            projectA,
            BasicTask("TP-401", member.UserId, null));

        Assert.Equal(
            HttpStatusCode.OK,
            (await member.Client.GetAsync(TasksUrl(workspaceA, projectA))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await member.Client.PostAsJsonAsync(
                TasksUrl(workspaceA, projectA),
                BasicTask("TP-402", member.UserId, null))).StatusCode);

        var foreignWorkspace = await leaderA.Client.GetAsync(TasksUrl(workspaceB, projectB));
        Assert.Equal(HttpStatusCode.Forbidden, foreignWorkspace.StatusCode);

        var disguisedProject = await leaderA.Client.GetAsync(TasksUrl(workspaceA, projectB));
        await AssertErrorAsync(disguisedProject, HttpStatusCode.NotFound, "project_not_found");

        var disguisedTask = await leaderA.Client.GetAsync(
            $"{TasksUrl(workspaceA, projectA)}/{Guid.NewGuid()}");
        await AssertErrorAsync(disguisedTask, HttpStatusCode.NotFound, "task_not_found");

        var outsiderAssignee = await leaderA.Client.PostAsJsonAsync(
            TasksUrl(workspaceA, projectA),
            BasicTask("TP-403", leaderB.UserId, null));
        await AssertErrorAsync(outsiderAssignee, HttpStatusCode.BadRequest, "assignee_not_found");

        Assert.Equal(taskA.Id, (await member.Client.GetFromJsonAsync<TaskDetailsResponse>(
            $"{TasksUrl(workspaceA, projectA)}/{taskA.Id}"))?.Id);
    }

    [Fact]
    public async Task EvidenceAndManualReview_ValidateLinksPermissionsWorkflowAndActivity()
    {
        using var leader = await RegisterAsync("Evidence Leader");
        using var assignee = await RegisterAsync("Evidence Assignee");
        using var otherMember = await RegisterAsync("Evidence Other");
        var workspaceId = await CreateWorkspaceAsync(leader.Client, "Evidence Workspace");
        await AddMemberAsync(leader.Client, workspaceId, assignee.Email, "Member");
        await AddMemberAsync(leader.Client, workspaceId, otherMember.Email, "Member");
        var projectId = await CreateProjectAsync(leader.Client, workspaceId, "Evidence Project");
        var task = await CreateTaskAsync(
            leader.Client,
            workspaceId,
            projectId,
            BasicTask("TP-501", assignee.UserId, null));
        task = await ChangeStatusSuccessAsync(
            assignee.Client,
            workspaceId,
            projectId,
            task,
            "InProgress");
        var collaborationUrl = $"{TasksUrl(workspaceId, projectId)}/{task.Id}";

        var invalidUrl = await assignee.Client.PostAsJsonAsync(
            $"{collaborationUrl}/evidence",
            new AddEvidenceRequest("Commit", "javascript:alert(1)", null));
        await AssertErrorAsync(invalidUrl, HttpStatusCode.BadRequest, "validation_failed");

        var forbidden = await otherMember.Client.PostAsJsonAsync(
            $"{collaborationUrl}/evidence",
            new AddEvidenceRequest("Commit", "https://github.com/example/commit/1", null));
        await AssertErrorAsync(
            forbidden,
            HttpStatusCode.Forbidden,
            "task_collaboration_forbidden");

        var addEvidence = await assignee.Client.PostAsJsonAsync(
            $"{collaborationUrl}/evidence",
            new AddEvidenceRequest(
                "PullRequest",
                "https://github.com/example/pull/42",
                "Ready for review"));
        Assert.Equal(HttpStatusCode.Created, addEvidence.StatusCode);

        task = (await assignee.Client.GetFromJsonAsync<TaskDetailsResponse>(collaborationUrl))!;
        Assert.Equal("EvidenceLinked", task.Status);
        Assert.NotEqual("Done", task.Status);

        var collaboration = await assignee.Client
            .GetFromJsonAsync<TaskCollaborationResponse>($"{collaborationUrl}/collaboration");
        Assert.NotNull(collaboration);
        var evidence = Assert.Single(collaboration.Evidence);
        Assert.Equal("PullRequest", evidence.Type);
        Assert.Equal(assignee.UserId, evidence.SubmittedBy.UserId);

        task = await ChangeStatusSuccessAsync(
            assignee.Client,
            workspaceId,
            projectId,
            task,
            "InReview");
        var memberReview = await assignee.Client.PostAsJsonAsync(
            $"{collaborationUrl}/review",
            new ReviewTaskRequest("Accept", "Looks good", task.RowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, memberReview.StatusCode);

        var requestChanges = await leader.Client.PostAsJsonAsync(
            $"{collaborationUrl}/review",
            new ReviewTaskRequest(
                "RequestChanges",
                "Please add a failure-path test.",
                task.RowVersion));
        Assert.True(
            requestChanges.IsSuccessStatusCode,
            await requestChanges.Content.ReadAsStringAsync());
        var changesResult = await requestChanges.Content.ReadFromJsonAsync<TaskReviewResponse>();
        Assert.NotNull(changesResult);
        Assert.Equal("NeedsChanges", changesResult.Status);
        Assert.Equal("ChangesRequested", changesResult.Comment.Kind);

        task = await ChangeStatusSuccessAsync(
            leader.Client,
            workspaceId,
            projectId,
            task with { RowVersion = changesResult.RowVersion },
            "InReview");
        var accept = await leader.Client.PostAsJsonAsync(
            $"{collaborationUrl}/review",
            new ReviewTaskRequest("Accept", "Verified and accepted.", task.RowVersion));
        accept.EnsureSuccessStatusCode();
        var accepted = await accept.Content.ReadFromJsonAsync<TaskReviewResponse>();
        Assert.NotNull(accepted);
        Assert.Equal("Done", accepted.Status);
        Assert.Equal("ReviewAccepted", accepted.Comment.Kind);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamPulseDbContext>();
        var actions = await dbContext.ActivityLogs
            .IgnoreQueryFilters()
            .Where(activity => activity.EntityId == task.Id)
            .Select(activity => activity.Action)
            .ToListAsync();
        Assert.Contains(ActivityAction.TaskEvidenceAdded, actions);
        Assert.Contains(ActivityAction.TaskChangesRequested, actions);
        Assert.Contains(ActivityAction.TaskReviewAccepted, actions);
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
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotNull(result);
        return result.Id;
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
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotNull(result);
        return result.Id;
    }

    private static async Task<Guid> CreateSprintAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/projects/{projectId}/sprints",
            new
            {
                name,
                goal = "Task API tests",
                startsOn = new DateOnly(2026, 11, 1),
                endsOn = new DateOnly(2026, 11, 30),
            });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotNull(result);
        return result.Id;
    }

    private static async Task<TaskDetailsResponse> CreateTaskAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        SaveTaskRequest request)
    {
        var response = await client.PostAsJsonAsync(TasksUrl(workspaceId, projectId), request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TaskDetailsResponse>();
        Assert.NotNull(result);
        return result;
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        Guid taskId,
        string status,
        string rowVersion) =>
        client.PatchAsJsonAsync(
            $"{TasksUrl(workspaceId, projectId)}/{taskId}/status",
            new ChangeStatusRequest(status, rowVersion));

    private static async Task<TaskDetailsResponse> ChangeStatusSuccessAsync(
        HttpClient client,
        Guid workspaceId,
        Guid projectId,
        TaskDetailsResponse task,
        string status)
    {
        var response = await ChangeStatusAsync(
            client,
            workspaceId,
            projectId,
            task.Id,
            status,
            task.RowVersion);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TaskDetailsResponse>();
        Assert.NotNull(result);
        return result;
    }

    private static SaveTaskRequest BasicTask(
        string code,
        Guid? assigneeUserId,
        Guid? reviewerUserId) =>
        new(
            code,
            $"Task {code}",
            "Task API integration test",
            assigneeUserId,
            reviewerUserId,
            null,
            "Medium",
            null,
            null,
            null);

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(expectedCode, error.Code);
    }

    private static string TasksUrl(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}/tasks";

    private sealed record RegisteredUser(Guid UserId, string Email, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthSessionResponse(AuthUserResponse User, string AccessToken);

    private sealed record AuthUserResponse(Guid Id);

    private sealed record IdResponse(Guid Id);

    private sealed record SaveTaskRequest(
        string? Code,
        string? Title,
        string? Description,
        Guid? AssigneeUserId,
        Guid? ReviewerUserId,
        Guid? SprintId,
        string? Priority,
        DateOnly? DueOn,
        decimal? EstimateHours,
        string? RowVersion);

    private sealed record ChangeStatusRequest(string Status, string RowVersion);

    private sealed record ArchiveTaskRequest(string RowVersion);

    private sealed record AddEvidenceRequest(string Type, string Url, string? Note);

    private sealed record ReviewTaskRequest(string Decision, string Comment, string RowVersion);

    private sealed record TaskMemberResponse(Guid UserId, string DisplayName, string Email);

    private sealed record TaskSprintResponse(Guid Id, string Name);

    private sealed record TaskEvidenceResponse(
        Guid Id,
        string Type,
        string Url,
        string? Note,
        TaskMemberResponse SubmittedBy,
        DateTimeOffset CreatedAtUtc);

    private sealed record TaskCommentResponse(
        Guid Id,
        string Kind,
        string Body,
        TaskMemberResponse Author,
        DateTimeOffset CreatedAtUtc);

    private sealed record TaskCollaborationResponse(
        IReadOnlyList<TaskEvidenceResponse> Evidence,
        IReadOnlyList<TaskCommentResponse> Comments);

    private sealed record TaskReviewResponse(
        Guid TaskId,
        string Status,
        string RowVersion,
        TaskCommentResponse Comment);

    private sealed record TaskDetailsResponse(
        Guid Id,
        Guid WorkspaceId,
        Guid ProjectId,
        string Code,
        string Title,
        string? Description,
        string Status,
        string? StatusBeforeBlocked,
        string Priority,
        DateOnly? DueOn,
        decimal? EstimateHours,
        TaskMemberResponse? Assignee,
        TaskMemberResponse? Reviewer,
        TaskSprintResponse? Sprint,
        bool IsArchived,
        DateTimeOffset? ArchivedAtUtc,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        string RowVersion);

    private sealed record TaskListItemResponse(Guid Id, bool IsArchived);

    private sealed record PagedTaskResponse(
        IReadOnlyList<TaskListItemResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record ApiErrorResponse(string Code, string Message);
}
