namespace TeamPulse.UnitTests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void DomainAndApplicationAssemblies_AreAvailable()
    {
        Assert.Equal("TeamPulse.Domain", typeof(global::TeamPulse.Domain.AssemblyReference).Assembly.GetName().Name);
        Assert.Equal("TeamPulse.Application", typeof(global::TeamPulse.Application.AssemblyReference).Assembly.GetName().Name);
    }
}
