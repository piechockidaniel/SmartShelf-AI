using SmartShelf.Domain.Common;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Domain.Entities;

public sealed class Alert : AuditableEntity
{
    public Guid ShelfId { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public AlertStatus Status { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public int Occurrences { get; private set; }
    public DateTimeOffset LastOccurredAt { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private Alert() { }

    public Alert(Guid shelfId, AlertSeverity severity, string message, DateTimeOffset occurredAt)
    {
        ShelfId = shelfId;
        Severity = severity;
        Message = message;
        Status = AlertStatus.Active;
        Occurrences = 1;
        LastOccurredAt = occurredAt;
    }

    public void RecordOccurrence(AlertSeverity severity, string message, DateTimeOffset occurredAt)
    {
        Severity = severity;
        Message = message;
        LastOccurredAt = occurredAt;
        Occurrences++;
        Touch();
    }

    public void Acknowledge(DateTimeOffset acknowledgedAt)
    {
        if (Status == AlertStatus.Resolved)
        {
            return;
        }

        Status = AlertStatus.Acknowledged;
        AcknowledgedAt = acknowledgedAt;
        Touch();
    }

    public void Resolve(DateTimeOffset resolvedAt)
    {
        Status = AlertStatus.Resolved;
        ResolvedAt = resolvedAt;
        Touch();
    }
}
