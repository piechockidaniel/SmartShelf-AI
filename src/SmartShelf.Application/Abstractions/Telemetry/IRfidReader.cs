namespace SmartShelf.Application.Abstractions.Telemetry;

public interface IRfidReader
{
    Task<string?> ReadTagAsync(CancellationToken cancellationToken = default);
}
