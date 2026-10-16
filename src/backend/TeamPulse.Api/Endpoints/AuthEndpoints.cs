using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TeamPulse.Api.Contracts;
using TeamPulse.Application.Authentication;
using TeamPulse.Domain.Users;

namespace TeamPulse.Api.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshCookieName = "teampulse.refresh";
    private const int PasswordMaximumLength = 128;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        DisableCaching(httpContext);
        var validationErrors = ValidateRegistration(request);
        if (validationErrors.Count > 0)
        {
            return Results.BadRequest(new ApiErrorResponse(
                "validation_failed",
                "Please correct the highlighted fields.",
                validationErrors));
        }

        var result = await authService.RegisterAsync(
            request.Email,
            request.DisplayName,
            request.Password,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new ApiErrorResponse(
                "registration_failed",
                "Unable to create an account with the provided details."));
        }

        return CreateSessionResult(httpContext, result.Session!, created: true);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        DisableCaching(httpContext);
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return InvalidCredentials();
        }

        var result = await authService.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        return CreateSessionResult(httpContext, result.Session!, created: false);
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        DisableCaching(httpContext);
        if (!httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return InvalidRefreshToken();
        }

        var result = await authService.RefreshAsync(refreshToken, cancellationToken);
        if (!result.Succeeded)
        {
            DeleteRefreshCookie(httpContext);
            return InvalidRefreshToken();
        }

        return CreateSessionResult(httpContext, result.Session!, created: false);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        DisableCaching(httpContext);
        if (httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            await authService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        }

        DeleteRefreshCookie(httpContext);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        DisableCaching(httpContext);
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await authService.GetUserAsync(userId, cancellationToken);
        return user is null ? Results.Unauthorized() : Results.Ok(user);
    }

    private static IResult CreateSessionResult(
        HttpContext httpContext,
        AuthSession session,
        bool created)
    {
        httpContext.Response.Cookies.Append(
            RefreshCookieName,
            session.RefreshToken,
            CreateRefreshCookieOptions(httpContext, session.RefreshTokenExpiresAtUtc));

        var response = new SessionResponse(
            session.User,
            session.AccessToken,
            session.AccessTokenExpiresAtUtc);

        return created
            ? Results.Created("/api/auth/me", response)
            : Results.Ok(response);
    }

    private static CookieOptions CreateRefreshCookieOptions(
        HttpContext httpContext,
        DateTimeOffset expiresAtUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = expiresAtUtc,
            IsEssential = true,
        };
    }

    private static void DeleteRefreshCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(
            RefreshCookieName,
            CreateRefreshCookieOptions(httpContext, DateTimeOffset.UnixEpoch));
    }

    private static void DisableCaching(HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
    }

    private static IResult InvalidCredentials()
    {
        return Results.Json(
            new ApiErrorResponse("invalid_credentials", "Invalid email or password."),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static IResult InvalidRefreshToken()
    {
        return Results.Json(
            new ApiErrorResponse("invalid_session", "Your session is invalid or has expired."),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var email = request.Email?.Trim() ?? string.Empty;
        var displayName = request.DisplayName?.Trim() ?? string.Empty;

        if (email.Length == 0 ||
            email.Length > User.EmailMaximumLength ||
            !new EmailAddressAttribute().IsValid(email))
        {
            errors["email"] = ["Enter a valid email address."];
        }

        if (displayName.Length == 0 || displayName.Length > User.DisplayNameMaximumLength)
        {
            errors["displayName"] =
                [$"Display name must contain 1 to {User.DisplayNameMaximumLength} characters."];
        }

        if (request.Password is null ||
            request.Password.Length < 8 ||
            request.Password.Length > PasswordMaximumLength ||
            !request.Password.Any(char.IsUpper) ||
            !request.Password.Any(char.IsLower) ||
            !request.Password.Any(char.IsDigit))
        {
            errors["password"] =
            [
                $"Password must contain 8 to {PasswordMaximumLength} characters, " +
                "including uppercase, lowercase, and a number.",
            ];
        }

        return errors;
    }

    public sealed record RegisterRequest(string Email, string DisplayName, string Password);

    public sealed record LoginRequest(string Email, string Password);

    private sealed record SessionResponse(
        AuthenticatedUser User,
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAtUtc);
}
