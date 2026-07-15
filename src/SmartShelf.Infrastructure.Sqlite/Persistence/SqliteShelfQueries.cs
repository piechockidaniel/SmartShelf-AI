using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Infrastructure.Sqlite.Persistence;

public sealed class SqliteShelfQueries(
    SqliteShelfRepository shelves,
    SqliteResourceCatalogStore catalog,
    SqliteShelfObservationStore observations,
    SqliteAlertStore alerts) : IShelfQueries
{
    public async Task<IReadOnlyList<ShelfDto>> GetShelvesAsync(CancellationToken cancellationToken = default)
    {
        var resources = await GetResourceMapAsync(cancellationToken);
        return [.. (await shelves.GetAllAsync(cancellationToken)).Select(shelf => MapShelf(shelf, resources))];
    }

    public async Task<ShelfConfigurationDto?> GetConfigurationAsync(Guid shelfId, CancellationToken cancellationToken = default)
    {
        var shelf = await shelves.GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null)
        {
            return null;
        }
        var schema = await GetResourceSchemaAsync(cancellationToken);
        var map = schema.ToDictionary(resource => (resource.Kind, resource.Id));
        return new ShelfConfigurationDto(
            MapShelf(shelf, map),
            shelf.Bindings.Select(binding => new ShelfBindingDto(binding.Id, binding.Kind.ToString(), binding.ResourceId)).ToArray(),
            schema);
    }

    public async Task<IReadOnlyList<ResourceNodeDto>> GetResourceSchemaAsync(CancellationToken cancellationToken = default)
    {
        var products = (await catalog.GetProductsAsync(cancellationToken))
            .Select(product => new ResourceNodeDto(product.Id, product.Name, ShelfResourceKind.Product.ToString(), "Products", product.SKU));
        var devices = (await catalog.GetDevicesAsync(cancellationToken))
            .Select(device => new ResourceNodeDto(device.Id, device.Name, device.Kind.ToString(), "Hardware", device.SerialNumber));
        var rules = (await catalog.GetRulesAsync(cancellationToken))
            .Select(rule => new ResourceNodeDto(rule.Id, rule.Name, ShelfResourceKind.EvaluationRule.ToString(), "Evaluation rules"));
        return products.Concat(devices).Concat(rules).ToArray();
    }

    public async Task<IReadOnlyList<ShelfOverviewDto>> GetOverviewsAsync(CancellationToken cancellationToken = default)
    {
        var allShelves = await shelves.GetAllAsync(cancellationToken);
        var schema = await GetResourceSchemaAsync(cancellationToken);
        var map = schema.ToDictionary(resource => (resource.Kind, resource.Id));
        var openAlerts = await alerts.GetAsync(true, 500, cancellationToken);
        var results = new List<ShelfOverviewDto>(allShelves.Count);
        foreach (var shelf in allShelves)
        {
            results.Add(await MapOverviewAsync(shelf, schema, map, openAlerts, cancellationToken));
        }
        return results;
    }

    public async Task<ShelfOverviewDto?> GetOverviewAsync(Guid shelfId, CancellationToken cancellationToken = default)
    {
        var shelf = await shelves.GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null)
        {
            return null;
        }
        var schema = await GetResourceSchemaAsync(cancellationToken);
        var map = schema.ToDictionary(resource => (resource.Kind, resource.Id));
        var openAlerts = await alerts.GetAsync(true, 500, cancellationToken);
        return await MapOverviewAsync(shelf, schema, map, openAlerts, cancellationToken);
    }

    private async Task<ShelfOverviewDto> MapOverviewAsync(
        Shelf shelf,
        IReadOnlyList<ResourceNodeDto> schema,
        IReadOnlyDictionary<(string Kind, Guid Id), ResourceNodeDto> resourceMap,
        IReadOnlyList<AlertDto> openAlerts,
        CancellationToken cancellationToken)
    {
        var bound = shelf.Bindings
            .Select(binding => resourceMap.GetValueOrDefault((binding.Kind.ToString(), binding.ResourceId)))
            .Where(resource => resource is not null)
            .Cast<ResourceNodeDto>()
            .ToArray();
        return new ShelfOverviewDto(
            MapShelf(shelf, resourceMap), bound,
            await observations.GetLatestAsync(shelf.Id, cancellationToken),
            openAlerts.Where(alert => alert.ShelfId == shelf.Id).ToArray());
    }

    private async Task<Dictionary<(string Kind, Guid Id), ResourceNodeDto>> GetResourceMapAsync(CancellationToken cancellationToken)
        => (await GetResourceSchemaAsync(cancellationToken)).ToDictionary(resource => (resource.Kind, resource.Id));

    private static ShelfDto MapShelf(
        Shelf shelf,
        IReadOnlyDictionary<(string Kind, Guid Id), ResourceNodeDto> resources)
    {
        string? External(ShelfResourceKind kind) => shelf.Bindings
            .Where(binding => binding.Kind == kind)
            .Select(binding => resources.GetValueOrDefault((binding.Kind.ToString(), binding.ResourceId))?.ExternalKey)
            .FirstOrDefault(value => value is not null);
        return new ShelfDto(
            shelf.Id, shelf.Name, shelf.Location.Warehouse, shelf.Location.Aisle,
            shelf.Location.Shelf, shelf.Location.Position,
            External(ShelfResourceKind.Controller), External(ShelfResourceKind.Camera),
            shelf.Enabled, shelf.CreatedAt, shelf.UpdatedAt, shelf.Version);
    }
}
