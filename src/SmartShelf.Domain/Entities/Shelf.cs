using SmartShelf.Domain.Common;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;

namespace SmartShelf.Domain.Entities;

public class Shelf : AuditableEntity
{
    private readonly List<Product> _products = [];

    public string Name { get; private set; } = "";

    public ShelfLocation Location { get; private set; } = new ShelfLocation("", "", "", "");

    public LedColor LedColor { get; private set; }

    public ShelfStatus Status { get; private set; }

    public IReadOnlyCollection<Product> Products => _products;

    private Shelf() { }

    public Shelf(string name, ShelfLocation location)
    {
        Name = name;
        Location = location;

        Status = ShelfStatus.Healthy;
        LedColor = LedColor.Green;
    }

    public void AddProduct(Product product)
    {
        _products.Add(product);
        EvaluateStatus();
    }

    public void RemoveProduct(Guid productId)
    {
        var product = _products.FirstOrDefault(x => x.Id == productId);

        if (product is null)
            return;

        _products.Remove(product);

        EvaluateStatus();
    }

    private void EvaluateStatus()
    {
        if (_products.Any(p => p.IsExpired()))
        {
            Status = ShelfStatus.Critical;
            LedColor = LedColor.Red;
            return;
        }

        if (_products.Any(p => p.DaysUntilExpiration() < 7))
        {
            Status = ShelfStatus.Warning;
            LedColor = LedColor.Yellow;
            return;
        }

        Status = ShelfStatus.Healthy;
        LedColor = LedColor.Green;
    }
}