namespace TeamPulse.Infrastructure.Integrations.GitHub;

public sealed class GitHubWebhookOptions
{
    public const string SectionName = "Integrations:GitHub:Webhook";
    public const int MinimumSecretBytes = 32;

    public bool Enabled { get; init; }

    public string Secret { get; init; } = string.Empty;

    public int MaximumPayloadBytes { get; init; } = 2 * 1024 * 1024;
}
