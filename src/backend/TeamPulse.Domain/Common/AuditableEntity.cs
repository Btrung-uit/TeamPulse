namespace TeamPulse.Domain.Common;

public abstract class AuditableEntity : IAuditableEntity
{
    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity ID cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}
