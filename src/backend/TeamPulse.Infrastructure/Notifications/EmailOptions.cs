namespace TeamPulse.Infrastructure.Notifications;

internal sealed class EmailOptions
{
    public const string SectionName = "Email";
    public bool Enabled { get; init; }
    public string PublicAppUrl { get; init; } = string.Empty;
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string SmtpUsername { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public string SenderAddress { get; init; } = string.Empty;
    public string SenderName { get; init; } = "TeamPulse";
    public bool EnableSsl { get; init; } = true;
}
