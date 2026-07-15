using SmartShelf.Domain.Enums;

namespace SmartShelf.Application.Abstractions.Telemetry;

public interface ILedController
{
    Task SetAsync(Guid shelfId, LedColor color, CancellationToken cancellationToken = default);
    Task TurnOffAsync(Guid shelfId, CancellationToken cancellationToken = default);
}
