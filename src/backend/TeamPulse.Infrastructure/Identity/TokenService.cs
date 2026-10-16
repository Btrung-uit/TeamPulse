using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TeamPulse.Domain.Users;

namespace TeamPulse.Infrastructure.Identity;

internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public TokenPair Create(User user)
    {
        var now = _timeProvider.GetUtcNow();
        var accessTokenExpiresAtUtc = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshTokenExpiresAtUtc = now.AddDays(_options.RefreshTokenDays);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessTokenExpiresAtUtc.UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim("client_id", "teampulse-web"),
            ]),
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);
        var refreshTokenBytes = RandomNumberGenerator.GetBytes(64);
        var refreshToken = Base64UrlEncoder.Encode(refreshTokenBytes);

        return new TokenPair(
            accessToken,
            accessTokenExpiresAtUtc,
            refreshToken,
            HashRefreshToken(refreshToken),
            now,
            refreshTokenExpiresAtUtc);
    }

    public static string HashRefreshToken(string refreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(hash);
    }
}
