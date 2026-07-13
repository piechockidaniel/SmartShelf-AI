using SmartShelf.Application.Contracts;

namespace SmartShelf.Application.Abstractions.Telemetry;

public interface IDeviceTelemetry
{
    Task<TelemetryDto> GetAsync(CancellationToken cancellationToken = default);
}
