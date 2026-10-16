using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TeamPulse.IntegrationTests;

public sealed class AuthenticationEndpointsTests : IClassFixture<TeamPulseApiFactory>
{
    private readonly TeamPulseApiFactory _factory;

    public AuthenticationEndpointsTests(TeamPulseApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_IssuesSessionAndAccessesProtectedCurrentUser()
    {
        using var client = CreateClient();
        var email = $"member-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "New Member",
            password = "StrongPass123",
        });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal("/api/auth/me", registerResponse.Headers.Location?.OriginalString);
        var setCookie = Assert.Single(registerResponse.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);

        var session = await ReadJsonAsync(registerResponse);
        var accessToken = session.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        using var anonymousClient = CreateClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        var currentUserResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
        var currentUser = await ReadJsonAsync(currentUserResponse);
        Assert.Equal(email, currentUser.RootElement.GetProperty("email").GetString());
        Assert.Equal(
            "New Member",
            currentUser.RootElement.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsGenericSafeError()
    {
        using var client = CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPass123",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", body, StringComparison.Ordinal);
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithValidPassword_ReturnsNewSession()
    {
        using var client = CreateClient();
        var email = $"valid-login-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "StrongPass123",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await ReadJsonAsync(response);
        Assert.Equal(
            email,
            session.RootElement.GetProperty("user").GetProperty("email").GetString());
        Assert.False(
            string.IsNullOrWhiteSpace(
                session.RootElement.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Login_AfterRepeatedFailures_IsTemporarilyLockedOut()
    {
        using var client = CreateClient();
        var email = $"lockout-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failedResponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "WrongPass123",
            });
            Assert.Equal(HttpStatusCode.Unauthorized, failedResponse.StatusCode);
        }

        var lockedResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "StrongPass123",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, lockedResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshRotatesSession_AndLogoutRevokesIt()
    {
        using var client = CreateClient();
        await RegisterAsync(client, $"refresh-{Guid.NewGuid():N}@example.com");

        var refreshResponse = await client.PostAsync("/api/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var rejectedRefresh = await client.PostAsync("/api/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedRefresh.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_WithMalformedToken_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            displayName = "Test Member",
            password = "StrongPass123",
        });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
