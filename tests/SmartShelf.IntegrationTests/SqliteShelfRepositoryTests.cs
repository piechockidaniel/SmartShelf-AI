using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;
using SmartShelf.Infrastructure.Sqlite.Persistence;
using Xunit;

namespace SmartShelf.IntegrationTests;

public sealed class SqliteShelfRepositoryTests
{
    [Fact]
    public async Task Shelf_configuration_supports_full_persistent_crud()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"smartshelf-config-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            var writer = new SqliteShelfRepository(connectionString);
            var shelf = new Shelf("Produce A", new ShelfLocation("WH-1", "A1", "S1", "P1"));
            var controllerId = Guid.NewGuid();
            shelf.ReplaceBindings([new ShelfResourceBinding(ShelfResourceKind.Controller, controllerId)]);
            await writer.AddAsync(shelf, cancellationToken);

            var reader = new SqliteShelfRepository(connectionString);
            var loaded = await reader.GetByIdAsync(shelf.Id, cancellationToken);
            Assert.NotNull(loaded);
            Assert.Contains(loaded.Bindings, binding => binding.ResourceId == controllerId);
            var expectedVersion = loaded.Version;

            loaded.UpdateConfiguration(
                "Produce B", new ShelfLocation("WH-1", "A2", "S2", "P2"));
            loaded.SetEnabled(false);
            await reader.UpdateAsync(loaded, expectedVersion, cancellationToken);

            var all = await reader.GetAllAsync(cancellationToken);
            Assert.Single(all);
            Assert.Equal("Produce B", all[0].Name);
            Assert.False(all[0].Enabled);

            await reader.DeleteAsync(shelf.Id, all[0].Version, cancellationToken);
            Assert.Null(await reader.GetByIdAsync(shelf.Id, cancellationToken));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
