namespace TeamPulse.Application.Integrations;

public sealed record GitHubWebhookReceiveResult(
    GitHubWebhookReceiveOutcome Outcome,
    string Message);
