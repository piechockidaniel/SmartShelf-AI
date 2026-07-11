namespace SmartShelf.Contracts;

public record ShelfStateDto(
Guid ShelfId,
string ShelfName,
string LedColor,
int ProductCount);
