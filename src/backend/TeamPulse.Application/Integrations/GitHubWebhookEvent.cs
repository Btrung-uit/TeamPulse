namespace TeamPulse.Application.Integrations;

public sealed record GitHubWebhookEvent(
    Guid DeliveryRecordId,
    string DeliveryId,
    string EventName,
    string? Action,
    long? RepositoryId,
    string? RepositoryFullName,
    string Payload);
