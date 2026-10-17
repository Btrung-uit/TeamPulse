using TeamPulse.Domain.Tasks;

namespace TeamPulse.UnitTests;

public sealed class TaskLifecycleTests
{
    [Theory]
    [InlineData("tp-001", "TP-001")]
    [InlineData(" TEAM2-42 ", "TEAM2-42")]
    public void TaskCode_NormalizesValidCode(string input, string expected)
    {
        var code = TaskCode.Parse(input);

        Assert.Equal(expected, code.Value);
        Assert.Equal(expected, code.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("TP")]
    [InlineData("TP-")]
    [InlineData("1TP-123")]
    [InlineData("TP-ABC")]
    public void TaskCode_RejectsInvalidCode(string input)
    {
        Assert.Throws<ArgumentException>(() => TaskCode.Parse(input));
    }

    [Fact]
    public void NewTask_StartsAsTodoWithMediumPriority()
    {
        var task = CreateTask();

        Assert.Equal(ProjectTaskStatus.Todo, task.Status);
        Assert.Equal(TaskPriority.Medium, task.Priority);
        Assert.False(task.HasStartedWorkflow);
        Assert.False(task.IsArchived);
    }

    [Fact]
    public void MainLifecycle_ReachesDoneOnlyThroughReview()
    {
        var task = CreateTask();

        task.TransitionTo(ProjectTaskStatus.InProgress);
        task.TransitionTo(ProjectTaskStatus.EvidenceLinked);
        task.TransitionTo(ProjectTaskStatus.InReview);
        task.TransitionTo(ProjectTaskStatus.Done);

        Assert.Equal(ProjectTaskStatus.Done, task.Status);
        Assert.True(task.HasStartedWorkflow);
    }

    [Theory]
    [InlineData(ProjectTaskStatus.EvidenceLinked)]
    [InlineData(ProjectTaskStatus.InReview)]
    [InlineData(ProjectTaskStatus.NeedsChanges)]
    [InlineData(ProjectTaskStatus.Done)]
    public void Todo_RejectsSkippedWorkflowTransitions(ProjectTaskStatus targetStatus)
    {
        var task = CreateTask();

        Assert.Throws<InvalidOperationException>(() => task.TransitionTo(targetStatus));
        Assert.Equal(ProjectTaskStatus.Todo, task.Status);
        Assert.False(task.HasStartedWorkflow);
    }

    [Fact]
    public void EvidenceLinked_CannotTransitionDirectlyToDone()
    {
        var task = CreateTask();
        task.TransitionTo(ProjectTaskStatus.InProgress);
        task.TransitionTo(ProjectTaskStatus.EvidenceLinked);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            task.TransitionTo(ProjectTaskStatus.Done));

        Assert.Contains("EvidenceLinked to Done", exception.Message, StringComparison.Ordinal);
        Assert.Equal(ProjectTaskStatus.EvidenceLinked, task.Status);
    }

    [Fact]
    public void BlockedTask_ReturnsOnlyToItsPreviousStatus()
    {
        var task = CreateTask();
        task.TransitionTo(ProjectTaskStatus.InProgress);
        task.TransitionTo(ProjectTaskStatus.EvidenceLinked);
        task.TransitionTo(ProjectTaskStatus.Blocked);

        Assert.Equal(ProjectTaskStatus.EvidenceLinked, task.StatusBeforeBlocked);
        Assert.Throws<InvalidOperationException>(() =>
            task.TransitionTo(ProjectTaskStatus.InProgress));

        task.TransitionTo(ProjectTaskStatus.EvidenceLinked);

        Assert.Equal(ProjectTaskStatus.EvidenceLinked, task.Status);
        Assert.Null(task.StatusBeforeBlocked);
    }

    [Fact]
    public void ReviewCanRequestChangesAndReturnToReview()
    {
        var task = CreateTask();
        task.TransitionTo(ProjectTaskStatus.InProgress);
        task.TransitionTo(ProjectTaskStatus.EvidenceLinked);
        task.TransitionTo(ProjectTaskStatus.InReview);
        task.TransitionTo(ProjectTaskStatus.NeedsChanges);
        task.TransitionTo(ProjectTaskStatus.InProgress);
        task.TransitionTo(ProjectTaskStatus.EvidenceLinked);
        task.TransitionTo(ProjectTaskStatus.InReview);

        Assert.Equal(ProjectTaskStatus.InReview, task.Status);
    }

    [Fact]
    public void DoneTask_IsTerminal()
    {
        var task = CreateTaskInReview();
        task.TransitionTo(ProjectTaskStatus.Done);

        Assert.Throws<InvalidOperationException>(() =>
            task.TransitionTo(ProjectTaskStatus.InProgress));
        Assert.Throws<InvalidOperationException>(() =>
            task.TransitionTo(ProjectTaskStatus.Blocked));
    }

    [Fact]
    public void ArchivedTask_CannotBeChanged()
    {
        var task = CreateTask();
        var archivedAtUtc = new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

        task.Archive(archivedAtUtc);
        task.Archive(archivedAtUtc.AddHours(1));

        Assert.True(task.IsArchived);
        Assert.Equal(archivedAtUtc, task.ArchivedAtUtc);
        Assert.Throws<InvalidOperationException>(() => task.UpdateDetails("Changed", null));
        Assert.Throws<InvalidOperationException>(() =>
            task.TransitionTo(ProjectTaskStatus.InProgress));
    }

    [Fact]
    public void HardDelete_IsAllowedOnlyForUnusedDraftWithoutHistory()
    {
        var unusedDraft = CreateTask();
        var taskWithHistory = CreateTask();
        var taskThatEnteredWorkflow = CreateTask();

        taskThatEnteredWorkflow.TransitionTo(ProjectTaskStatus.InProgress);
        taskThatEnteredWorkflow.TransitionTo(ProjectTaskStatus.Todo);

        Assert.True(unusedDraft.CanBeHardDeleted(hasRelatedHistory: false));
        Assert.False(taskWithHistory.CanBeHardDeleted(hasRelatedHistory: true));
        Assert.False(taskThatEnteredWorkflow.CanBeHardDeleted(hasRelatedHistory: false));
    }

    [Fact]
    public void TaskDependency_RejectsSelfReference()
    {
        var taskId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() =>
            new TaskDependency(Guid.NewGuid(), Guid.NewGuid(), taskId, taskId));

        Assert.Equal("successorTaskId", exception.ParamName);
    }

    private static ProjectTask CreateTask()
    {
        return new ProjectTask(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TaskCode.Parse("TP-001"),
            "Implement task lifecycle");
    }

    private static ProjectTask CreateTaskInReview()
    {
        var task = CreateTask();
        task.TransitionTo(ProjectTaskStatus.InProgress);
        task.TransitionTo(ProjectTaskStatus.EvidenceLinked);
        task.TransitionTo(ProjectTaskStatus.InReview);
        return task;
    }
}
