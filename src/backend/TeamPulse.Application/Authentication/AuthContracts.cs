namespace TeamPulse.Application.Authentication;

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string DisplayName);

public sealed record AuthSession(
    AuthenticatedUser User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public enum AuthFailure
{
    InvalidCredentials,
    RegistrationFailed,
    InvalidRefreshToken,
}

public sealed record AuthResult(AuthSession? Session, AuthFailure? Failure)
{
    public bool Succeeded => Session is not null;

    public static AuthResult Success(AuthSession session) => new(session, null);

    public static AuthResult Failed(AuthFailure failure) => new(null, failure);
}
