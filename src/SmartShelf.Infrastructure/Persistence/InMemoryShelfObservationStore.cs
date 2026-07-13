using System.Collections.Concurrent;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;

namespace SmartShelf.Infrastructure.Persistence;

public sealed class InMemoryShelfObservationStore : IShelfObservationStore
{
    private readonly ConcurrentDictionary<Guid, List<LatestShelfObservationDto>> observations = new();

    public Task SaveAsync(
        LatestShelfObservationDto observation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var history = observations.GetOrAdd(observation.ShelfId, _ => []);
        lock (history)
        {
            history.Add(observation);
        }

        return Task.CompletedTask;
    }

    public Task<LatestShelfObservationDto?> GetLatestAsync(
        Guid shelfId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recent = GetSnapshot(shelfId, 1);
        return Task.FromResult(recent.FirstOrDefault());
    }

    public Task<IReadOnlyList<LatestShelfObservationDto>> GetRecentAsync(
        Guid shelfId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<LatestShelfObservationDto>>(GetSnapshot(shelfId, limit));
    }

    public Task<IReadOnlyList<ShelfSummaryDto>> GetSummariesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summaries = observations
            .Take(Math.Clamp(limit, 1, 500))
            .Select(pair =>
            {
                lock (pair.Value)
                {
                    var latest = pair.Value.OrderByDescending(item => item.Observation.CapturedAt).First();
                    return new ShelfSummaryDto(pair.Key, pair.Value.Count, latest);
                }
            })
            .OrderByDescending(summary => summary.Latest.Observation.CapturedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ShelfSummaryDto>>(summaries);
    }

    private IReadOnlyList<LatestShelfObservationDto> GetSnapshot(Guid shelfId, int limit)
    {
        if (!observations.TryGetValue(shelfId, out var history))
        {
            return [];
        }

        lock (history)
        {
            return [.. history
                .OrderByDescending(item => item.Observation.CapturedAt)
                .Take(Math.Clamp(limit, 1, 500))];
        }
    }
}
