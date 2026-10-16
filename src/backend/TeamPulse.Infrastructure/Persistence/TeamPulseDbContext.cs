using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Application.Common.Interfaces;
using TeamPulse.Domain.Common;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Users;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Identity;

namespace TeamPulse.Infrastructure.Persistence;

public sealed class TeamPulseDbContext(
    DbContextOptions<TeamPulseDbContext> options,
    IWorkspaceScope workspaceScope,
    TimeProvider timeProvider)
    : IdentityUserContext<IdentityAccount, Guid>(options)
{
    private readonly IWorkspaceScope _workspaceScope = workspaceScope;
    private readonly TimeProvider _timeProvider = timeProvider;

    public DbSet<User> UserProfiles => Set<User>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Sprint> Sprints => Set<Sprint>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeamPulseDbContext).Assembly);

        modelBuilder.Entity<Workspace>()
            .HasQueryFilter(workspace =>
                _workspaceScope.WorkspaceId.HasValue &&
                workspace.Id == _workspaceScope.WorkspaceId.Value);

        modelBuilder.Entity<WorkspaceMember>()
            .HasQueryFilter(member =>
                _workspaceScope.WorkspaceId.HasValue &&
                member.WorkspaceId == _workspaceScope.WorkspaceId.Value);

        modelBuilder.Entity<Project>()
            .HasQueryFilter(project =>
                _workspaceScope.WorkspaceId.HasValue &&
                project.WorkspaceId == _workspaceScope.WorkspaceId.Value);

        modelBuilder.Entity<Sprint>()
            .HasQueryFilter(sprint =>
                _workspaceScope.WorkspaceId.HasValue &&
                sprint.WorkspaceId == _workspaceScope.WorkspaceId.Value);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(entity => entity.CreatedAtUtc).CurrentValue = now;
                entry.Property(entity => entity.UpdatedAtUtc).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAtUtc).IsModified = false;
                entry.Property(entity => entity.UpdatedAtUtc).CurrentValue = now;
            }
        }
    }
}
