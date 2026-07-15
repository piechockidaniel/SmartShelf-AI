using SmartShelf.Domain.ValueObjects;

namespace SmartShelf.Application.Features.Shelves;

public sealed record UpdateShelfCommand(
    string Name,
    ShelfLocation Location,
    int? ExpectedVersion = null);
