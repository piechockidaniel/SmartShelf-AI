using SmartShelf.Domain.Common;

namespace SmartShelf.Domain.Entities;

public class Product : AuditableEntity
{
    public string SKU { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTime ExpirationDate { get; set; }

    public int Quantity { get; set; }

    private Product() { }

    public Product(
        string sku,
        string name,
        int quantity,
        DateTime expiration)
    {
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
        Quantity = quantity;
        Touch();
    }
}
