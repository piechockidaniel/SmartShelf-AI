namespace SmartShelf.Application.Contracts;

public sealed record AlertDto(
    Guid Id,
    Guid ShelfId,
    string Severity,
    string Status,
    string Message,
    int Occurrences,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastOccurredAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt);
