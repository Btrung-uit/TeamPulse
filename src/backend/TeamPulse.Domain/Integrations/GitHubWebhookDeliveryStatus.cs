namespace TeamPulse.Domain.Integrations;

public enum GitHubWebhookDeliveryStatus
{
    Received = 1,
    Processing = 2,
    Processed = 3,
    Ignored = 4,
    Failed = 5,
}
