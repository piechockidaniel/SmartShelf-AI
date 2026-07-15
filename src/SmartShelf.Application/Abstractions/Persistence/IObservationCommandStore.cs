using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Application.Abstractions.Persistence;

public interface IObservationCommandStore
{
    Task RecordAsync(
        LatestShelfObservationDto observation,
        AlertSeverity? severity,
        CancellationToken cancellationToken = default);
}
