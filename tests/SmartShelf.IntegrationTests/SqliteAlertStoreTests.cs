using SmartShelf.Domain.Enums;
using SmartShelf.Infrastructure.Sqlite.Persistence;
using Xunit;

namespace SmartShelf.IntegrationTests;

public sealed class SqliteAlertStoreTests
{
    [Fact]
    public async Task Alert_is_deduplicated_acknowledged_and_resolved()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"smartshelf-alerts-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            var shelfId = Guid.NewGuid();
            var firstTime = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
            var store = new SqliteAlertStore(connectionString);

            var first = await store.UpsertOpenAsync(
                shelfId, AlertSeverity.Warning, "Low stock", firstTime, cancellationToken);
            var repeated = await store.UpsertOpenAsync(
                shelfId, AlertSeverity.Critical, "Expired product", firstTime.AddMinutes(1), cancellationToken);

            Assert.Equal(first.Id, repeated.Id);
            Assert.Equal(2, repeated.Occurrences);
            Assert.Equal("Critical", repeated.Severity);

            var acknowledged = await store.AcknowledgeAsync(
                repeated.Id, firstTime.AddMinutes(2), cancellationToken);
            Assert.NotNull(acknowledged);
            Assert.Equal("Acknowledged", acknowledged.Status);

            await store.ResolveOpenAsync(shelfId, firstTime.AddMinutes(3), cancellationToken);
            var openAlerts = await store.GetAsync(true, 100, cancellationToken);
            var allAlerts = await store.GetAsync(false, 100, cancellationToken);

            Assert.Empty(openAlerts);
            Assert.Single(allAlerts);
            Assert.Equal("Resolved", allAlerts[0].Status);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
