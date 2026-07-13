using System.Globalization;
using Microsoft.Data.Sqlite;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;

namespace SmartShelf.Infrastructure.Persistence;

public sealed partial class SqliteShelfObservationStore : IShelfObservationStore
{
    private readonly string connectionString;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private bool initialized;

    public SqliteShelfObservationStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public async Task SaveAsync(
        LatestShelfObservationDto observation,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO shelf_observations (
                shelf_id, captured_at, inventory_percent, days_until_expiration,
                expired_product_detected, sensor_online, status, led_color, confidence, reason)
            VALUES (
                $shelfId, $capturedAt, $inventoryPercent, $daysUntilExpiration,
                $expiredProductDetected, $sensorOnline, $status, $ledColor, $confidence, $reason);
            """;

        command.Parameters.AddWithValue("$shelfId", observation.ShelfId.ToString("D"));
        command.Parameters.AddWithValue("$capturedAt", observation.Observation.CapturedAt.ToString("O"));
        command.Parameters.AddWithValue("$inventoryPercent", observation.Observation.InventoryPercent);
        command.Parameters.AddWithValue("$daysUntilExpiration", observation.Observation.DaysUntilExpiration);
        command.Parameters.AddWithValue("$expiredProductDetected", observation.Observation.ExpiredProductDetected);
        command.Parameters.AddWithValue("$sensorOnline", observation.Observation.SensorOnline);
        command.Parameters.AddWithValue("$status", observation.Decision.Status);
        command.Parameters.AddWithValue("$ledColor", observation.Decision.LedColor);
        command.Parameters.AddWithValue("$confidence", observation.Decision.Confidence);
        command.Parameters.AddWithValue("$reason", observation.Decision.Reason);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LatestShelfObservationDto?> GetLatestAsync(
        Guid shelfId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT captured_at, inventory_percent, days_until_expiration,
                   expired_product_detected, sensor_online, status, led_color, confidence, reason
            FROM shelf_observations
            WHERE shelf_id = $shelfId
            ORDER BY captured_at DESC, id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$shelfId", shelfId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var capturedAt = DateTimeOffset.Parse(
            reader.GetString(0),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

        var observation = new ShelfObservationDto(
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetBoolean(3),
            reader.GetBoolean(4),
            capturedAt);

        var decision = new ShelfDecisionDto(
            shelfId,
            reader.GetString(5),
            reader.GetString(6),
            reader.GetFloat(7),
            reader.GetString(8));

        return new LatestShelfObservationDto(shelfId, observation, decision);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (initialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (initialized)
            {
                return;
            }

            EnsureDatabaseDirectoryExists();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS shelf_observations (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    shelf_id TEXT NOT NULL,
                    captured_at TEXT NOT NULL,
                    inventory_percent INTEGER NOT NULL,
                    days_until_expiration INTEGER NOT NULL,
                    expired_product_detected INTEGER NOT NULL,
                    sensor_online INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    led_color TEXT NOT NULL,
                    confidence REAL NOT NULL,
                    reason TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_shelf_observations_shelf_captured
                    ON shelf_observations (shelf_id, captured_at DESC);
                """;

            await command.ExecuteNonQueryAsync(cancellationToken);
            initialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private void EnsureDatabaseDirectoryExists()
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
}

