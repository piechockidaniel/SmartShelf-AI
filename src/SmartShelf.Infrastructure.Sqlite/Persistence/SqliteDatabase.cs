using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Infrastructure.Sqlite.Persistence;

public sealed class SqliteDatabase
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _migrationLock = new(1, 1);
    private bool _initialized;

    public SqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        EnsureDirectory();
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await ConfigureAsync(connection, cancellationToken);
        return connection;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _migrationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await ConfigureAsync(connection, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                "CREATE TABLE IF NOT EXISTS schema_migrations (version TEXT PRIMARY KEY, applied_at TEXT NOT NULL);",
                cancellationToken: cancellationToken));

            await EnsureLegacyShelfColumnsAsync(connection, cancellationToken);

            var assembly = typeof(SqliteDatabase).Assembly;
            foreach (var resource in assembly.GetManifestResourceNames()
                         .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                         .Order(StringComparer.Ordinal))
            {
                var version = resource.Split('.').Reverse().Skip(1).First();
                var exists = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    "SELECT COUNT(*) FROM schema_migrations WHERE version = @version;",
                    new { version }, cancellationToken: cancellationToken));
                if (exists > 0)
                {
                    continue;
                }

                await using var stream = assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"Migration resource '{resource}' was not found.");
                using var reader = new StreamReader(stream);
                var sql = await reader.ReadToEndAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO schema_migrations(version, applied_at) VALUES (@version, @appliedAt);",
                    new { version, appliedAt = DateTimeOffset.UtcNow.ToString("O") }, transaction,
                    cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
            }

            await MigrateLegacyBindingsAsync(connection, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _migrationLock.Release();
        }
    }

    private static async Task EnsureLegacyShelfColumnsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var tableExists = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='shelves';",
            cancellationToken: cancellationToken));
        if (tableExists == 0)
        {
            return;
        }

        var columns = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM pragma_table_info('shelves');", cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!columns.Contains("version"))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE shelves ADD COLUMN version INTEGER NOT NULL DEFAULT 1;",
                cancellationToken: cancellationToken));
        }
        if (!columns.Contains("device_id"))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE shelves ADD COLUMN device_id TEXT NULL;",
                cancellationToken: cancellationToken));
        }
        if (!columns.Contains("camera_device"))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE shelves ADD COLUMN camera_device TEXT NULL;",
                cancellationToken: cancellationToken));
        }
    }

    private static async Task MigrateLegacyBindingsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var shelves = await connection.QueryAsync<LegacyShelf>(new CommandDefinition(
            "SELECT id Id, device_id DeviceId, camera_device CameraDevice FROM shelves WHERE device_id IS NOT NULL OR camera_device IS NOT NULL;",
            cancellationToken: cancellationToken));
        foreach (var shelf in shelves)
        {
            await MigrateLegacyDeviceAsync(connection, shelf.Id, shelf.DeviceId, DeviceKind.Controller, ShelfResourceKind.Controller, cancellationToken);
            await MigrateLegacyDeviceAsync(connection, shelf.Id, shelf.CameraDevice, DeviceKind.Camera, ShelfResourceKind.Camera, cancellationToken);
        }
    }

    private static async Task MigrateLegacyDeviceAsync(
        SqliteConnection connection,
        string shelfId,
        string? externalId,
        DeviceKind deviceKind,
        ShelfResourceKind bindingKind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return;
        }

        var deviceId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT id FROM devices WHERE serial_number = @externalId LIMIT 1;",
            new { externalId }, cancellationToken: cancellationToken));
        if (deviceId is null)
        {
            deviceId = Guid.NewGuid().ToString("D");
            var now = DateTime.UtcNow.ToString("O");
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO devices(id, name, serial_number, kind, status, last_seen, created_at)
                VALUES (@deviceId, @name, @externalId, @kind, 'Online', @now, @now);
                """,
                new { deviceId, name = externalId, externalId, kind = deviceKind.ToString(), now },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT OR IGNORE INTO shelf_resource_bindings(id, shelf_id, kind, resource_id)
            VALUES (@id, @shelfId, @kind, @deviceId);
            """,
            new { id = Guid.NewGuid().ToString("D"), shelfId, kind = bindingKind.ToString(), deviceId },
            cancellationToken: cancellationToken));
    }

    private static Task ConfigureAsync(SqliteConnection connection, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;",
            cancellationToken: cancellationToken));

    private void EnsureDirectory()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:" || builder.Mode == SqliteOpenMode.Memory)
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }

    private sealed record LegacyShelf(string Id, string? DeviceId, string? CameraDevice);
}
