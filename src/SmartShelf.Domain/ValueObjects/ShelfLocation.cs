namespace SmartShelf.Domain.ValueObjects;

public record ShelfLocation(
    string Warehouse,
    string Aisle,
    string Shelf,
    string Position);
