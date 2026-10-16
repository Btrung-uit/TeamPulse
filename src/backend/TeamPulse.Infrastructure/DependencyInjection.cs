using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TeamPulse.Application.Authentication;
using TeamPulse.Application.Common.Interfaces;
using TeamPulse.Infrastructure.Identity;
using TeamPulse.Infrastructure.Multitenancy;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure;

public static class DependencyInjection
{
    private const string TeamPulseConnectionString = "TeamPulse";
    private const int MinimumSigningKeyBytes = 32;

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

        services.AddAuthorization();
        services.AddScoped<TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
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
