using System.Globalization;
using Microsoft.Data.Sqlite;
using SmartShelf.Application.Contracts;

namespace SmartShelf.Infrastructure.Persistence;

public sealed partial class SqliteShelfObservationStore
{
    public async Task<IReadOnlyList<LatestShelfObservationDto>> GetRecentAsync(
        Guid shelfId, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT captured_at, inventory_percent, days_until_expiration,
                   expired_product_detected, sensor_online, status, led_color, confidence, reason
            FROM shelf_observations
            WHERE shelf_id = $shelfId
            ORDER BY captured_at DESC, id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$shelfId", shelfId.ToString("D"));
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));

        var results = new List<LatestShelfObservationDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadObservation(reader, shelfId, 0));
        }

        return results;
    }

    public async Task<IReadOnlyList<ShelfSummaryDto>> GetSummariesAsync(
        int limit, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT o.shelf_id,
                   (SELECT COUNT(*) FROM shelf_observations c WHERE c.shelf_id = o.shelf_id),
                   o.captured_at, o.inventory_percent, o.days_until_expiration,
                   o.expired_product_detected, o.sensor_online, o.status, o.led_color,
                   o.confidence, o.reason
            FROM shelf_observations o
            WHERE o.id = (
                SELECT latest.id FROM shelf_observations latest
                WHERE latest.shelf_id = o.shelf_id
                ORDER BY latest.captured_at DESC, latest.id DESC LIMIT 1
            )
            ORDER BY o.captured_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));

        var results = new List<ShelfSummaryDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var shelfId = Guid.Parse(reader.GetString(0));
            results.Add(new ShelfSummaryDto(
                shelfId, reader.GetInt64(1), ReadObservation(reader, shelfId, 2)));
        }

        return results;
    }

    private static LatestShelfObservationDto ReadObservation(
        SqliteDataReader reader, Guid shelfId, int offset)
    {
        var capturedAt = DateTimeOffset.Parse(
            reader.GetString(offset), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var observation = new ShelfObservationDto(
            reader.GetInt32(offset + 1), reader.GetInt32(offset + 2),
            reader.GetBoolean(offset + 3), reader.GetBoolean(offset + 4), capturedAt);
        var decision = new ShelfDecisionDto(
            shelfId, reader.GetString(offset + 5), reader.GetString(offset + 6),
            reader.GetFloat(offset + 7), reader.GetString(offset + 8));
        return new LatestShelfObservationDto(shelfId, observation, decision);
    }
}
