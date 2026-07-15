namespace SmartShelf.Application.Contracts;

public sealed record ShelfDecisionDto(
    Guid ShelfId,
    string Status,
    string LedColor,
    float Confidence,
    string Reason);
