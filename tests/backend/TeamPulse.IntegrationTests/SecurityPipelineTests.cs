using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using TeamPulse.Api.Security;

namespace TeamPulse.IntegrationTests;

public sealed class SecurityPipelineTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public SecurityPipelineTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Responses_IncludeCorrelationAndBaselineSecurityHeaders()
    {
        using var client = CreateClient(_factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api");
        request.Headers.Add(SecurityPolicies.CorrelationHeader, "tp-security-test");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            "tp-security-test",
            Assert.Single(response.Headers.GetValues(SecurityPolicies.CorrelationHeader)));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOriginAndRejectsUnknownOrigin()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Cors:AllowedOrigins:0", "https://app.example.test"));
        using var client = CreateClient(factory);

        var allowed = await SendPreflightAsync(client, "https://app.example.test");
        var rejected = await SendPreflightAsync(client, "https://attacker.example.test");

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(
            "https://app.example.test",
            Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(rejected.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AuthRateLimit_ReturnsStableErrorAfterConfiguredPermitLimit()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Auth:PermitLimit", "2");
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
        });
        using var client = CreateClient(factory);
        var body = new { email = "missing@example.com", password = "WrongPass123" };

        var first = await client.PostAsJsonAsync("/api/auth/login", body);
        var second = await client.PostAsJsonAsync("/api/auth/login", body);
        var limited = await client.PostAsJsonAsync("/api/auth/login", body);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        var error = await limited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rate_limited", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ImportRateLimit_IsAppliedIndependentlyFromAuthLimit()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Import:PermitLimit", "1");
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
        });
        using var client = CreateClient(factory);
        const string importPath =
            "/api/workspaces/00000000-0000-0000-0000-000000000001/projects/" +
            "00000000-0000-0000-0000-000000000002/tasks/imports";

        var first = await client.PostAsync(importPath, content: null);
        var limited = await client.PostAsync(importPath, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        var error = await limited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rate_limited", error.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/workspaces")]
    [InlineData("GET", "/api/workspaces/00000000-0000-0000-0000-000000000001/projects")]
    [InlineData("GET", "/api/workspaces/00000000-0000-0000-0000-000000000001/projects/00000000-0000-0000-0000-000000000002/tasks")]
    [InlineData("GET", "/api/workspaces/00000000-0000-0000-0000-000000000001/projects/00000000-0000-0000-0000-000000000002/dashboard")]
    [InlineData("POST", "/api/workspaces/00000000-0000-0000-0000-000000000001/projects/00000000-0000-0000-0000-000000000002/tasks/imports")]
    public async Task ProtectedEndpointGroups_RejectAnonymousRequests(string method, string path)
    {
        using var client = CreateClient(_factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExceptionHandler_DoesNotExposeExceptionOrStackDetails()
    {
        const string secretMarker = "database-password-marker";
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-security-test",
        };
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException(secretMarker),
            CancellationToken.None);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("trace-security-test", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(secretMarker, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    private static async Task<HttpResponseMessage> SendPreflightAsync(
        HttpClient client,
        string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return await client.SendAsync(request);
    }
}
