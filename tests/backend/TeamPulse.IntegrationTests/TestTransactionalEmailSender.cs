using System.Collections.Concurrent;
using TeamPulse.Application.Notifications;

namespace TeamPulse.IntegrationTests;

public sealed class TestTransactionalEmailSender : ITransactionalEmailSender
{
    private readonly ConcurrentQueue<TestEmail> _emails = new();
    private readonly ConcurrentDictionary<string, byte> _failingRecipients = new();

    public IReadOnlyCollection<TestEmail> Emails => _emails.ToArray();

    public void FailFor(string recipient) => _failingRecipients[recipient] = 0;

    public Task SendPasswordResetAsync(
        string recipient,
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (_failingRecipients.ContainsKey(recipient))
        {
            throw new InvalidOperationException("Simulated email delivery failure.");
        }

        _emails.Enqueue(new TestEmail("password-reset", recipient, token, userId));
        return Task.CompletedTask;
    }

    public Task SendWorkspaceInvitationAsync(
        string recipient,
        string workspaceName,
        string token,
        CancellationToken cancellationToken = default)
    {
        _emails.Enqueue(new TestEmail("workspace-invitation", recipient, token));
        return Task.CompletedTask;
    }
}

public sealed record TestEmail(string Kind, string Recipient, string Token, Guid? UserId = null);
