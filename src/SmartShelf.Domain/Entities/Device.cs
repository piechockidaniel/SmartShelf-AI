using SmartShelf.Domain.Common;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Domain.Entities;

public class Device : AuditableEntity
{
    public string Name { get; private set; } = "";

    public string SerialNumber { get; private set; } = "";

    public DeviceStatus Status { get; private set; }

    public DateTime LastSeen { get; private set; }

    private Device() { }

    public Device(string name, string serial)
    {
        Name = name;
        SerialNumber = serial;
        Status = DeviceStatus.Online;
        LastSeen = DateTime.UtcNow;
    }

    public void Heartbeat()
    {
        LastSeen = DateTime.UtcNow;
        Status = DeviceStatus.Online;
    }

    public void Disconnect()
    {
        Status = DeviceStatus.Offline;
    }
}