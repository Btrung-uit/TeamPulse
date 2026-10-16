namespace TeamPulse.Domain.Common;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAtUtc { get; }

    DateTimeOffset UpdatedAtUtc { get; }
}
