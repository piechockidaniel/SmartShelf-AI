using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Entities;

namespace SmartShelf.Application.Abstractions.AI;

public interface IAiPredictionService
{
    Task<ShelfDecisionDto> PredictShelfHealthAsync(
        Shelf shelf,
        CancellationToken cancellationToken = default);
}
