using Microsoft.Extensions.DependencyInjection;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Application.Exceptions;
using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;
using SmartShelf.Domain.ValueObjects;
using SmartShelf.Infrastructure.InMemory.DependencyInjection;
using SmartShelf.Infrastructure.Sqlite.DependencyInjection;
using Xunit;

namespace SmartShelf.IntegrationTests;

public sealed class PersistenceProviderContractTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("inmemory")]
    public async Task Providers_support_versioned_bindings_catalogs_and_composite_queries(string provider)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"smartshelf-provider-{Guid.NewGuid():N}.db");
        var ct = TestContext.Current.CancellationToken;
        try
        {
            await using var services = Build(provider, databasePath);
            var shelves = services.GetRequiredService<IShelfRepository>();
            var catalog = services.GetRequiredService<IResourceCatalogStore>();
            var queries = services.GetRequiredService<IShelfQueries>();
            var observations = services.GetRequiredService<IObservationCommandStore>();

            var controller = new Device("Controller", $"controller-{Guid.NewGuid():N}", DeviceKind.Controller);
            var product = new Product($"SKU-{Guid.NewGuid():N}", "Product", 4, DateTime.UtcNow.AddDays(10));
            await catalog.SaveDeviceAsync(controller, ct);
            await catalog.SaveProductAsync(product, ct);

            var shelf = new Shelf("Contract shelf", new ShelfLocation("WH", "A", "S", "P"));
            await shelves.AddAsync(shelf, ct);
            var expectedVersion = shelf.Version;
            shelf.ReplaceBindings([
                new ShelfResourceBinding(ShelfResourceKind.Controller, controller.Id),
                new ShelfResourceBinding(ShelfResourceKind.Product, product.Id)
            ]);
            await shelves.UpdateAsync(shelf, expectedVersion, ct);

            await Assert.ThrowsAsync<PersistenceConcurrencyException>(() => shelves.UpdateAsync(shelf, expectedVersion, ct));

            var observation = new LatestShelfObservationDto(
                shelf.Id,
                new ShelfObservationDto(5, 10, false, true, DateTimeOffset.UtcNow),
                new ShelfDecisionDto(shelf.Id, "Critical", "Red", .98f, "Low stock"));
            await observations.RecordAsync(observation, AlertSeverity.Critical, ct);

            var configuration = await queries.GetConfigurationAsync(shelf.Id, ct);
            var overview = await queries.GetOverviewAsync(shelf.Id, ct);
            Assert.NotNull(configuration);
            Assert.Equal(2, configuration.Bindings.Count);
            Assert.NotNull(overview?.LatestObservation);
            Assert.Single(overview!.OpenAlerts);
            Assert.True(await shelves.HasOperationalHistoryAsync(shelf.Id, ct));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static ServiceProvider Build(string provider, string databasePath)
    {
        var services = new ServiceCollection();
        if (provider == "sqlite") services.AddSqlitePersistence($"Data Source={databasePath};Pooling=False");
        else services.AddInMemoryPersistence();
        return services.BuildServiceProvider();
    }
}
