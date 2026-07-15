using System.Globalization;
using Dapper;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Infrastructure.Sqlite.Persistence;

public sealed class SqliteAlertStore : IAlertStore
{
    private readonly SqliteDatabase _database;

    public SqliteAlertStore(string connectionString) => _database = new(connectionString);
    public SqliteAlertStore(SqliteDatabase database) => _database = database;

    public async Task<AlertDto> UpsertOpenAsync(
        Guid shelfId, AlertSeverity severity, string message, DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO alerts(id, shelf_id, severity, status, message, occurrences, created_at, last_occurred_at)
            VALUES (@id, @shelfId, @severity, 'Active', @message, 1, @occurredAt, @occurredAt)
            ON CONFLICT(shelf_id) WHERE status <> 'Resolved'
            DO UPDATE SET severity=excluded.severity, message=excluded.message,
                occurrences=alerts.occurrences + 1, last_occurred_at=excluded.last_occurred_at;
            """,
            new
            {
                id = Guid.NewGuid().ToString("D"), shelfId = shelfId.ToString("D"),
                severity = severity.ToString(), message, occurredAt = occurredAt.ToString("O")
            }, cancellationToken: cancellationToken));
        return (await GetOpenForShelfAsync(connection, shelfId, cancellationToken))!;
    }

    public async Task ResolveOpenAsync(
        Guid shelfId, DateTimeOffset resolvedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE alerts SET status='Resolved', resolved_at=@resolvedAt WHERE shelf_id=@shelfId AND status <> 'Resolved';",
            new { shelfId = shelfId.ToString("D"), resolvedAt = resolvedAt.ToString("O") },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AlertDto>> GetAsync(
        bool openOnly, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var where = openOnly ? " WHERE status <> 'Resolved'" : string.Empty;
        var rows = await connection.QueryAsync<AlertRow>(new CommandDefinition(
            AlertSql + where + " ORDER BY last_occurred_at DESC LIMIT @limit;",
            new { limit = Math.Clamp(limit, 1, 500) }, cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task<AlertDto?> AcknowledgeAsync(
        Guid alertId, DateTimeOffset acknowledgedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE alerts SET status='Acknowledged', acknowledged_at=@at WHERE id=@id AND status <> 'Resolved';",
            new { id = alertId.ToString("D"), at = acknowledgedAt.ToString("O") }, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            return null;
        }

        var row = await connection.QuerySingleAsync<AlertRow>(new CommandDefinition(
            AlertSql + " WHERE id=@id LIMIT 1;", new { id = alertId.ToString("D") }, cancellationToken: cancellationToken));
        return Map(row);
    }

    private static async Task<AlertDto?> GetOpenForShelfAsync(
        System.Data.IDbConnection connection,
        Guid shelfId,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<AlertRow>(new CommandDefinition(
            AlertSql + " WHERE shelf_id=@shelfId AND status <> 'Resolved' LIMIT 1;",
            new { shelfId = shelfId.ToString("D") }, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    private static AlertDto Map(AlertRow row)
        => new(
            Guid.Parse(row.Id), Guid.Parse(row.ShelfId), row.Severity, row.Status, row.Message,
            checked((int)row.Occurrences), Parse(row.CreatedAt), Parse(row.LastOccurredAt),
            ParseNullable(row.AcknowledgedAt), ParseNullable(row.ResolvedAt));

    private static DateTimeOffset Parse(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static DateTimeOffset? ParseNullable(string? value) => value is null ? null : Parse(value);

    private const string AlertSql =
        "SELECT id Id, shelf_id ShelfId, severity Severity, status Status, message Message, occurrences Occurrences, created_at CreatedAt, last_occurred_at LastOccurredAt, acknowledged_at AcknowledgedAt, resolved_at ResolvedAt FROM alerts";

    private sealed record AlertRow(
        string Id, string ShelfId, string Severity, string Status, string Message, long Occurrences,
        string CreatedAt, string LastOccurredAt, string? AcknowledgedAt, string? ResolvedAt);
}
