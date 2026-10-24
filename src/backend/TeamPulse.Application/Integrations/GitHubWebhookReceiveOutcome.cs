namespace TeamPulse.Application.Integrations;

public enum GitHubWebhookReceiveOutcome
{
    Accepted,
    Duplicate,
    Ignored,
    InvalidSignature,
    InvalidRequest,
    Disabled,
    PayloadTooLarge,
}
