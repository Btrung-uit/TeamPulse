using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using TeamPulse.Application.Notifications;

namespace TeamPulse.Infrastructure.Notifications;

internal sealed class SmtpTransactionalEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpTransactionalEmailSender> logger) : ITransactionalEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public Task SendPasswordResetAsync(
        string recipient,
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(
            "reset-password",
            $"userId={Uri.EscapeDataString(userId.ToString())}&token={Uri.EscapeDataString(token)}");
        return SendAsync(
            recipient,
            "Reset your TeamPulse password",
            $"Reset your password within 30 minutes: {url}",
            cancellationToken);
    }

    public Task SendWorkspaceInvitationAsync(
        string recipient,
        string workspaceName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(
            "workspace-invitations/accept",
            $"token={Uri.EscapeDataString(token)}");
        return SendAsync(
            recipient,
            $"Join {workspaceName} on TeamPulse",
            $"You were invited to {workspaceName}. Accept the invitation within 7 days: {url}",
            cancellationToken);
    }

    private string BuildUrl(string path, string query) =>
        $"{_options.PublicAppUrl.TrimEnd('/')}/{path}?{query}";

    private async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Transactional email delivery is disabled; skipped {Subject}.", subject);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.SenderAddress, _options.SenderName),
            Subject = subject,
            Body = body,
        };
        message.To.Add(recipient);

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword),
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
