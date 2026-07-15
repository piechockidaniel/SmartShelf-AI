namespace SmartShelf.Application.Contracts;

public sealed record ShelfDto(
    Guid Id,
    string Name,
    string Warehouse,
    string Aisle,
    string ShelfCode,
    string Position,
    string? DeviceId,
    string? CameraDevice,
    bool Enabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int Version = 1);
