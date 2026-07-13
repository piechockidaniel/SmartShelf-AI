using SmartShelf.Application.Contracts;
using SmartShelf.Infrastructure.Persistence;

namespace SmartShelf.IntegrationTests;

public sealed class SqliteShelfObservationStoreTests
{
    [Fact]
    public async Task Saved_observation_can_be_loaded_by_a_new_store_instance()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"smartshelf-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";

        try
        {
            var shelfId = Guid.NewGuid();
            var capturedAt = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
            var expected = new LatestShelfObservationDto(
                shelfId,
                new ShelfObservationDto(28, 5, false, true, capturedAt),
                new ShelfDecisionDto(shelfId, "Warning", "Yellow", 0.86f, "Expiry risk"));

            var writer = new SqliteShelfObservationStore(connectionString);
            await writer.SaveAsync(expected, TestContext.Current.CancellationToken);

            var reader = new SqliteShelfObservationStore(connectionString);
            var actual = await reader.GetLatestAsync(shelfId, TestContext.Current.CancellationToken);

            Assert.NotNull(actual);
            Assert.Equal(expected.ShelfId, actual.ShelfId);
            Assert.Equal(expected.Observation, actual.Observation);
            Assert.Equal(expected.Decision.Status, actual.Decision.Status);
            Assert.Equal(expected.Decision.LedColor, actual.Decision.LedColor);
            Assert.Equal(expected.Decision.Confidence, actual.Decision.Confidence, 2);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}

