using SmartShelf.Application.Contracts;

namespace SmartShelf.Application.Features.ShelfObservations;

public sealed record RecordShelfObservationCommand(
    Guid ShelfId,
    ShelfObservationDto Observation);
