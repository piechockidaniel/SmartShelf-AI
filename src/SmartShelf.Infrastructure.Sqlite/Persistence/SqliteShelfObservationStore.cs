using System.Globalization;
using Dapper;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Infrastructure.Sqlite.Persistence;

public sealed class SqliteShelfObservationStore : IShelfObservationStore, IObservationCommandStore
{
    private readonly SqliteDatabase _database;

    public SqliteShelfObservationStore(string connectionString) => _database = new(connectionString);
    public SqliteShelfObservationStore(SqliteDatabase database) => _database = database;

    public async Task SaveAsync(LatestShelfObservationDto observation, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await InsertAsync(connection, null, observation, cancellationToken);
    }

    public async Task RecordAsync(
        LatestShelfObservationDto observation,
        AlertSeverity? severity,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await InsertAsync(connection, transaction, observation, cancellationToken);

        if (severity is null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE alerts SET status='Resolved', resolved_at=@at WHERE shelf_id=@shelfId AND status <> 'Resolved';",
                new { shelfId = observation.ShelfId.ToString("D"), at = observation.Observation.CapturedAt.ToString("O") },
                transaction, cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO alerts(id, shelf_id, severity, status, message, occurrences, created_at, last_occurred_at)
                VALUES (@id, @shelfId, @severity, 'Active', @message, 1, @at, @at)
                ON CONFLICT(shelf_id) WHERE status <> 'Resolved'
                DO UPDATE SET severity=excluded.severity, message=excluded.message,
                    occurrences=alerts.occurrences + 1, last_occurred_at=excluded.last_occurred_at;
                """,
                new
                {
                    id = Guid.NewGuid().ToString("D"),
                    shelfId = observation.ShelfId.ToString("D"),
                    severity = severity.Value.ToString(),
                    message = observation.Decision.Reason,
                    at = observation.Observation.CapturedAt.ToString("O")
                }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<LatestShelfObservationDto?> GetLatestAsync(Guid shelfId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ObservationRow>(new CommandDefinition(
            ObservationSql + " WHERE shelf_id=@shelfId ORDER BY captured_at DESC, id DESC LIMIT 1;",
            new { shelfId = shelfId.ToString("D") }, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<LatestShelfObservationDto>> GetRecentAsync(
        Guid shelfId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ObservationRow>(new CommandDefinition(
            ObservationSql + " WHERE shelf_id=@shelfId ORDER BY captured_at DESC, id DESC LIMIT @limit;",
            new { shelfId = shelfId.ToString("D"), limit = Math.Clamp(limit, 1, 500) },
            cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<ShelfSummaryDto>> GetSummariesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<SummaryRow>(new CommandDefinition(
            """
            SELECT o.id Id, o.shelf_id ShelfId, o.captured_at CapturedAt,
                   o.inventory_percent InventoryPercent, o.days_until_expiration DaysUntilExpiration,
                   o.expired_product_detected ExpiredProductDetected, o.sensor_online SensorOnline,
                   o.status Status, o.led_color LedColor, o.confidence Confidence, o.reason Reason,
                   (SELECT COUNT(*) FROM shelf_observations c WHERE c.shelf_id=o.shelf_id) ObservationCount
            FROM shelf_observations o
            WHERE o.id=(SELECT latest.id FROM shelf_observations latest
                        WHERE latest.shelf_id=o.shelf_id
                        ORDER BY latest.captured_at DESC, latest.id DESC LIMIT 1)
            ORDER BY o.captured_at DESC LIMIT @limit;
            """,
            new { limit = Math.Clamp(limit, 1, 500) }, cancellationToken: cancellationToken));
        return rows.Select(row => new ShelfSummaryDto(Guid.Parse(row.ShelfId), row.ObservationCount, Map(row))).ToArray();
    }

    private static Task<int> InsertAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? transaction,
        LatestShelfObservationDto observation,
        CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO shelf_observations(
                shelf_id, captured_at, inventory_percent, days_until_expiration,
                expired_product_detected, sensor_online, status, led_color, confidence, reason)
            VALUES(@ShelfId, @CapturedAt, @InventoryPercent, @DaysUntilExpiration,
                @ExpiredProductDetected, @SensorOnline, @Status, @LedColor, @Confidence, @Reason);
            """,
            new
            {
                ShelfId = observation.ShelfId.ToString("D"),
                CapturedAt = observation.Observation.CapturedAt.ToString("O"),
                observation.Observation.InventoryPercent,
                observation.Observation.DaysUntilExpiration,
                observation.Observation.ExpiredProductDetected,
                observation.Observation.SensorOnline,
                observation.Decision.Status,
                observation.Decision.LedColor,
                observation.Decision.Confidence,
                observation.Decision.Reason
            }, transaction, cancellationToken: cancellationToken));

    private static LatestShelfObservationDto Map(ObservationRow row)
    {
        var shelfId = Guid.Parse(row.ShelfId);
        var observation = new ShelfObservationDto(
            checked((int)row.InventoryPercent), checked((int)row.DaysUntilExpiration), row.ExpiredProductDetected != 0,
            row.SensorOnline != 0, ParseTimestamp(row.CapturedAt));
        var decision = new ShelfDecisionDto(
            shelfId, row.Status, row.LedColor, (float)row.Confidence, row.Reason);
        return new LatestShelfObservationDto(shelfId, observation, decision);
    }

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private const string ObservationSql =
        "SELECT id Id, shelf_id ShelfId, captured_at CapturedAt, inventory_percent InventoryPercent, days_until_expiration DaysUntilExpiration, expired_product_detected ExpiredProductDetected, sensor_online SensorOnline, status Status, led_color LedColor, confidence Confidence, reason Reason FROM shelf_observations";

    private record ObservationRow(
        long Id, string ShelfId, string CapturedAt, long InventoryPercent, long DaysUntilExpiration,
        long ExpiredProductDetected, long SensorOnline, string Status, string LedColor,
        double Confidence, string Reason);
    private sealed record SummaryRow(
        long Id, string ShelfId, string CapturedAt, long InventoryPercent, long DaysUntilExpiration,
        long ExpiredProductDetected, long SensorOnline, string Status, string LedColor,
        double Confidence, string Reason, long ObservationCount)
        : ObservationRow(Id, ShelfId, CapturedAt, InventoryPercent, DaysUntilExpiration,
            ExpiredProductDetected, SensorOnline, Status, LedColor, Confidence, Reason);
}
