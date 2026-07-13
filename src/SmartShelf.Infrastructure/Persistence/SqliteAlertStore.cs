using System.Globalization;
using Microsoft.Data.Sqlite;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Infrastructure.Persistence;

public sealed class SqliteAlertStore : IAlertStore
{
    private readonly string connectionString;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private bool initialized;

    public SqliteAlertStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public async Task<AlertDto> UpsertOpenAsync(
        Guid shelfId, AlertSeverity severity, string message, DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var alertId = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO alerts (
                id, shelf_id, severity, status, message, occurrences, created_at, last_occurred_at)
            VALUES ($id, $shelfId, $severity, 'Active', $message, 1, $occurredAt, $occurredAt)
            ON CONFLICT(shelf_id) WHERE status <> 'Resolved'
            DO UPDATE SET
                severity = excluded.severity,
                message = excluded.message,
                occurrences = alerts.occurrences + 1,
                last_occurred_at = excluded.last_occurred_at;
            """;
        command.Parameters.AddWithValue("$id", alertId.ToString("D"));
        command.Parameters.AddWithValue("$shelfId", shelfId.ToString("D"));
        command.Parameters.AddWithValue("$severity", severity.ToString());
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$occurredAt", occurredAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return (await GetOpenForShelfAsync(connection, shelfId, cancellationToken))!;
    }

    public async Task ResolveOpenAsync(
        Guid shelfId, DateTimeOffset resolvedAt, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE alerts
            SET status = 'Resolved', resolved_at = $resolvedAt
            WHERE shelf_id = $shelfId AND status <> 'Resolved';
            """;
        command.Parameters.AddWithValue("$shelfId", shelfId.ToString("D"));
        command.Parameters.AddWithValue("$resolvedAt", resolvedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertDto>> GetAsync(
        bool openOnly, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = openOnly
            ? "SELECT id, shelf_id, severity, status, message, occurrences, created_at, last_occurred_at, acknowledged_at, resolved_at FROM alerts WHERE status <> 'Resolved' ORDER BY last_occurred_at DESC LIMIT $limit;"
            : "SELECT id, shelf_id, severity, status, message, occurrences, created_at, last_occurred_at, acknowledged_at, resolved_at FROM alerts ORDER BY last_occurred_at DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));

        var alerts = new List<AlertDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) alerts.Add(ReadAlert(reader));
        return alerts;
    }

    public async Task<AlertDto?> AcknowledgeAsync(
        Guid alertId, DateTimeOffset acknowledgedAt, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE alerts
            SET status = 'Acknowledged', acknowledged_at = $acknowledgedAt
            WHERE id = $id AND status <> 'Resolved';
            """;
        command.Parameters.AddWithValue("$id", alertId.ToString("D"));
        command.Parameters.AddWithValue("$acknowledgedAt", acknowledgedAt.ToString("O"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) return null;

        return await GetByIdAsync(connection, alertId, cancellationToken);
    }

    private async Task<AlertDto?> GetOpenForShelfAsync(
        SqliteConnection connection, Guid shelfId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, shelf_id, severity, status, message, occurrences, created_at, last_occurred_at, acknowledged_at, resolved_at FROM alerts WHERE shelf_id = $shelfId AND status <> 'Resolved' LIMIT 1;";
        command.Parameters.AddWithValue("$shelfId", shelfId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAlert(reader) : null;
    }

    private async Task<AlertDto?> GetByIdAsync(
        SqliteConnection connection, Guid alertId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, shelf_id, severity, status, message, occurrences, created_at, last_occurred_at, acknowledged_at, resolved_at FROM alerts WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", alertId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAlert(reader) : null;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (initialized) return;
        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (initialized) return;
            EnsureDatabaseDirectoryExists();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS alerts (
                    id TEXT PRIMARY KEY,
                    shelf_id TEXT NOT NULL,
                    severity TEXT NOT NULL,
                    status TEXT NOT NULL,
                    message TEXT NOT NULL,
                    occurrences INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    last_occurred_at TEXT NOT NULL,
                    acknowledged_at TEXT NULL,
                    resolved_at TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_alerts_open_shelf
                    ON alerts (shelf_id) WHERE status <> 'Resolved';
                CREATE INDEX IF NOT EXISTS ix_alerts_status_last_occurred
                    ON alerts (status, last_occurred_at DESC);
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
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:") return;
        var directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (directory is not null) Directory.CreateDirectory(directory);
    }

    private static AlertDto ReadAlert(SqliteDataReader reader)
    {
        return new AlertDto(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            ParseTimestamp(reader.GetString(6)),
            ParseTimestamp(reader.GetString(7)),
            reader.IsDBNull(8) ? null : ParseTimestamp(reader.GetString(8)),
            reader.IsDBNull(9) ? null : ParseTimestamp(reader.GetString(9)));
    }

    private static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
