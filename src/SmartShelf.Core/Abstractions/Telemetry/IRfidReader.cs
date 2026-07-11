namespace SmartShelf.Core.Abstractions.Telemetry;
public interface IRfidReader
{
    Task<string?> ReadTagAsync();
}
