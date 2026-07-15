using SmartShelf.Domain.ValueObjects;

namespace SmartShelf.Application.Features.Shelves;

public sealed record CreateShelfCommand(
    string Name,
    ShelfLocation Location);
