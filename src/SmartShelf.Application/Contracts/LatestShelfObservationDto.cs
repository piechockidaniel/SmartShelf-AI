namespace SmartShelf.Application.Contracts;

public sealed record LatestShelfObservationDto(
    Guid ShelfId,
    ShelfObservationDto Observation,
    ShelfDecisionDto Decision);
