namespace TeamPulse.Application.Notifications;

public interface ITransactionalEmailSender
{
    Task SendPasswordResetAsync(
        string recipient,
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task SendWorkspaceInvitationAsync(
        string recipient,
        string workspaceName,
        string token,
        CancellationToken cancellationToken = default);
}
