using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPulse.Domain.Common;

namespace TeamPulse.Infrastructure.Persistence.Configurations;

internal static class AuditableConfigurationExtensions
{
    public static void ConfigureAuditProperties<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IAuditableEntity
    {
        builder.Property(entity => entity.CreatedAtUtc)
            .HasPrecision(7)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(entity => entity.UpdatedAtUtc)
            .HasPrecision(7)
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
