using TeamPulse.Domain.Common;

namespace TeamPulse.Domain.Integrations;

public sealed class GitHubWebhookDelivery : AuditableEntity
{
    public const int DeliveryIdMaximumLength = 128;
    public const int EventNameMaximumLength = 64;
    public const int ActionMaximumLength = 64;
    public const int RepositoryNameMaximumLength = 256;
    public const int PayloadHashMaximumLength = 64;
    public const int ErrorMaximumLength = 1000;

    private GitHubWebhookDelivery()
    {
    }

    public GitHubWebhookDelivery(
        Guid id,
        string deliveryId,
        string eventName,
        string? action,
        long? repositoryId,
        string? repositoryFullName,
        string payload,
        string payloadSha256,
        bool shouldProcess,
        string? ignoredReason = null)
        : base(id)
    {
        DeliveryId = DomainText.Required(
            deliveryId,
            nameof(deliveryId),
            DeliveryIdMaximumLength);
        EventName = DomainText.Required(
            eventName,
            nameof(eventName),
            EventNameMaximumLength);
        Action = DomainText.Optional(action, nameof(action), ActionMaximumLength);
        RepositoryId = repositoryId;
        RepositoryFullName = DomainText.Optional(
            repositoryFullName,
            nameof(repositoryFullName),
            RepositoryNameMaximumLength);
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("GitHub webhook payload is required.", nameof(payload));
        }

        Payload = payload;
        PayloadSha256 = DomainText.Required(
            payloadSha256,
            nameof(payloadSha256),
            PayloadHashMaximumLength);

        if (shouldProcess)
        {
            Status = GitHubWebhookDeliveryStatus.Received;
        }
        else
        {
            Status = GitHubWebhookDeliveryStatus.Ignored;
            Error = DomainText.Optional(ignoredReason, nameof(ignoredReason), ErrorMaximumLength);
        }
    }

    public string DeliveryId { get; private set; } = string.Empty;

    public string EventName { get; private set; } = string.Empty;

    public string? Action { get; private set; }

    public long? RepositoryId { get; private set; }

    public string? RepositoryFullName { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public string PayloadSha256 { get; private set; } = string.Empty;

    public GitHubWebhookDeliveryStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LastAttemptedAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public string? Error { get; private set; }

    public void MarkProcessing(DateTimeOffset attemptedAtUtc)
    {
        if (Status is not GitHubWebhookDeliveryStatus.Received and
            not GitHubWebhookDeliveryStatus.Processing)
        {
            throw new InvalidOperationException("Only pending GitHub deliveries can be processed.");
        }

        Status = GitHubWebhookDeliveryStatus.Processing;
        AttemptCount++;
        LastAttemptedAtUtc = attemptedAtUtc;
        Error = null;
    }

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        if (Status != GitHubWebhookDeliveryStatus.Processing)
        {
            throw new InvalidOperationException("GitHub delivery must be processing before completion.");
        }

        Status = GitHubWebhookDeliveryStatus.Processed;
        ProcessedAtUtc = processedAtUtc;
        Error = null;
    }

    public void MarkProcessingFailure(string error, int maximumAttempts)
    {
        if (Status != GitHubWebhookDeliveryStatus.Processing)
        {
            throw new InvalidOperationException("GitHub delivery must be processing before failure.");
        }

        Error = DomainText.Required(error, nameof(error), ErrorMaximumLength);
        Status = AttemptCount >= maximumAttempts
            ? GitHubWebhookDeliveryStatus.Failed
            : GitHubWebhookDeliveryStatus.Received;
    }
}
