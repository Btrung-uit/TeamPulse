namespace TeamPulse.Application.Authentication;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
