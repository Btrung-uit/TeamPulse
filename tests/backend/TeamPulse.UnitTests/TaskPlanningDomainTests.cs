using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Tasks;

namespace TeamPulse.UnitTests;

public sealed class TaskPlanningDomainTests
{
    [Fact]
    public void SprintUpdate_ChangesDetailsWhenDateRangeIsValid()
    {
        var sprint = CreateSprint();

        sprint.UpdateDetails(
            "Sprint 2",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 14),
            "Ship task planning");

        Assert.Equal("Sprint 2", sprint.Name);
        Assert.Equal("Ship task planning", sprint.Goal);
        Assert.Equal(new DateOnly(2026, 11, 1), sprint.StartsOn);
        Assert.Equal(new DateOnly(2026, 11, 14), sprint.EndsOn);
    }

    [Fact]
    public void SprintUpdate_RejectsInvalidRangeWithoutPartiallyUpdating()
    {
        var sprint = CreateSprint();

        Assert.Throws<ArgumentException>(() => sprint.UpdateDetails(
            "Invalid update",
            new DateOnly(2026, 11, 14),
            new DateOnly(2026, 11, 1)));

        Assert.Equal("Sprint 1", sprint.Name);
        Assert.Equal(new DateOnly(2026, 10, 17), sprint.StartsOn);
    }

    [Fact]
    public void DependencyGraph_DetectsIndirectCycle()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        TaskDependencyLink[] links =
        [
            new(first, second),
            new(second, third),
        ];

        Assert.True(TaskDependencyGraph.WouldCreateCycle(links, third, first));
        Assert.False(TaskDependencyGraph.WouldCreateCycle(links, first, third));
        Assert.True(TaskDependencyGraph.WouldCreateCycle(links, first, first));
    }

    [Fact]
    public void RiskEvaluator_ReportsIncompleteOverdueAndDeadlineConflict_WithoutMovingDates()
    {
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var predecessor = CreateTask(workspaceId, projectId, "TP-101");
        var successor = CreateTask(workspaceId, projectId, "TP-102");
        var predecessorDueOn = new DateOnly(2026, 10, 20);
        var successorDueOn = new DateOnly(2026, 10, 18);
        predecessor.Schedule(predecessorDueOn, 4);
        successor.Schedule(successorDueOn, 6);

        var risks = TaskDependencyRiskEvaluator.Evaluate(
            predecessor,
            successor,
            new DateOnly(2026, 10, 21));

        Assert.Equal(
            [
                DependencyRiskKind.PredecessorIncomplete,
                DependencyRiskKind.PredecessorOverdue,
                DependencyRiskKind.DeadlineConflict,
            ],
            risks);
        Assert.Equal(predecessorDueOn, predecessor.DueOn);
        Assert.Equal(successorDueOn, successor.DueOn);
    }

    [Fact]
    public void RiskEvaluator_DoesNotWarnAboutCompletedPredecessor()
    {
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var predecessor = CreateTask(workspaceId, projectId, "TP-201");
        var successor = CreateTask(workspaceId, projectId, "TP-202");
        predecessor.Schedule(new DateOnly(2026, 10, 15), 4);
        successor.Schedule(new DateOnly(2026, 10, 20), 4);
        MoveToDone(predecessor);

        var risks = TaskDependencyRiskEvaluator.Evaluate(
            predecessor,
            successor,
            new DateOnly(2026, 10, 21));

        Assert.Empty(risks);
    }

    private static Sprint CreateSprint()
    {
        return new Sprint(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Sprint 1",
            new DateOnly(2026, 10, 17),
            new DateOnly(2026, 10, 31));
    }

    private static ProjectTask CreateTask(Guid workspaceId, Guid projectId, string code)
    {
        return new ProjectTask(
            Guid.NewGuid(),
            workspaceId,
            projectId,
            Guid.NewGuid(),
            TaskCode.Parse(code),
            $"Task {code}");
    }

    private static void MoveToDone(ProjectTask task)
    {
        task.TransitionTo(ProjectTaskStatus.InProgress);
        task.TransitionTo(ProjectTaskStatus.EvidenceLinked);
        task.TransitionTo(ProjectTaskStatus.InReview);
        task.TransitionTo(ProjectTaskStatus.Done);
    }
}
