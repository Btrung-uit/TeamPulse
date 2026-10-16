using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Infrastructure.Multitenancy;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Authorization;

internal sealed class WorkspaceAuthorizationHandler(
    TeamPulseDbContext dbContext,
    WorkspaceScope workspaceScope)
    : AuthorizationHandler<WorkspaceRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WorkspaceRoleRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext ||
            !Guid.TryParse(
                context.User.FindFirstValue(JwtRegisteredClaimNames.Sub),
                out var userId) ||
            !Guid.TryParse(
                httpContext.Request.RouteValues["workspaceId"]?.ToString(),
                out var workspaceId))
        {
            return;
        }

        var membership = await dbContext.WorkspaceMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.WorkspaceId == workspaceId &&
                candidate.UserId == userId &&
                candidate.IsActive,
                httpContext.RequestAborted);

        if (membership is null ||
            (requirement.AllowedRoles.Count > 0 &&
             !requirement.AllowedRoles.Contains(membership.Role)))
        {
            return;
        }

        workspaceScope.SetWorkspace(workspaceId);
        context.Succeed(requirement);
    }
}
