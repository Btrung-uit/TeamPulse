using TeamPulse.Domain.Workspaces;

namespace TeamPulse.UnitTests;

public sealed class WorkspaceInvitationTests
{
    [Fact]
    public void Invitation_CannotBeAcceptedAfterExpiryOrAcceptance()
    {
        var now = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
        var invitation = new WorkspaceInvitation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "member@example.com",
            WorkspaceRole.Member,
            new string('A', WorkspaceInvitation.TokenHashMaximumLength),
            now.AddMinutes(1));

        Assert.True(invitation.CanBeAcceptedAt(now));
        Assert.False(invitation.CanBeAcceptedAt(now.AddMinutes(1)));

        invitation.Accept(now);

        Assert.False(invitation.CanBeAcceptedAt(now));
    }
}
