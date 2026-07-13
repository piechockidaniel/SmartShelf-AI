namespace SmartShelf.Application.Contracts;

public sealed record ShelfStateDto(
    Guid ShelfId,
    string ShelfName,
    string LedColor,
    int ProductCount);
