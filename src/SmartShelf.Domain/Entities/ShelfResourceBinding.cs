using SmartShelf.Domain.Common;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Domain.Entities;

public sealed class ShelfResourceBinding : Entity
{
    public ShelfResourceKind Kind { get; private set; }
    public Guid ResourceId { get; private set; }

    private ShelfResourceBinding() { }

    public ShelfResourceBinding(ShelfResourceKind kind, Guid resourceId)
    {
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("A resource id is required.", nameof(resourceId));
        }

        Kind = kind;
        ResourceId = resourceId;
    }

    public static ShelfResourceBinding Restore(Guid id, ShelfResourceKind kind, Guid resourceId)
        => new(kind, resourceId) { Id = id };
}
