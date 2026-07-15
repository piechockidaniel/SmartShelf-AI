using System.Collections.Concurrent;
using SmartShelf.Application.Abstractions.Telemetry;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Infrastructure.Device;

public sealed class VirtualLedController : ILedController
{
    private readonly ConcurrentDictionary<Guid, LedColor> _colors = new();

    public IReadOnlyDictionary<Guid, LedColor> Current => _colors;

    public Task SetAsync(
        Guid shelfId, LedColor color, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _colors[shelfId] = color;
        return Task.CompletedTask;
    }

    public Task TurnOffAsync(Guid shelfId, CancellationToken cancellationToken = default)
        => SetAsync(shelfId, LedColor.Off, cancellationToken);
}
