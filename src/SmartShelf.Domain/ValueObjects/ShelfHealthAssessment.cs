using SmartShelf.Domain.Enums;

namespace SmartShelf.Domain.ValueObjects;

public sealed record ShelfHealthAssessment(
    ShelfStatus Status,
    LedColor LedColor,
    string Reason,
    decimal Confidence);
