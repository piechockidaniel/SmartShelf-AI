namespace SmartShelf.Application.Contracts;

public sealed record TelemetryDto(
    Guid DeviceId,
    double Cpu,
    double Memory,
    double Temperature,
    DateTime Timestamp);
