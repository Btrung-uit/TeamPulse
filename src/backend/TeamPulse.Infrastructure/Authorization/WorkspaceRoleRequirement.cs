using Microsoft.AspNetCore.Authorization;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.Infrastructure.Authorization;

internal sealed record WorkspaceRoleRequirement(
    IReadOnlySet<WorkspaceRole> AllowedRoles) : IAuthorizationRequirement;
