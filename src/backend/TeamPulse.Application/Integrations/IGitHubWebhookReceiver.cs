namespace TeamPulse.Application.Integrations;

public interface IGitHubWebhookReceiver
{
    Task<GitHubWebhookReceiveResult> ReceiveAsync(
        GitHubWebhookRequest request,
        CancellationToken cancellationToken = default);
}
