namespace SmartShelf.Contracts;

public record TelemetryDto(
    Guid DeviceId,
    double Cpu,
    double Memory,
    double Temperature,
    DateTime Timestamp);
