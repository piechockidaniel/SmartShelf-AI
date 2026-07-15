namespace SmartShelf.Application.Contracts;

public sealed record ShelfOverviewDto(
    ShelfDto Shelf,
    IReadOnlyList<ResourceNodeDto> Resources,
    LatestShelfObservationDto? LatestObservation,
    IReadOnlyList<AlertDto> OpenAlerts);
