using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Users;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasMaxLength(User.EmailMaximumLength)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasMaxLength(User.EmailMaximumLength)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(User.DisplayNameMaximumLength)
            .IsRequired();

        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.ConfigureAuditProperties();
    }
}
