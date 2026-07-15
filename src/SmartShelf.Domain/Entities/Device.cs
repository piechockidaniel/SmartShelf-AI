using SmartShelf.Domain.Common;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Domain.Entities;

public class Device : AuditableEntity
{
    public string Name { get; private set; } = "";

    public string SerialNumber { get; private set; } = "";

    public DeviceStatus Status { get; private set; }
    public DeviceKind Kind { get; private set; }

    public DateTime LastSeen { get; private set; }

    private Device() { }

    public Device(string name, string serial, DeviceKind kind = DeviceKind.Controller)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        Name = name;
        SerialNumber = serial;
        Kind = kind;
        Status = DeviceStatus.Online;
        LastSeen = DateTime.UtcNow;
    }

    public void Heartbeat()
    {
        LastSeen = DateTime.UtcNow;
        Status = DeviceStatus.Online;
    }

    public void Disconnect() => Status = DeviceStatus.Offline;

    public static Device Restore(
        Guid id, string name, string serialNumber, DeviceKind kind, DeviceStatus status,
        DateTime lastSeen, DateTime createdAt, DateTime? updatedAt)
        => new()
        {
            Id = id,
            Name = name,
            SerialNumber = serialNumber,
            Kind = kind,
            Status = status,
            LastSeen = lastSeen,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}
