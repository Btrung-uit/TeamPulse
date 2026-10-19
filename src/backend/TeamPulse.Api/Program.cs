using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using TeamPulse.Api.Endpoints;
using TeamPulse.Api.Contracts;
using TeamPulse.Api.Security;
using TeamPulse.Infrastructure;
using TeamPulse.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
if (allowedOrigins.Any(origin => origin == "*"))
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must contain explicit origins when credentials are enabled.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(SecurityPolicies.FrontendCors, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var authPermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 30);
var importPermitLimit = builder.Configuration.GetValue("RateLimiting:Import:PermitLimit", 10);
var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
if (authPermitLimit < 1 || importPermitLimit < 1 || windowSeconds < 1)
{
    throw new InvalidOperationException("Rate limiting values must be positive integers.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(
                "rate_limited",
                "Too many requests. Wait before trying again."),
            cancellationToken);
    };
    options.AddPolicy(SecurityPolicies.AuthRateLimit, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartition(httpContext, "auth"),
            _ => CreateLimiter(authPermitLimit, windowSeconds)));
    options.AddPolicy(SecurityPolicies.ImportRateLimit, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartition(httpContext, "import"),
            _ => CreateLimiter(importPermitLimit, windowSeconds)));
});
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: ["live"])
    .AddDbContextCheck<TeamPulseDbContext>(
        "sql-server",
        tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseCors(SecurityPolicies.FrontendCors);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

app.MapGet("/api", () => Results.Ok(new
{
    name = "TeamPulse.Api",
    status = "running",
}))
    .WithName("GetApiInfo");

app.MapAuthEndpoints();
app.MapWorkspaceEndpoints();
app.MapProjectEndpoints();
app.MapPlanningEndpoints();
app.MapTaskEndpoints();
app.MapTaskCollaborationEndpoints();
app.MapDashboardEndpoints();
app.MapResourceEndpoints();

app.Run();

static string GetClientPartition(HttpContext httpContext, string prefix)
{
    var identity = httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
    var client = identity ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return $"{prefix}:{client}";
}

static FixedWindowRateLimiterOptions CreateLimiter(int permitLimit, int windowSeconds) =>
    new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(windowSeconds),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    };

public partial class Program;
