using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;

namespace SmartShelf.Application.Features.Alerts;

public sealed class AcknowledgeAlertHandler(IAlertStore alertStore)
{
    public Task<AlertDto?> HandleAsync(Guid alertId, CancellationToken cancellationToken = default)
        => alertStore.AcknowledgeAsync(alertId, DateTimeOffset.UtcNow, cancellationToken);
}
