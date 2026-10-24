using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamPulse.Application.Integrations;
using TeamPulse.Domain.Activities;
using TeamPulse.Domain.Integrations;
using TeamPulse.Domain.Notifications;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Resources;
using TeamPulse.Domain.Tasks;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Integrations.GitHub;

internal sealed partial class GitHubTaskAutomationHandler(
    TeamPulseDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<GitHubTaskAutomationHandler> logger) : IGitHubWebhookEventHandler
{
    public async Task HandleAsync(
        GitHubWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookEvent.RepositoryFullName) ||
            !RepositoryNamePattern().IsMatch(webhookEvent.RepositoryFullName))
        {
            logger.LogWarning(
                "GitHub delivery {DeliveryId} has no usable repository name.",
                webhookEvent.DeliveryId);
            return;
        }

        var automation = ParseAutomation(webhookEvent) ?? ParsedAutomation.Empty;
        var projectsPullRequests = webhookEvent.EventName is
            "pull_request" or "pull_request_review" or "check_suite";
        if (automation.TaskCodes.Count == 0 && !projectsPullRequests)
        {
            return;
        }

        var githubResources = await dbContext.ProjectResources
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(resource =>
                resource.Type == ResourceType.GitHub &&
                resource.ExternalId != null &&
                !resource.Project.IsArchived)
            .Select(resource => new ProjectMapping(
                resource.WorkspaceId,
                resource.ProjectId,
                resource.ExternalId!))
            .ToListAsync(cancellationToken);
        var mappings = githubResources
            .Where(resource => string.Equals(
                resource.RepositoryFullName,
                webhookEvent.RepositoryFullName,
                StringComparison.OrdinalIgnoreCase))
            .DistinctBy(resource => new { resource.WorkspaceId, resource.ProjectId })
            .ToArray();

        foreach (var mapping in mappings)
        {
            if (projectsPullRequests)
            {
                await ApplyPullRequestProjectionAsync(
                    mapping,
                    automation,
                    webhookEvent,
                    cancellationToken);
            }

            if (automation.TaskCodes.Count > 0)
            {
                await ApplyToProjectAsync(
                    mapping,
                    automation,
                    webhookEvent.DeliveryId,
                    cancellationToken);
            }
        }
    }

    private async Task ApplyPullRequestProjectionAsync(
        ProjectMapping mapping,
        ParsedAutomation automation,
        GitHubWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(webhookEvent.Payload);
        var root = document.RootElement;
        if (webhookEvent.EventName == "check_suite")
        {
            await ApplyCheckSuiteAsync(mapping, webhookEvent, root, cancellationToken);
            return;
        }

        if (!TryGetObject(root, "pull_request", out var payload))
        {
            return;
        }

        var number = GetInt32(payload, "number") ?? GetInt32(root, "number");
        if (!number.HasValue || number <= 0)
        {
            return;
        }

        var pullRequest = await dbContext.GitHubPullRequests
            .IgnoreQueryFilters()
            .Include(item => item.TaskLinks)
            .SingleOrDefaultAsync(item =>
                item.WorkspaceId == mapping.WorkspaceId &&
                item.ProjectId == mapping.ProjectId &&
                item.RepositoryFullName == mapping.RepositoryFullName &&
                item.Number == number.Value,
                cancellationToken);
        var now = timeProvider.GetUtcNow();
        var title = GetString(payload, "title") ?? $"Pull request #{number.Value}";
        var url = GetString(payload, "html_url") ??
            $"https://github.com/{mapping.RepositoryFullName}/pull/{number.Value}";
        var authorLogin = TryGetObject(payload, "user", out var author)
            ? GetString(author, "login")
            : pullRequest?.AuthorLogin;
        var requestedReviewers = GetRequestedReviewers(payload) ??
            pullRequest?.RequestedReviewers;
        var status = ResolvePullRequestStatus(
            webhookEvent.EventName,
            webhookEvent.Action,
            payload,
            root,
            pullRequest?.Status);
        var openedAt = GetDateTimeOffset(payload, "created_at") ?? now;

        if (pullRequest is null)
        {
            pullRequest = new GitHubPullRequest(
                Guid.NewGuid(),
                mapping.WorkspaceId,
                mapping.ProjectId,
                mapping.RepositoryFullName,
                number.Value,
                title,
                url,
                authorLogin,
                requestedReviewers,
                status,
                openedAt,
                now);
            dbContext.GitHubPullRequests.Add(pullRequest);
        }
        else
        {
            pullRequest.Update(
                title,
                url,
                authorLogin,
                requestedReviewers,
                status,
                now);
        }

        if (webhookEvent.Action == "synchronize")
        {
            pullRequest.UpdateChecks(GitHubCheckStatus.Pending, now);
        }

        var linkedTasks = await dbContext.Tasks
            .IgnoreQueryFilters()
            .Where(task =>
                task.WorkspaceId == mapping.WorkspaceId &&
                task.ProjectId == mapping.ProjectId &&
                !task.IsArchived &&
                automation.TaskCodes.Contains(task.Code.Value))
            .ToListAsync(cancellationToken);
        SynchronizeTaskLinks(pullRequest, linkedTasks);
        await AddLifecycleNotificationsAsync(
            pullRequest,
            linkedTasks,
            webhookEvent,
            cancellationToken);
    }

    private async Task ApplyCheckSuiteAsync(
        ProjectMapping mapping,
        GitHubWebhookEvent webhookEvent,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        if (!TryGetObject(root, "check_suite", out var checkSuite) ||
            !checkSuite.TryGetProperty("pull_requests", out var pullRequests) ||
            pullRequests.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var checkStatus = ResolveCheckStatus(checkSuite);
        var numbers = pullRequests.EnumerateArray()
            .Select(item => GetInt32(item, "number"))
            .Where(number => number is > 0)
            .Select(number => number!.Value)
            .Distinct()
            .ToArray();
        if (numbers.Length == 0)
        {
            return;
        }

        var snapshots = await dbContext.GitHubPullRequests
            .IgnoreQueryFilters()
            .Include(item => item.TaskLinks)
                .ThenInclude(link => link.Task)
            .Where(item =>
                item.WorkspaceId == mapping.WorkspaceId &&
                item.ProjectId == mapping.ProjectId &&
                item.RepositoryFullName == mapping.RepositoryFullName &&
                numbers.Contains(item.Number))
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var snapshot in snapshots)
        {
            snapshot.UpdateChecks(checkStatus, now);
            if (checkStatus == GitHubCheckStatus.Failing)
            {
                await AddNotificationsAsync(
                    snapshot,
                    snapshot.TaskLinks.Select(link => link.Task),
                    NotificationKind.PullRequestChecksFailed,
                    task => task.AssigneeUserId,
                    "Checks failed",
                    "GitHub checks failed. Open the pull request to inspect the failing jobs.",
                    webhookEvent.DeliveryId,
                    cancellationToken);
            }
        }
    }

    private async Task AddLifecycleNotificationsAsync(
        GitHubPullRequest pullRequest,
        IReadOnlyCollection<ProjectTask> tasks,
        GitHubWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        if (webhookEvent.EventName == "pull_request" &&
            webhookEvent.Action is "opened" or "reopened" or "ready_for_review" &&
            pullRequest.Status == GitHubPullRequestStatus.WaitingForReview)
        {
            await AddNotificationsAsync(
                pullRequest,
                tasks,
                NotificationKind.PullRequestReviewRequested,
                task => task.ReviewerUserId,
                "PR ready for review",
                "A linked pull request is ready for review on GitHub.",
                webhookEvent.DeliveryId,
                cancellationToken);
        }
        else if (webhookEvent.EventName == "pull_request_review" &&
                 webhookEvent.Action == "submitted" &&
                 pullRequest.Status == GitHubPullRequestStatus.ChangesRequested)
        {
            await AddNotificationsAsync(
                pullRequest,
                tasks,
                NotificationKind.PullRequestChangesRequested,
                task => task.AssigneeUserId,
                "PR changes requested",
                "A reviewer requested changes. Open the pull request for feedback.",
                webhookEvent.DeliveryId,
                cancellationToken);
        }
        else if (webhookEvent.EventName == "pull_request" &&
                 pullRequest.Status == GitHubPullRequestStatus.Merged)
        {
            var recipients = tasks
                .SelectMany(task => new[]
                {
                    (Task: task, UserId: task.AssigneeUserId),
                    (Task: task, UserId: task.ReviewerUserId),
                })
                .Where(item => item.UserId.HasValue)
                .DistinctBy(item => new { item.Task.Id, item.UserId })
                .ToArray();
            foreach (var recipient in recipients)
            {
                await AddNotificationAsync(
                    pullRequest,
                    recipient.Task,
                    recipient.UserId!.Value,
                    NotificationKind.PullRequestMerged,
                    "PR merged",
                    "A linked pull request was merged on GitHub.",
                    webhookEvent.DeliveryId,
                    cancellationToken);
            }
        }
    }

    private async Task AddNotificationsAsync(
        GitHubPullRequest pullRequest,
        IEnumerable<ProjectTask> tasks,
        NotificationKind kind,
        Func<ProjectTask, Guid?> recipientSelector,
        string title,
        string message,
        string deliveryId,
        CancellationToken cancellationToken)
    {
        foreach (var task in tasks)
        {
            var recipientUserId = recipientSelector(task);
            if (!recipientUserId.HasValue)
            {
                continue;
            }

            await AddNotificationAsync(
                pullRequest,
                task,
                recipientUserId.Value,
                kind,
                title,
                message,
                deliveryId,
                cancellationToken);
        }
    }

    private async Task AddNotificationAsync(
        GitHubPullRequest pullRequest,
        ProjectTask task,
        Guid recipientUserId,
        NotificationKind kind,
        string title,
        string message,
        string deliveryId,
        CancellationToken cancellationToken)
    {
        var sourceReference = $"github:{deliveryId}:{task.Id:N}:{kind}";
        var exists = await dbContext.Notifications
            .IgnoreQueryFilters()
            .AnyAsync(notification =>
                notification.WorkspaceId == task.WorkspaceId &&
                notification.RecipientUserId == recipientUserId &&
                notification.SourceReference == sourceReference,
                cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.Notifications.Add(new Notification(
            Guid.NewGuid(),
            task.WorkspaceId,
            recipientUserId,
            kind,
            $"{title}: {task.Code.Value}",
            message,
            task.ProjectId,
            task.Id,
            pullRequest.Url,
            sourceReference));
    }

    private static void SynchronizeTaskLinks(
        GitHubPullRequest pullRequest,
        IReadOnlyCollection<ProjectTask> tasks)
    {
        var taskIds = tasks.Select(task => task.Id).ToHashSet();
        foreach (var staleLink in pullRequest.TaskLinks
                     .Where(link => !taskIds.Contains(link.TaskId))
                     .ToArray())
        {
            pullRequest.TaskLinks.Remove(staleLink);
        }

        var existingTaskIds = pullRequest.TaskLinks.Select(link => link.TaskId).ToHashSet();
        foreach (var task in tasks.Where(task => !existingTaskIds.Contains(task.Id)))
        {
            pullRequest.TaskLinks.Add(new GitHubPullRequestTask(
                pullRequest.Id,
                pullRequest.WorkspaceId,
                pullRequest.ProjectId,
                task.Id));
        }
    }

    private async Task ApplyToProjectAsync(
        ProjectMapping mapping,
        ParsedAutomation automation,
        string deliveryId,
        CancellationToken cancellationToken)
    {
        var tasks = await dbContext.Tasks
            .IgnoreQueryFilters()
            .Where(task =>
                task.WorkspaceId == mapping.WorkspaceId &&
                task.ProjectId == mapping.ProjectId &&
                !task.IsArchived)
            .ToListAsync(cancellationToken);
        var tasksByCode = tasks
            .Where(task => automation.TaskCodes.Contains(task.Code.Value))
            .ToDictionary(task => task.Code.Value, StringComparer.OrdinalIgnoreCase);
        if (tasksByCode.Count == 0)
        {
            return;
        }

        var taskIds = tasksByCode.Values.Select(task => task.Id).ToArray();
        var existingEvidence = await dbContext.TaskEvidence
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(evidence =>
                evidence.WorkspaceId == mapping.WorkspaceId &&
                evidence.ProjectId == mapping.ProjectId &&
                taskIds.Contains(evidence.TaskId) &&
                evidence.Source == TaskEvidenceSource.GitHubAutomation &&
                evidence.ExternalId != null)
            .Select(evidence => new { evidence.TaskId, evidence.ExternalId })
            .ToListAsync(cancellationToken);
        var evidenceKeys = existingEvidence
            .Select(evidence => EvidenceKey(evidence.TaskId, evidence.ExternalId!))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var task in tasksByCode.Values)
        {
            var previousStatus = task.Status;
            var evidenceAdded = false;
            foreach (var candidate in automation.Evidence.Where(item =>
                         item.TaskCodes.Contains(task.Code.Value)))
            {
                var evidenceKey = EvidenceKey(task.Id, candidate.ExternalId);
                if (!evidenceKeys.Add(evidenceKey))
                {
                    continue;
                }

                dbContext.TaskEvidence.Add(TaskEvidence.CreateGitHubAutomation(
                    Guid.NewGuid(),
                    task.WorkspaceId,
                    task.ProjectId,
                    task.Id,
                    candidate.Type,
                    candidate.Url,
                    candidate.Note,
                    candidate.ExternalId));
                AddAutomationActivity(
                    task,
                    ActivityAction.TaskEvidenceAdded,
                    deliveryId,
                    newValue: candidate.Type.ToString());
                evidenceAdded = true;
            }

            if (evidenceAdded && task.Status == ProjectTaskStatus.InProgress)
            {
                task.TransitionTo(ProjectTaskStatus.EvidenceLinked);
            }

            ApplyTargetStatus(task, automation.TargetStatus);
            if (task.Status != previousStatus)
            {
                AddAutomationActivity(
                    task,
                    ActivityAction.TaskStatusChanged,
                    deliveryId,
                    previousStatus.ToString(),
                    task.Status.ToString());
            }
        }
    }

    private void AddAutomationActivity(
        ProjectTask task,
        ActivityAction action,
        string deliveryId,
        string? previousValue = null,
        string? newValue = null)
    {
        dbContext.ActivityLogs.Add(ActivityLog.CreateGitHubAutomation(
            Guid.NewGuid(),
            task.WorkspaceId,
            action,
            nameof(ProjectTask),
            task.Id,
            timeProvider.GetUtcNow(),
            deliveryId,
            previousValue,
            newValue));
    }

    private static void ApplyTargetStatus(
        ProjectTask task,
        ProjectTaskStatus? targetStatus)
    {
        if (!targetStatus.HasValue ||
            task.Status is ProjectTaskStatus.Blocked or ProjectTaskStatus.Done)
        {
            return;
        }

        switch (targetStatus.Value)
        {
            case ProjectTaskStatus.InReview:
                AdvanceToInReview(task);
                break;
            case ProjectTaskStatus.NeedsChanges when task.Status == ProjectTaskStatus.InReview:
                task.TransitionTo(ProjectTaskStatus.NeedsChanges);
                break;
            case ProjectTaskStatus.Done:
                AdvanceToInReview(task);
                if (task.Status == ProjectTaskStatus.InReview)
                {
                    task.TransitionTo(ProjectTaskStatus.Done);
                }

                break;
            case ProjectTaskStatus.InProgress when task.Status is
                ProjectTaskStatus.EvidenceLinked or
                ProjectTaskStatus.InReview or
                ProjectTaskStatus.NeedsChanges:
                task.TransitionTo(ProjectTaskStatus.InProgress);
                break;
        }
    }

    private static void AdvanceToInReview(ProjectTask task)
    {
        if (task.Status == ProjectTaskStatus.Todo)
        {
            task.TransitionTo(ProjectTaskStatus.InProgress);
        }

        if (task.Status == ProjectTaskStatus.InProgress)
        {
            task.TransitionTo(ProjectTaskStatus.EvidenceLinked);
        }

        if (task.Status is ProjectTaskStatus.EvidenceLinked or ProjectTaskStatus.NeedsChanges)
        {
            task.TransitionTo(ProjectTaskStatus.InReview);
        }
    }

    private static ParsedAutomation? ParseAutomation(GitHubWebhookEvent webhookEvent)
    {
        using var document = JsonDocument.Parse(webhookEvent.Payload);
        var root = document.RootElement;
        return webhookEvent.EventName switch
        {
            "push" => ParsePush(root, webhookEvent.RepositoryFullName!),
            "pull_request" => ParsePullRequest(
                root,
                webhookEvent.Action,
                webhookEvent.RepositoryFullName!),
            "pull_request_review" => ParsePullRequestReview(
                root,
                webhookEvent.Action,
                webhookEvent.RepositoryFullName!),
            _ => null,
        };
    }

    private static ParsedAutomation ParsePush(JsonElement root, string repositoryFullName)
    {
        var evidence = new List<EvidenceCandidate>();
        if (root.TryGetProperty("commits", out var commits) &&
            commits.ValueKind == JsonValueKind.Array)
        {
            foreach (var commit in commits.EnumerateArray())
            {
                var sha = GetString(commit, "id");
                var message = GetString(commit, "message");
                var codes = ExtractTaskCodes(message);
                if (codes.Count == 0 ||
                    string.IsNullOrWhiteSpace(sha) ||
                    !CommitShaPattern().IsMatch(sha))
                {
                    continue;
                }

                evidence.Add(new EvidenceCandidate(
                    TaskEvidenceType.Commit,
                    $"https://github.com/{repositoryFullName}/commit/{sha}",
                    TruncateNote($"GitHub commit {sha[..Math.Min(7, sha.Length)]}: {FirstLine(message)}"),
                    $"commit:{sha.ToLowerInvariant()}",
                    codes));
            }
        }

        return new ParsedAutomation(
            evidence,
            evidence.SelectMany(item => item.TaskCodes).ToHashSet(StringComparer.OrdinalIgnoreCase),
            TargetStatus: null);
    }

    private static ParsedAutomation ParsePullRequest(
        JsonElement root,
        string? action,
        string repositoryFullName)
    {
        if (!TryGetObject(root, "pull_request", out var pullRequest))
        {
            return ParsedAutomation.Empty;
        }

        var codes = ExtractTaskCodes(
            GetString(pullRequest, "title"),
            GetString(pullRequest, "body"));
        var number = GetInt32(root, "number");
        var evidence = CreatePullRequestEvidence(
            pullRequest,
            repositoryFullName,
            number,
            codes);
        var merged = GetBoolean(pullRequest, "merged");
        var draft = GetBoolean(pullRequest, "draft");
        var targetStatus = action switch
        {
            "ready_for_review" => ProjectTaskStatus.InReview,
            "opened" or "reopened" when !draft => ProjectTaskStatus.InReview,
            "closed" when merged => ProjectTaskStatus.Done,
            "closed" => ProjectTaskStatus.InProgress,
            _ => (ProjectTaskStatus?)null,
        };
        return new ParsedAutomation(
            evidence is null ? [] : [evidence],
            codes,
            targetStatus);
    }

    private static ParsedAutomation ParsePullRequestReview(
        JsonElement root,
        string? action,
        string repositoryFullName)
    {
        if (!TryGetObject(root, "pull_request", out var pullRequest))
        {
            return ParsedAutomation.Empty;
        }

        var codes = ExtractTaskCodes(
            GetString(pullRequest, "title"),
            GetString(pullRequest, "body"));
        var number = GetInt32(pullRequest, "number") ?? GetInt32(root, "number");
        var evidence = CreatePullRequestEvidence(
            pullRequest,
            repositoryFullName,
            number,
            codes);
        var reviewState = TryGetObject(root, "review", out var review)
            ? GetString(review, "state")
            : null;
        var targetStatus = action == "submitted" &&
            string.Equals(reviewState, "changes_requested", StringComparison.OrdinalIgnoreCase)
                ? ProjectTaskStatus.NeedsChanges
                : (ProjectTaskStatus?)null;
        return new ParsedAutomation(
            evidence is null ? [] : [evidence],
            codes,
            targetStatus);
    }

    private static EvidenceCandidate? CreatePullRequestEvidence(
        JsonElement pullRequest,
        string repositoryFullName,
        int? number,
        IReadOnlySet<string> codes)
    {
        if (!number.HasValue || number <= 0 || codes.Count == 0)
        {
            return null;
        }

        var url = GetString(pullRequest, "html_url");
        if (string.IsNullOrWhiteSpace(url))
        {
            url = $"https://github.com/{repositoryFullName}/pull/{number.Value}";
        }

        return new EvidenceCandidate(
            TaskEvidenceType.PullRequest,
            url,
            TruncateNote($"GitHub PR #{number.Value}: {GetString(pullRequest, "title")}"),
            $"pull_request:{number.Value}",
            codes);
    }

    private static HashSet<string> ExtractTaskCodes(params string?[] values)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            foreach (Match match in TaskCodePattern().Matches(value!))
            {
                try
                {
                    codes.Add(TaskCode.Parse(match.Value).Value);
                }
                catch (ArgumentException)
                {
                    // A malformed reference is ignored without failing the delivery.
                }
            }
        }

        return codes;
    }

    private static bool TryGetObject(
        JsonElement parent,
        string propertyName,
        out JsonElement value)
    {
        if (parent.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt32(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;

    private static bool GetBoolean(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) &&
        value.ValueKind is JsonValueKind.True;

    private static DateTimeOffset? GetDateTimeOffset(
        JsonElement parent,
        string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        value.TryGetDateTimeOffset(out var result)
            ? result
            : null;

    private static string? GetRequestedReviewers(JsonElement pullRequest)
    {
        if (!pullRequest.TryGetProperty("requested_reviewers", out var reviewers) ||
            reviewers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var logins = reviewers.EnumerateArray()
            .Select(reviewer => GetString(reviewer, "login"))
            .Where(login => !string.IsNullOrWhiteSpace(login))
            .Select(login => login!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (logins.Length == 0)
        {
            return null;
        }

        var selected = new List<string>();
        var length = 0;
        foreach (var login in logins)
        {
            var nextLength = length + (selected.Count == 0 ? 0 : 1) + login.Length;
            if (nextLength > GitHubPullRequest.RequestedReviewersMaximumLength)
            {
                break;
            }

            selected.Add(login);
            length = nextLength;
        }

        return selected.Count == 0 ? null : string.Join(',', selected);
    }

    private static GitHubPullRequestStatus ResolvePullRequestStatus(
        string eventName,
        string? action,
        JsonElement pullRequest,
        JsonElement root,
        GitHubPullRequestStatus? currentStatus)
    {
        if (eventName == "pull_request_review")
        {
            var reviewState = TryGetObject(root, "review", out var review)
                ? GetString(review, "state")
                : null;
            if (string.Equals(reviewState, "changes_requested", StringComparison.OrdinalIgnoreCase))
            {
                return GitHubPullRequestStatus.ChangesRequested;
            }

            if (string.Equals(reviewState, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return GitHubPullRequestStatus.ReadyToMerge;
            }

            return action == "dismissed"
                ? GitHubPullRequestStatus.WaitingForReview
                : currentStatus ?? GitHubPullRequestStatus.WaitingForReview;
        }

        var draft = GetBoolean(pullRequest, "draft");
        var merged = GetBoolean(pullRequest, "merged");
        return action switch
        {
            "converted_to_draft" => GitHubPullRequestStatus.Draft,
            "opened" or "reopened" or "ready_for_review" when draft =>
                GitHubPullRequestStatus.Draft,
            "opened" or "reopened" or "ready_for_review" or "synchronize" =>
                GitHubPullRequestStatus.WaitingForReview,
            "closed" when merged => GitHubPullRequestStatus.Merged,
            "closed" => GitHubPullRequestStatus.Closed,
            _ => currentStatus ?? GitHubPullRequestStatus.WaitingForReview,
        };
    }

    private static GitHubCheckStatus ResolveCheckStatus(JsonElement checkSuite)
    {
        var status = GetString(checkSuite, "status");
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return GitHubCheckStatus.Pending;
        }

        var conclusion = GetString(checkSuite, "conclusion");
        return conclusion is "success" or "neutral" or "skipped"
            ? GitHubCheckStatus.Passing
            : GitHubCheckStatus.Failing;
    }

    private static string FirstLine(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "No commit message"
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();

    private static string TruncateNote(string value) =>
        value.Length <= TaskEvidence.NoteMaximumLength
            ? value
            : value[..TaskEvidence.NoteMaximumLength];

    private static string EvidenceKey(Guid taskId, string externalId) =>
        $"{taskId:N}:{externalId}";

    [GeneratedRegex(
        "(?<![A-Z0-9])([A-Z][A-Z0-9]{1,9}-[0-9]{1,6})(?![A-Z0-9-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TaskCodePattern();

    [GeneratedRegex("^[0-9a-f]{7,64}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CommitShaPattern();

    [GeneratedRegex(
        "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryNamePattern();

    private sealed record ProjectMapping(
        Guid WorkspaceId,
        Guid ProjectId,
        string RepositoryFullName);

    private sealed record EvidenceCandidate(
        TaskEvidenceType Type,
        string Url,
        string Note,
        string ExternalId,
        IReadOnlySet<string> TaskCodes);

    private sealed record ParsedAutomation(
        IReadOnlyList<EvidenceCandidate> Evidence,
        IReadOnlySet<string> TaskCodes,
        ProjectTaskStatus? TargetStatus)
    {
        public static ParsedAutomation Empty { get; } = new(
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            null);
    }
}
