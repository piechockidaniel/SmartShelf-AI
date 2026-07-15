namespace SmartShelf.Application.Contracts;

public sealed record ShelfObservationDto(
    int InventoryPercent,
    int DaysUntilExpiration,
    bool ExpiredProductDetected,
    bool SensorOnline,
    DateTimeOffset CapturedAt);
