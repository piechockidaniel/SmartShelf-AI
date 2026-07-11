namespace SmartShelf.Core.Abstractions.Telemetry;
public interface IDeviceTelemetry
{
    Task<DeviceTelemetry> GetAsync();
}