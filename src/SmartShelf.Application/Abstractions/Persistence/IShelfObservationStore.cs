using SmartShelf.Application.Contracts;

namespace SmartShelf.Application.Abstractions.Persistence;

public interface IShelfObservationStore
{
    Task SaveAsync(
        LatestShelfObservationDto observation,
        CancellationToken cancellationToken = default);

    Task<LatestShelfObservationDto?> GetLatestAsync(
        Guid shelfId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LatestShelfObservationDto>> GetRecentAsync(
        Guid shelfId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShelfSummaryDto>> GetSummariesAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
