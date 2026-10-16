using TeamPulse.Domain.Projects;
using TeamPulse.Domain.Workspaces;

namespace TeamPulse.UnitTests;

public sealed class FoundationDomainTests
{
    [Fact]
    public void Project_RequiresWorkspace()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Project(
                Guid.NewGuid(),
                Guid.Empty,
                Guid.NewGuid(),
                "Project without tenant"));

        Assert.Equal("workspaceId", exception.ParamName);
    }

    [Fact]
    public void Sprint_RejectsEndDateBeforeStartDate()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Sprint(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Invalid sprint",
                new DateOnly(2026, 10, 20),
                new DateOnly(2026, 10, 19)));

        Assert.Equal("endsOn", exception.ParamName);
    }

    [Fact]
    public void WorkspaceMember_RejectsUnknownRole()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceMember(
                Guid.NewGuid(),
                Guid.NewGuid(),
                (WorkspaceRole)999));
    }
}
