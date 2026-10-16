using TeamPulse.Application.Common.Interfaces;

namespace TeamPulse.Infrastructure.Multitenancy;

public sealed class WorkspaceScope : IWorkspaceScope
{
    public Guid? WorkspaceId { get; private set; }

    public void SetWorkspace(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace is required.", nameof(workspaceId));
        }

        if (WorkspaceId.HasValue && WorkspaceId.Value != workspaceId)
        {
            throw new InvalidOperationException(
                "The workspace scope cannot be changed during the same request.");
        }

        WorkspaceId = workspaceId;
    }
}
