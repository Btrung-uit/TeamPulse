using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.IntegrationTests;

public sealed class TeamPulseApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TeamPulseTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:TeamPulse",
            "Server=unused;Database=unused;User Id=unused;Password=unused");
        builder.UseSetting("Authentication:Jwt:Issuer", "TeamPulse.Tests");
        builder.UseSetting("Authentication:Jwt:Audience", "TeamPulse.TestClient");
        builder.UseSetting(
            "Authentication:Jwt:SigningKey",
            "TeamPulse_integration_test_signing_key_2026_at_least_32_bytes");
        builder.UseSetting("Authentication:Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Authentication:Jwt:RefreshTokenDays", "7");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<TeamPulseDbContext>>();
            services.RemoveAll<DbContextOptions<TeamPulseDbContext>>();
            services.AddDbContext<TeamPulseDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
