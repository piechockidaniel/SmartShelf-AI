using System.Globalization;
using Dapper;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Exceptions;
using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;

namespace SmartShelf.Infrastructure.Sqlite.Persistence;

public sealed class SqliteShelfRepository : IShelfRepository
{
    private readonly SqliteDatabase _database;

    public SqliteShelfRepository(string connectionString) => _database = new(connectionString);

    public SqliteShelfRepository(SqliteDatabase database) => _database = database;

    public async Task<Shelf?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ShelfRow>(new CommandDefinition(
            ShelfSql + " WHERE id = @id LIMIT 1;", new { id = id.ToString("D") }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var bindings = await ReadBindingsAsync(connection, new[] { row.Id }, cancellationToken);
        return Map(row, bindings.GetValueOrDefault(row.Id, []));
    }

    public async Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<ShelfRow>(new CommandDefinition(
            ShelfSql + " ORDER BY warehouse, aisle, shelf_code, position, name;",
            cancellationToken: cancellationToken))).ToArray();
        var bindings = await ReadBindingsAsync(connection, rows.Select(row => row.Id), cancellationToken);
        return rows.Select(row => Map(row, bindings.GetValueOrDefault(row.Id, []))).ToArray();
    }

    public async Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO shelves(
                id, name, warehouse, aisle, shelf_code, position, device_id, camera_device,
                enabled, version, created_at, updated_at)
            VALUES(
                @Id, @Name, @Warehouse, @Aisle, @ShelfCode, @Position, NULL, NULL,
                @Enabled, @Version, @CreatedAt, @UpdatedAt);
            """,
            Parameters(shelf), transaction, cancellationToken: cancellationToken));
        await WriteBindingsAsync(connection, transaction, shelf, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task UpdateAsync(Shelf shelf, CancellationToken cancellationToken = default)
        => UpdateAsync(shelf, Math.Max(1, shelf.Version - 1), cancellationToken);

    public async Task UpdateAsync(
        Shelf shelf,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE shelves SET
                name=@Name, warehouse=@Warehouse, aisle=@Aisle, shelf_code=@ShelfCode,
                position=@Position, enabled=@Enabled, version=@Version, updated_at=@UpdatedAt
            WHERE id=@Id AND version=@ExpectedVersion;
            """,
            new
            {
                shelf.Name,
                Warehouse = shelf.Location.Warehouse,
                Aisle = shelf.Location.Aisle,
                ShelfCode = shelf.Location.Shelf,
                shelf.Location.Position,
                shelf.Enabled,
                shelf.Version,
                UpdatedAt = Timestamp(shelf.UpdatedAt),
                Id = shelf.Id.ToString("D"),
                ExpectedVersion = expectedVersion
            }, transaction, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new PersistenceConcurrencyException(
                $"Shelf '{shelf.Id:D}' was modified by another request.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM shelf_resource_bindings WHERE shelf_id=@id;",
            new { id = shelf.Id.ToString("D") }, transaction, cancellationToken: cancellationToken));
        await WriteBindingsAsync(connection, transaction, shelf, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var shelf = await GetByIdAsync(id, cancellationToken);
        if (shelf is not null)
        {
            await DeleteAsync(id, shelf.Version, cancellationToken);
        }
    }

    public async Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM shelves WHERE id=@id AND version=@expectedVersion;",
            new { id = id.ToString("D"), expectedVersion }, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            throw new PersistenceConcurrencyException($"Shelf '{id:D}' was modified or deleted.");
        }
    }

    public async Task<bool> HasOperationalHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*) FROM shelf_observations WHERE shelf_id=@id) +
                (SELECT COUNT(*) FROM alerts WHERE shelf_id=@id);
            """,
            new { id = id.ToString("D") }, cancellationToken: cancellationToken));
        return count > 0;
    }

    private static async Task<Dictionary<string, IReadOnlyList<ShelfResourceBinding>>> ReadBindingsAsync(
        System.Data.IDbConnection connection,
        IEnumerable<string> shelfIds,
        CancellationToken cancellationToken)
    {
        var ids = shelfIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var rows = await connection.QueryAsync<BindingRow>(new CommandDefinition(
            "SELECT id Id, shelf_id ShelfId, kind Kind, resource_id ResourceId FROM shelf_resource_bindings WHERE shelf_id IN @ids;",
            new { ids }, cancellationToken: cancellationToken));
        return rows.GroupBy(row => row.ShelfId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<ShelfResourceBinding>)group.Select(MapBinding).ToArray());
    }

    private static async Task WriteBindingsAsync(
        System.Data.IDbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Shelf shelf,
        CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO shelf_resource_bindings(id, shelf_id, kind, resource_id) VALUES (@Id, @ShelfId, @Kind, @ResourceId);";
        foreach (var binding in shelf.Bindings)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = binding.Id.ToString("D"),
                ShelfId = shelf.Id.ToString("D"),
                Kind = binding.Kind.ToString(),
                ResourceId = binding.ResourceId.ToString("D")
            }, transaction, cancellationToken: cancellationToken));
        }
    }

    private static Shelf Map(ShelfRow row, IReadOnlyList<ShelfResourceBinding> bindings)
        => Shelf.Restore(
            Guid.Parse(row.Id), row.Name,
            new ShelfLocation(row.Warehouse, row.Aisle, row.ShelfCode, row.Position),
            row.Enabled != 0, checked((int)row.Version), ParseDate(row.CreatedAt), ParseNullableDate(row.UpdatedAt), bindings);

    private static ShelfResourceBinding MapBinding(BindingRow row)
        => ShelfResourceBinding.Restore(
            Guid.Parse(row.Id), Enum.Parse<ShelfResourceKind>(row.Kind), Guid.Parse(row.ResourceId));

    private static object Parameters(Shelf shelf) => new
    {
        Id = shelf.Id.ToString("D"),
        shelf.Name,
        Warehouse = shelf.Location.Warehouse,
        Aisle = shelf.Location.Aisle,
        ShelfCode = shelf.Location.Shelf,
        shelf.Location.Position,
        shelf.Enabled,
        shelf.Version,
        CreatedAt = shelf.CreatedAt.ToUniversalTime().ToString("O"),
        UpdatedAt = Timestamp(shelf.UpdatedAt)
    };

    private static string? Timestamp(DateTime? value) => value?.ToUniversalTime().ToString("O");
    private static DateTime ParseDate(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static DateTime? ParseNullableDate(string? value) => value is null ? null : ParseDate(value);

    private const string ShelfSql =
        "SELECT id Id, name Name, warehouse Warehouse, aisle Aisle, shelf_code ShelfCode, position Position, enabled Enabled, version Version, created_at CreatedAt, updated_at UpdatedAt FROM shelves";

    private sealed record ShelfRow(
        string Id, string Name, string Warehouse, string Aisle, string ShelfCode, string Position,
        long Enabled, long Version, string CreatedAt, string? UpdatedAt);
    private sealed record BindingRow(string Id, string ShelfId, string Kind, string ResourceId);
}
