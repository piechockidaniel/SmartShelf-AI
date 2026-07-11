namespace SmartShelf.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; protected set; }

    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}