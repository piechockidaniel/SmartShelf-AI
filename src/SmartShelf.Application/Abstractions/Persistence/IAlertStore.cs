using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Application.Abstractions.Persistence;

public interface IAlertStore
{
    Task<AlertDto> UpsertOpenAsync(
        Guid shelfId,
        AlertSeverity severity,
        string message,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);

    Task ResolveOpenAsync(
        Guid shelfId,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertDto>> GetAsync(
        bool openOnly,
        int limit,
        CancellationToken cancellationToken = default);

    Task<AlertDto?> AcknowledgeAsync(
        Guid alertId,
        DateTimeOffset acknowledgedAt,
        CancellationToken cancellationToken = default);
}
