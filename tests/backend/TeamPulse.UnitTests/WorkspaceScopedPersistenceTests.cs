using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Tasks;
using TeamPulse.Domain.Users;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Multitenancy;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.UnitTests;

public sealed class WorkspaceScopedPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 10, 16, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task WorkManagementQueries_AreLimitedToCurrentWorkspace()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = CreateOptions(databaseRoot);
        var firstWorkspaceId = Guid.NewGuid();
        var secondWorkspaceId = Guid.NewGuid();

        await using (var seedContext = CreateContext(options, firstWorkspaceId))
        {
            AddWorkspaceGraph(seedContext, firstWorkspaceId, "Alpha");
            AddWorkspaceGraph(seedContext, secondWorkspaceId, "Beta");
            await seedContext.SaveChangesAsync();
        }

        await using (var firstContext = CreateContext(options, firstWorkspaceId))
        {
            var workspaces = await firstContext.Workspaces.ToListAsync();
            var members = await firstContext.WorkspaceMembers.ToListAsync();
            var projects = await firstContext.Projects.ToListAsync();
            var sprints = await firstContext.Sprints.ToListAsync();
            var tasks = await firstContext.Tasks.ToListAsync();
            var dependencies = await firstContext.TaskDependencies.ToListAsync();

            Assert.Single(workspaces);
            Assert.Equal(firstWorkspaceId, workspaces[0].Id);
            Assert.Single(members);
            Assert.Equal(firstWorkspaceId, members[0].WorkspaceId);
            Assert.Single(projects);
            Assert.Equal(firstWorkspaceId, projects[0].WorkspaceId);
            Assert.Single(sprints);
            Assert.Equal(firstWorkspaceId, sprints[0].WorkspaceId);
            Assert.Equal(2, tasks.Count);
            Assert.All(tasks, task => Assert.Equal(firstWorkspaceId, task.WorkspaceId));
            Assert.Single(dependencies);
            Assert.Equal(firstWorkspaceId, dependencies[0].WorkspaceId);
        }

        await using (var secondContext = CreateContext(options, secondWorkspaceId))
        {
            var workspaces = await secondContext.Workspaces.ToListAsync();
            var members = await secondContext.WorkspaceMembers.ToListAsync();
            var projects = await secondContext.Projects.ToListAsync();
            var sprints = await secondContext.Sprints.ToListAsync();
            var tasks = await secondContext.Tasks.ToListAsync();
            var dependencies = await secondContext.TaskDependencies.ToListAsync();

            Assert.Single(workspaces);
            Assert.Equal(secondWorkspaceId, workspaces[0].Id);
            Assert.Single(members);
            Assert.Equal(secondWorkspaceId, members[0].WorkspaceId);
            Assert.Single(projects);
            Assert.Equal(secondWorkspaceId, projects[0].WorkspaceId);
            Assert.Single(sprints);
            Assert.Equal(secondWorkspaceId, sprints[0].WorkspaceId);
            Assert.Equal(2, tasks.Count);
            Assert.All(tasks, task => Assert.Equal(secondWorkspaceId, task.WorkspaceId));
            Assert.Single(dependencies);
            Assert.Equal(secondWorkspaceId, dependencies[0].WorkspaceId);
        }
    }

    [Fact]
    public async Task SaveChanges_SetsUtcAuditTimestamps()
    {
        var options = CreateOptions(new InMemoryDatabaseRoot());
        var workspaceId = Guid.NewGuid();

        await using var context = CreateContext(options, workspaceId);
        var project = AddWorkspaceGraph(context, workspaceId, "Audited");

        await context.SaveChangesAsync();

        Assert.Equal(FixedUtcNow, project.CreatedAtUtc);
        Assert.Equal(FixedUtcNow, project.UpdatedAtUtc);
    }

    [Fact]
    public void ProjectAndSprintOwnership_UsesWorkspaceCompositeForeignKeys()
    {
        var options = CreateOptions(new InMemoryDatabaseRoot());
        using var context = CreateContext(options, Guid.NewGuid());

        var projectForeignKeys = context.Model.FindEntityType(typeof(Project))!
            .GetForeignKeys()
            .Select(foreignKey => foreignKey.Properties.Select(property => property.Name).ToArray())
            .ToArray();

        var sprintForeignKeys = context.Model.FindEntityType(typeof(Sprint))!
            .GetForeignKeys()
            .Select(foreignKey => foreignKey.Properties.Select(property => property.Name).ToArray())
            .ToArray();

        Assert.Contains(
            projectForeignKeys,
            properties => properties.SequenceEqual(["WorkspaceId", "CreatedByUserId"]));
        Assert.Contains(
            sprintForeignKeys,
            properties => properties.SequenceEqual(["WorkspaceId", "ProjectId"]));
        Assert.Contains(
            sprintForeignKeys,
            properties => properties.SequenceEqual(["WorkspaceId", "CreatedByUserId"]));
    }

    [Fact]
    public void TaskOwnershipAndConcurrency_AreConfiguredInTheEfModel()
    {
        var options = CreateOptions(new InMemoryDatabaseRoot());
        using var context = CreateContext(options, Guid.NewGuid());

        var taskType = context.Model.FindEntityType(typeof(ProjectTask))!;
        var foreignKeys = taskType.GetForeignKeys()
            .Select(foreignKey => foreignKey.Properties.Select(property => property.Name).ToArray())
            .ToArray();
        var codeIndex = taskType.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["WorkspaceId", "ProjectId", "Code"]));
        var rowVersion = taskType.FindProperty(nameof(ProjectTask.RowVersion))!;

        Assert.Contains(
            foreignKeys,
            properties => properties.SequenceEqual(["WorkspaceId", "ProjectId"]));
        Assert.Contains(
            foreignKeys,
            properties => properties.SequenceEqual(["WorkspaceId", "CreatedByUserId"]));
        Assert.Contains(
            foreignKeys,
            properties => properties.SequenceEqual(["WorkspaceId", "AssigneeUserId"]));
        Assert.True(codeIndex.IsUnique);
        Assert.True(rowVersion.IsConcurrencyToken);
    }

    [Fact]
    public void WorkspaceScope_CannotSwitchTenantWithinRequest()
    {
        var scope = new WorkspaceScope();
        scope.SetWorkspace(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => scope.SetWorkspace(Guid.NewGuid()));
    }

    private static DbContextOptions<TeamPulseDbContext> CreateOptions(
        InMemoryDatabaseRoot databaseRoot)
    {
        return new DbContextOptionsBuilder<TeamPulseDbContext>()
            .UseInMemoryDatabase("TeamPulseFoundationTests", databaseRoot)
            .Options;
    }

    private static TeamPulseDbContext CreateContext(
        DbContextOptions<TeamPulseDbContext> options,
        Guid workspaceId)
    {
        var scope = new WorkspaceScope();
        scope.SetWorkspace(workspaceId);

        return new TeamPulseDbContext(
            options,
            scope,
            new FixedTimeProvider(FixedUtcNow));
    }

    private static Project AddWorkspaceGraph(
        TeamPulseDbContext context,
        Guid workspaceId,
        string suffix)
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var user = new User(userId, $"{suffix.ToLowerInvariant()}@example.com", $"User {suffix}");
        var workspace = new Workspace(workspaceId, $"Workspace {suffix}", userId);
        var membership = new WorkspaceMember(workspaceId, userId, WorkspaceRole.Leader);
        var project = new Project(projectId, workspaceId, userId, $"Project {suffix}");
        var sprint = new Sprint(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            userId,
            $"Sprint {suffix}",
            new DateOnly(2026, 10, 16),
            new DateOnly(2026, 10, 30));
        var predecessor = new ProjectTask(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            userId,
            TaskCode.Parse($"{suffix}-001"),
            $"Predecessor {suffix}");
        var successor = new ProjectTask(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            userId,
            TaskCode.Parse($"{suffix}-002"),
            $"Successor {suffix}");
        var dependency = new TaskDependency(
            workspaceId,
            projectId,
            predecessor.Id,
            successor.Id);

        context.AddRange(
            user,
            workspace,
            membership,
            project,
            sprint,
            predecessor,
            successor,
            dependency);
        return project;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
