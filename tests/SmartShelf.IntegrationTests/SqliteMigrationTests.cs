using Microsoft.Data.Sqlite;
using SmartShelf.Infrastructure.Sqlite.Persistence;
using SmartShelf.Domain.Enums;
using Xunit;

namespace SmartShelf.IntegrationTests;

public sealed class SqliteMigrationTests
{
    [Fact]
    public async Task Legacy_shelf_columns_are_preserved_and_converted_to_bindings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"smartshelf-legacy-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        var shelfId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(ct);
                var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE shelves(
                        id TEXT PRIMARY KEY, name TEXT NOT NULL, warehouse TEXT NOT NULL,
                        aisle TEXT NOT NULL, shelf_code TEXT NOT NULL, position TEXT NOT NULL,
                        device_id TEXT NULL, camera_device TEXT NULL, enabled INTEGER NOT NULL,
                        created_at TEXT NOT NULL, updated_at TEXT NULL);
                    INSERT INTO shelves VALUES($id, 'Legacy', 'WH', 'A', 'S', 'P',
                        'controller-old', 'camera-old', 1, $createdAt, NULL);
                    """;
                command.Parameters.AddWithValue("$id", shelfId.ToString("D"));
                command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync(ct);
            }

            var repository = new SqliteShelfRepository(connectionString);
            var shelf = await repository.GetByIdAsync(shelfId, ct);
            Assert.NotNull(shelf);
            Assert.Equal(1, shelf.Version);
            Assert.Contains(shelf.Bindings, binding => binding.Kind == ShelfResourceKind.Controller);
            Assert.Contains(shelf.Bindings, binding => binding.Kind == ShelfResourceKind.Camera);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
