using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Infrastructure.Identity;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("RefreshSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();

        builder.Property(session => session.TokenHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(session => session.ReplacedByTokenHash)
            .HasMaxLength(64)
            .IsFixedLength();

        builder.Property(session => session.CreatedAtUtc).HasPrecision(7);
        builder.Property(session => session.ExpiresAtUtc).HasPrecision(7);
        builder.Property(session => session.RevokedAtUtc).HasPrecision(7);
        builder.Property(session => session.RowVersion).IsRowVersion();

        builder.HasOne(session => session.Account)
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => new
        {
            session.UserId,
            session.ExpiresAtUtc,
            session.RevokedAtUtc,
        });
    }
}
