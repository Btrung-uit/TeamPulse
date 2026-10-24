using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamPulse.Application.Integrations;
using TeamPulse.Domain.Activities;
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

        var automation = ParseAutomation(webhookEvent);
        if (automation is null || automation.TaskCodes.Count == 0)
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
            await ApplyToProjectAsync(
                mapping,
                automation,
                webhookEvent.DeliveryId,
                cancellationToken);
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
