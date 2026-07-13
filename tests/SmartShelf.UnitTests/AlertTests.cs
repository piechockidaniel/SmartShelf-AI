using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;

namespace SmartShelf.UnitTests;

public sealed class AlertTests
{
    [Fact]
    public void Alert_tracks_occurrences_and_lifecycle_transitions()
    {
        var shelfId = Guid.NewGuid();
        var first = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        var alert = new Alert(shelfId, AlertSeverity.Warning, "Low stock", first);

        alert.RecordOccurrence(AlertSeverity.Critical, "Expired product", first.AddMinutes(1));
        alert.Acknowledge(first.AddMinutes(2));
        alert.Resolve(first.AddMinutes(3));

        Assert.Equal(2, alert.Occurrences);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal(AlertStatus.Resolved, alert.Status);
        Assert.NotNull(alert.AcknowledgedAt);
        Assert.NotNull(alert.ResolvedAt);
    }
}
