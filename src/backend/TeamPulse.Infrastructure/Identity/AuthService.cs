using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TeamPulse.Application.Authentication;
using TeamPulse.Domain.Users;
using TeamPulse.Infrastructure.Persistence;

namespace TeamPulse.Infrastructure.Identity;

internal sealed class AuthService(
    TeamPulseDbContext dbContext,
    UserManager<IdentityAccount> userManager,
    SignInManager<IdentityAccount> signInManager,
    TokenService tokenService,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();

        if (await userManager.FindByEmailAsync(normalizedEmail) is not null)
        {
            return AuthResult.Failed(AuthFailure.RegistrationFailed);
        }

        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        await using (transaction)
        {
            var userId = Guid.NewGuid();
            var profile = new User(userId, normalizedEmail, displayName);
            var account = new IdentityAccount(userId, profile.Email);

            dbContext.UserProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);

            var identityResult = await userManager.CreateAsync(account, password);
            if (!identityResult.Succeeded)
            {
                if (transaction is null)
                {
                    dbContext.UserProfiles.Remove(profile);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return AuthResult.Failed(AuthFailure.RegistrationFailed);
            }

            var session = CreateSession(profile);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return AuthResult.Success(session);
        }
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var account = await userManager.FindByEmailAsync(email.Trim());
        if (account is null)
        {
            return AuthResult.Failed(AuthFailure.InvalidCredentials);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            account,
            password,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return AuthResult.Failed(AuthFailure.InvalidCredentials);
        }

        var profile = await dbContext.UserProfiles.SingleAsync(
            user => user.Id == account.Id,
            cancellationToken);

        var session = CreateSession(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return AuthResult.Success(session);
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var tokenHash = TokenService.HashRefreshToken(refreshToken);
        var existingSession = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            session => session.TokenHash == tokenHash,
            cancellationToken);

        if (existingSession is null)
        {
            return AuthResult.Failed(AuthFailure.InvalidRefreshToken);
        }

        if (existingSession.RevokedAtUtc is not null)
        {
            await RevokeAllActiveSessionsAsync(existingSession.UserId, now, cancellationToken);
            return AuthResult.Failed(AuthFailure.InvalidRefreshToken);
        }

        if (!existingSession.IsActiveAt(now))
        {
            existingSession.Revoke(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return AuthResult.Failed(AuthFailure.InvalidRefreshToken);
        }

        var profile = await dbContext.UserProfiles.SingleOrDefaultAsync(
            user => user.Id == existingSession.UserId,
            cancellationToken);

        if (profile is null)
        {
            return AuthResult.Failed(AuthFailure.InvalidRefreshToken);
        }

        var tokenPair = tokenService.Create(profile);
        existingSession.Revoke(now, tokenPair.RefreshTokenHash);
        dbContext.RefreshSessions.Add(CreateRefreshSession(profile.Id, tokenPair));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var userId = existingSession.UserId;
            dbContext.ChangeTracker.Clear();
            await RevokeAllActiveSessionsAsync(userId, now, cancellationToken);
            return AuthResult.Failed(AuthFailure.InvalidRefreshToken);
        }

        return AuthResult.Success(ToAuthSession(profile, tokenPair));
    }

    public async Task RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = TokenService.HashRefreshToken(refreshToken);
        var session = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            candidate => candidate.TokenHash == tokenHash,
            cancellationToken);

        if (session is null || session.RevokedAtUtc is not null)
        {
            return;
        }

        session.Revoke(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedUser?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UserProfiles
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AuthenticatedUser(user.Id, user.Email, user.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private AuthSession CreateSession(User profile)
    {
        var tokenPair = tokenService.Create(profile);
        dbContext.RefreshSessions.Add(CreateRefreshSession(profile.Id, tokenPair));
        return ToAuthSession(profile, tokenPair);
    }

    private static RefreshSession CreateRefreshSession(Guid userId, TokenPair tokenPair)
    {
        return new RefreshSession(
            Guid.NewGuid(),
            userId,
            tokenPair.RefreshTokenHash,
            tokenPair.CreatedAtUtc,
            tokenPair.RefreshTokenExpiresAtUtc);
    }

    private static AuthSession ToAuthSession(User profile, TokenPair tokenPair)
    {
        return new AuthSession(
            new AuthenticatedUser(profile.Id, profile.Email, profile.DisplayName),
            tokenPair.AccessToken,
            tokenPair.AccessTokenExpiresAtUtc,
            tokenPair.RefreshToken,
            tokenPair.RefreshTokenExpiresAtUtc);
    }

    private async Task RevokeAllActiveSessionsAsync(
        Guid userId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.RefreshSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.Revoke(revokedAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
