using SmartShelf.Domain.Entities;

namespace SmartShelf.Core.Abstractions.AI
public interface IAiPredictionService
{
    Task<PredictionResult> PredictShelfHealthAsync(
        Shelf shelf);
}