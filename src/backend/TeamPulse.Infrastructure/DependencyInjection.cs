using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure;

public static class DependencyInjection
{
    private const string TeamPulseConnectionString = "TeamPulse";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(TeamPulseConnectionString);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{TeamPulseConnectionString}' is required. " +
                "Configure it with .NET User Secrets or an environment variable.");
        }

        services.AddDbContext<TeamPulseDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }
}
