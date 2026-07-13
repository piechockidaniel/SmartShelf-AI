namespace SmartShelf.Application.Contracts;

public sealed record ShelfTelemetryDto(
    Guid ShelfId,
    int ProductCount,
    double TemperatureCelsius,
    DateTimeOffset CapturedAt);
