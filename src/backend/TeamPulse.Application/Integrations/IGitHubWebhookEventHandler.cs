namespace TeamPulse.Application.Integrations;

public interface IGitHubWebhookEventHandler
{
    Task HandleAsync(
        GitHubWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);
}
