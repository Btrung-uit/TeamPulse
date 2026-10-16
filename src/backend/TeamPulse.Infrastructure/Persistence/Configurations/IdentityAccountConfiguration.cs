using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Users;
using TeamPulse.Infrastructure.Identity;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class IdentityAccountConfiguration : IEntityTypeConfiguration<IdentityAccount>
{
    public void Configure(EntityTypeBuilder<IdentityAccount> builder)
    {
        builder.ToTable("IdentityAccounts");

        builder.Property(account => account.UserName)
            .HasMaxLength(User.EmailMaximumLength)
            .IsRequired();
        builder.Property(account => account.NormalizedUserName)
            .HasMaxLength(User.EmailMaximumLength)
            .IsRequired();
        builder.Property(account => account.Email)
            .HasMaxLength(User.EmailMaximumLength)
            .IsRequired();
        builder.Property(account => account.NormalizedEmail)
            .HasMaxLength(User.EmailMaximumLength)
            .IsRequired();

        builder.HasOne(account => account.Profile)
            .WithOne()
            .HasForeignKey<IdentityAccount>(account => account.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
