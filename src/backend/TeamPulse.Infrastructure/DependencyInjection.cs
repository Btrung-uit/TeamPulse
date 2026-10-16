using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeamPulse.Application.Common.Interfaces;
using TeamPulse.Infrastructure.Multitenancy;
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

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<WorkspaceScope>();
        services.AddScoped<IWorkspaceScope>(provider =>
            provider.GetRequiredService<WorkspaceScope>());

        services.AddDbContext<TeamPulseDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }
}
