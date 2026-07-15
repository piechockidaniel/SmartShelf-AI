namespace SmartShelf.Domain.ValueObjects;

public sealed record ShelfSignals(
    int InventoryPercent,
    int DaysUntilExpiration,
    bool ExpiredProductDetected,
    bool SensorOnline);
