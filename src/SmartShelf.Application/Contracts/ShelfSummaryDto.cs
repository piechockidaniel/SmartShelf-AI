namespace SmartShelf.Application.Contracts;

public sealed record ShelfSummaryDto(
    Guid ShelfId,
    long ObservationCount,
    LatestShelfObservationDto Latest);
