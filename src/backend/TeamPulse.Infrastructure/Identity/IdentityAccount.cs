using Microsoft.AspNetCore.Identity;
using TeamPulse.Domain.Users;

namespace TeamPulse.Infrastructure.Identity;

public sealed class IdentityAccount : IdentityUser<Guid>
{
    public IdentityAccount()
    {
    }

    public IdentityAccount(Guid userId, string email)
    {
        Id = userId;
        UserName = email;
        Email = email;
        LockoutEnabled = true;
    }

    public User Profile { get; private set; } = null!;
}
