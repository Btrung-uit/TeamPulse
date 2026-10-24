using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TeamPulse.Application.Authentication;
using TeamPulse.Application.Common.Interfaces;
using TeamPulse.Application.Dashboard;
using TeamPulse.Application.Integrations;
using TeamPulse.Application.Notifications;
using TeamPulse.Application.Projects;
using TeamPulse.Application.Resources;
using TeamPulse.Application.Search;
using TeamPulse.Application.Tasks;
using TeamPulse.Application.Planning;
using TeamPulse.Application.Workspaces;
using TeamPulse.Domain.Workspaces;
using TeamPulse.Infrastructure.Authorization;
using TeamPulse.Infrastructure.Dashboard;
using TeamPulse.Infrastructure.Identity;
using TeamPulse.Infrastructure.Integrations.GitHub;
using TeamPulse.Infrastructure.Multitenancy;
using TeamPulse.Infrastructure.Notifications;
using TeamPulse.Infrastructure.Persistence;
using TeamPulse.Infrastructure.Planning;
using TeamPulse.Infrastructure.Projects;
using TeamPulse.Infrastructure.Resources;
using TeamPulse.Infrastructure.Search;
using TeamPulse.Infrastructure.Tasks;
using TeamPulse.Infrastructure.Workspaces;

namespace TeamPulse.Infrastructure;

public static class DependencyInjection
{
    private const string TeamPulseConnectionString = "TeamPulse";
    private const int MinimumSigningKeyBytes = 32;

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<WorkspaceScope>();
        services.AddScoped<IWorkspaceScope>(provider =>
            provider.GetRequiredService<WorkspaceScope>());

        AddDatabase(services, configuration);

        var jwtSection = configuration.GetRequiredSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");

        ValidateJwtOptions(jwtOptions);
        services.Configure<JwtOptions>(jwtSection);

        services
            .AddIdentityCore<IdentityAccount>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<TeamPulseDbContext>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                WorkspacePolicies.Member,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new WorkspaceRoleRequirement(
                        new HashSet<WorkspaceRole>())));
            options.AddPolicy(
                WorkspacePolicies.Leader,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new WorkspaceRoleRequirement(
                        new HashSet<WorkspaceRole> { WorkspaceRole.Leader })));
        });
        services.AddScoped<IAuthorizationHandler, WorkspaceAuthorizationHandler>();
        services.AddScoped<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IPlanningService, PlanningService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ITaskSpreadsheetService, TaskSpreadsheetService>();
        services.AddScoped<ITaskImportService, TaskImportService>();
        services.AddScoped<ITaskCollaborationService, TaskCollaborationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IWorkspaceSearchService, WorkspaceSearchService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IGitHubWebhookReceiver, GitHubWebhookReceiver>();
        services.AddScoped<IGitHubWebhookEventHandler, GitHubTaskAutomationHandler>();
        services.AddScoped<IGitHubPullRequestService, GitHubPullRequestService>();
        services.AddHostedService<GitHubWebhookWorker>();

        services
            .AddOptions<GitHubWebhookOptions>()
            .Bind(configuration.GetSection(GitHubWebhookOptions.SectionName))
            .Validate(
                options => options.MaximumPayloadBytes is >= 1024 and <= 25 * 1024 * 1024,
                "GitHub webhook maximum payload size must be between 1 KB and 25 MB.")
            .Validate(
                options => !options.Enabled ||
                    Encoding.UTF8.GetByteCount(options.Secret) >= GitHubWebhookOptions.MinimumSecretBytes,
                $"Enabled GitHub webhooks require a secret of at least {GitHubWebhookOptions.MinimumSecretBytes} UTF-8 bytes.")
            .ValidateOnStart();

        return services;
    }

    private static void AddDatabase(
        IServiceCollection services,
        IConfiguration configuration)
    {
        if (string.Equals(
                configuration["Database:Provider"],
                "InMemory",
                StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = configuration["Database:Name"] ?? "TeamPulse";
            services.AddDbContext<TeamPulseDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            return;
        }

        var connectionString = configuration.GetConnectionString(TeamPulseConnectionString);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{TeamPulseConnectionString}' is required. " +
                "Configure it with .NET User Secrets or an environment variable.");
        }

        services.AddDbContext<TeamPulseDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 6,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));
    }

    private static void ValidateJwtOptions(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("JWT issuer and audience are required.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"JWT signing key must contain at least {MinimumSigningKeyBytes} UTF-8 bytes. " +
                "Configure it with .NET User Secrets or an environment variable.");
        }

        if (options.AccessTokenMinutes is < 5 or > 60)
        {
            throw new InvalidOperationException("JWT access token lifetime must be between 5 and 60 minutes.");
        }

        if (options.RefreshTokenDays is < 1 or > 90)
        {
            throw new InvalidOperationException("Refresh token lifetime must be between 1 and 90 days.");
        }
    }
}
