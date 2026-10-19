using System.Diagnostics;

namespace TeamPulse.Api.Security;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var correlationId = ResolveCorrelationId(httpContext);
        httpContext.Response.Headers[SecurityPolicies.CorrelationHeader] = correlationId;
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        httpContext.Response.Headers.XFrameOptions = "DENY";
        httpContext.Response.Headers["Referrer-Policy"] = "no-referrer";

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = httpContext.TraceIdentifier,
        });
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(httpContext);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        var requested = httpContext.Request.Headers[SecurityPolicies.CorrelationHeader].ToString();
        return requested.Length is > 0 and <= 64 && requested.All(IsSafeCharacter)
            ? requested
            : httpContext.TraceIdentifier;
    }

    private static bool IsSafeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.';
}
