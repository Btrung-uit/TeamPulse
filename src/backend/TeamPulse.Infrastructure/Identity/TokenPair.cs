namespace TeamPulse.Infrastructure.Identity;

internal sealed record TokenPair(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    string RefreshTokenHash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc);
