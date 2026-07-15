using SmartShelf.Domain.Common;

namespace SmartShelf.Domain.Entities;

public class Product : AuditableEntity
{
    public string SKU { get; private set; } = "";

    public string Name { get; private set; } = "";

    public DateTime ExpirationDate { get; private set; }

    public int Quantity { get; private set; }

    private Product() { }

    public Product(
        string sku,
        string name,
        int quantity,
        DateTime expiration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        SKU = sku;
        Name = name;
        Quantity = quantity;
        ExpirationDate = expiration;
    }

    public bool IsExpired()
        => DateTime.UtcNow > ExpirationDate;

    public int DaysUntilExpiration()
        => (ExpirationDate.Date - DateTime.UtcNow.Date).Days;

    public void UpdateQuantity(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        Quantity = quantity;
        Touch();
    }

    public static Product Restore(
        Guid id, string sku, string name, int quantity, DateTime expirationDate,
        DateTime createdAt, DateTime? updatedAt)
        => new()
        {
            Id = id,
            SKU = sku,
            Name = name,
            Quantity = quantity,
            ExpirationDate = expirationDate,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}
