using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Application.Exceptions;
using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Infrastructure.InMemory.Persistence;

public sealed class InMemoryPersistenceStore :
    IShelfRepository,
    IShelfQueries,
    IResourceCatalogStore,
    IShelfObservationStore,
    IObservationCommandStore,
    IAlertStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Shelf> _shelves = [];
    private readonly Dictionary<Guid, Product> _products = [];
    private readonly Dictionary<Guid, Device> _devices = [];
    private readonly Dictionary<Guid, EvaluationRule> _rules = [];
    private readonly Dictionary<Guid, List<LatestShelfObservationDto>> _observations = [];
    private readonly Dictionary<Guid, AlertDto> _alerts = [];

    public InMemoryPersistenceStore() => Seed();

    public Task<Shelf?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_shelves.TryGetValue(id, out var shelf) ? Clone(shelf) : null);
        }
    }

    public Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Shelf>>(_shelves.Values.Select(Clone).OrderBy(shelf => shelf.Name).ToArray());
        }
    }

    public Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_shelves.TryAdd(shelf.Id, Clone(shelf)))
            {
                throw new InvalidOperationException($"Shelf '{shelf.Id:D}' already exists.");
            }
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Shelf shelf, CancellationToken cancellationToken = default)
        => UpdateAsync(shelf, Math.Max(1, shelf.Version - 1), cancellationToken);

    public Task UpdateAsync(Shelf shelf, int expectedVersion, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_shelves.TryGetValue(shelf.Id, out var current) || current.Version != expectedVersion)
            {
                throw new PersistenceConcurrencyException($"Shelf '{shelf.Id:D}' was modified by another request.");
            }
            _shelves[shelf.Id] = Clone(shelf);
        }
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var shelf = await GetByIdAsync(id, cancellationToken);
        if (shelf is not null)
        {
            await DeleteAsync(id, shelf.Version, cancellationToken);
        }
    }

    public Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_shelves.TryGetValue(id, out var current) || current.Version != expectedVersion)
            {
                throw new PersistenceConcurrencyException($"Shelf '{id:D}' was modified or deleted.");
            }
            _shelves.Remove(id);
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasOperationalHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _observations.TryGetValue(id, out var history) && history.Count > 0 ||
                _alerts.Values.Any(alert => alert.ShelfId == id));
        }
    }

    public Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Product>>(_products.Values.Select(Clone).OrderBy(product => product.Name).ToArray());
        }
    }

    public Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Device>>(_devices.Values.Select(Clone).OrderBy(device => device.Kind).ThenBy(device => device.Name).ToArray());
        }
    }

    public Task<IReadOnlyList<EvaluationRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<EvaluationRule>>(_rules.Values.Select(Clone).OrderByDescending(rule => rule.Priority).ToArray());
        }
    }

    public Task<IReadOnlyList<EvaluationRule>> GetRulesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var wanted = ids.ToHashSet();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<EvaluationRule>>(
                _rules.Values.Where(rule => wanted.Contains(rule.Id)).Select(Clone).OrderByDescending(rule => rule.Priority).ToArray());
        }
    }

    public Task SaveProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        lock (_gate) _products[product.Id] = Clone(product);
        return Task.CompletedTask;
    }

    public Task SaveDeviceAsync(Device device, CancellationToken cancellationToken = default)
    {
        lock (_gate) _devices[device.Id] = Clone(device);
        return Task.CompletedTask;
    }

    public Task SaveRuleAsync(EvaluationRule rule, CancellationToken cancellationToken = default)
    {
        lock (_gate) _rules[rule.Id] = Clone(rule);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(ShelfResourceKind kind, Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_shelves.Values.Any(shelf => shelf.Bindings.Any(binding => binding.Kind == kind && binding.ResourceId == id)))
            {
                return Task.FromResult(false);
            }
            var removed = kind switch
            {
                ShelfResourceKind.Product => _products.Remove(id),
                ShelfResourceKind.EvaluationRule => _rules.Remove(id),
                _ => _devices.Remove(id)
            };
            return Task.FromResult(removed);
        }
    }

    public Task<bool> ExistsAsync(ShelfResourceKind kind, Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var exists = kind switch
            {
                ShelfResourceKind.Product => _products.ContainsKey(id),
                ShelfResourceKind.EvaluationRule => _rules.ContainsKey(id),
                _ => _devices.TryGetValue(id, out var device) && device.Kind.ToString() == kind.ToString()
            };
            return Task.FromResult(exists);
        }
    }

    public Task SaveAsync(LatestShelfObservationDto observation, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            GetHistory(observation.ShelfId).Add(observation);
        }
        return Task.CompletedTask;
    }

    public Task RecordAsync(
        LatestShelfObservationDto observation,
        AlertSeverity? severity,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            GetHistory(observation.ShelfId).Add(observation);
            var open = _alerts.Values.FirstOrDefault(alert => alert.ShelfId == observation.ShelfId && alert.Status != "Resolved");
            if (severity is null)
            {
                if (open is not null)
                {
                    _alerts[open.Id] = open with { Status = "Resolved", ResolvedAt = observation.Observation.CapturedAt };
                }
            }
            else if (open is null)
            {
                var id = Guid.NewGuid();
                _alerts[id] = new AlertDto(
                    id, observation.ShelfId, severity.Value.ToString(), "Active", observation.Decision.Reason,
                    1, observation.Observation.CapturedAt, observation.Observation.CapturedAt, null, null);
            }
            else
            {
                _alerts[open.Id] = open with
                {
                    Severity = severity.Value.ToString(), Message = observation.Decision.Reason,
                    Occurrences = open.Occurrences + 1, LastOccurredAt = observation.Observation.CapturedAt
                };
            }
        }
        return Task.CompletedTask;
    }

    public Task<LatestShelfObservationDto?> GetLatestAsync(Guid shelfId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var latest = _observations.GetValueOrDefault(shelfId)?.OrderByDescending(item => item.Observation.CapturedAt).FirstOrDefault();
            return Task.FromResult(latest);
        }
    }

    public Task<IReadOnlyList<LatestShelfObservationDto>> GetRecentAsync(Guid shelfId, int limit, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var items = (_observations.GetValueOrDefault(shelfId) ?? [])
                .OrderByDescending(item => item.Observation.CapturedAt).Take(Math.Clamp(limit, 1, 500)).ToArray();
            return Task.FromResult<IReadOnlyList<LatestShelfObservationDto>>(items);
        }
    }

    public Task<IReadOnlyList<ShelfSummaryDto>> GetSummariesAsync(int limit, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var result = _observations
                .Where(pair => pair.Value.Count > 0)
                .Select(pair => new ShelfSummaryDto(
                    pair.Key, pair.Value.Count, pair.Value.OrderByDescending(item => item.Observation.CapturedAt).First()))
                .OrderByDescending(summary => summary.Latest.Observation.CapturedAt)
                .Take(Math.Clamp(limit, 1, 500)).ToArray();
            return Task.FromResult<IReadOnlyList<ShelfSummaryDto>>(result);
        }
    }

    public Task<AlertDto> UpsertOpenAsync(Guid shelfId, AlertSeverity severity, string message, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var open = _alerts.Values.FirstOrDefault(alert => alert.ShelfId == shelfId && alert.Status != "Resolved");
            if (open is null)
            {
                var id = Guid.NewGuid();
                open = new AlertDto(id, shelfId, severity.ToString(), "Active", message, 1, occurredAt, occurredAt, null, null);
            }
            else
            {
                open = open with { Severity = severity.ToString(), Message = message, Occurrences = open.Occurrences + 1, LastOccurredAt = occurredAt };
            }
            _alerts[open.Id] = open;
            return Task.FromResult(open);
        }
    }

    public Task ResolveOpenAsync(Guid shelfId, DateTimeOffset resolvedAt, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            foreach (var alert in _alerts.Values.Where(alert => alert.ShelfId == shelfId && alert.Status != "Resolved").ToArray())
            {
                _alerts[alert.Id] = alert with { Status = "Resolved", ResolvedAt = resolvedAt };
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AlertDto>> GetAsync(bool openOnly, int limit, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var result = _alerts.Values.Where(alert => !openOnly || alert.Status != "Resolved")
                .OrderByDescending(alert => alert.LastOccurredAt).Take(Math.Clamp(limit, 1, 500)).ToArray();
            return Task.FromResult<IReadOnlyList<AlertDto>>(result);
        }
    }

    public Task<AlertDto?> AcknowledgeAsync(Guid alertId, DateTimeOffset acknowledgedAt, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_alerts.TryGetValue(alertId, out var alert) || alert.Status == "Resolved")
            {
                return Task.FromResult<AlertDto?>(null);
            }
            var updated = alert with { Status = "Acknowledged", AcknowledgedAt = acknowledgedAt };
            _alerts[alertId] = updated;
            return Task.FromResult<AlertDto?>(updated);
        }
    }

    public async Task<IReadOnlyList<ShelfDto>> GetShelvesAsync(CancellationToken cancellationToken = default)
        => [.. (await GetAllAsync(cancellationToken)).Select(shelf => MapShelf(shelf, ResourceMap()))];

    public async Task<ShelfConfigurationDto?> GetConfigurationAsync(Guid shelfId, CancellationToken cancellationToken = default)
    {
        var shelf = await GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null) return null;
        var schema = await GetResourceSchemaAsync(cancellationToken);
        return new ShelfConfigurationDto(
            MapShelf(shelf, schema.ToDictionary(resource => (resource.Kind, resource.Id))),
            shelf.Bindings.Select(binding => new ShelfBindingDto(binding.Id, binding.Kind.ToString(), binding.ResourceId)).ToArray(), schema);
    }

    public Task<IReadOnlyList<ResourceNodeDto>> GetResourceSchemaAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<ResourceNodeDto>>(ResourceMap().Values.ToArray());
        }
    }

    public async Task<IReadOnlyList<ShelfOverviewDto>> GetOverviewsAsync(CancellationToken cancellationToken = default)
    {
        var shelves = await GetAllAsync(cancellationToken);
        var result = new List<ShelfOverviewDto>();
        foreach (var shelf in shelves)
        {
            result.Add((await GetOverviewAsync(shelf.Id, cancellationToken))!);
        }
        return result;
    }

    public async Task<ShelfOverviewDto?> GetOverviewAsync(Guid shelfId, CancellationToken cancellationToken = default)
    {
        var shelf = await GetByIdAsync(shelfId, cancellationToken);
        if (shelf is null) return null;
        var map = ResourceMap();
        var resources = shelf.Bindings.Select(binding => map.GetValueOrDefault((binding.Kind.ToString(), binding.ResourceId)))
            .Where(resource => resource is not null).Cast<ResourceNodeDto>().ToArray();
        return new ShelfOverviewDto(
            MapShelf(shelf, map), resources, await GetLatestAsync(shelfId, cancellationToken),
            (await GetAsync(true, 500, cancellationToken)).Where(alert => alert.ShelfId == shelfId).ToArray());
    }

    private Dictionary<(string Kind, Guid Id), ResourceNodeDto> ResourceMap()
    {
        lock (_gate)
        {
            return _products.Values.Select(product => new ResourceNodeDto(product.Id, product.Name, "Product", "Products", product.SKU))
                .Concat(_devices.Values.Select(device => new ResourceNodeDto(device.Id, device.Name, device.Kind.ToString(), "Hardware", device.SerialNumber)))
                .Concat(_rules.Values.Select(rule => new ResourceNodeDto(rule.Id, rule.Name, "EvaluationRule", "Evaluation rules")))
                .ToDictionary(resource => (resource.Kind, resource.Id));
        }
    }

    private static ShelfDto MapShelf(Shelf shelf, IReadOnlyDictionary<(string Kind, Guid Id), ResourceNodeDto> resources)
    {
        string? External(ShelfResourceKind kind) => shelf.Bindings.Where(binding => binding.Kind == kind)
            .Select(binding => resources.GetValueOrDefault((binding.Kind.ToString(), binding.ResourceId))?.ExternalKey).FirstOrDefault(value => value is not null);
        return new ShelfDto(
            shelf.Id, shelf.Name, shelf.Location.Warehouse, shelf.Location.Aisle, shelf.Location.Shelf,
            shelf.Location.Position, External(ShelfResourceKind.Controller), External(ShelfResourceKind.Camera),
            shelf.Enabled, shelf.CreatedAt, shelf.UpdatedAt, shelf.Version);
    }

    private List<LatestShelfObservationDto> GetHistory(Guid shelfId)
    {
        if (!_observations.TryGetValue(shelfId, out var history))
        {
            history = [];
            _observations[shelfId] = history;
        }
        return history;
    }

    private void Seed()
    {
        var resources = new Device[]
        {
            new("Edge controller 001", "edge-controller-001", DeviceKind.Controller),
            new("Shelf camera 001", "camera-001", DeviceKind.Camera),
            new("Inventory sensor 001", "sensor-001", DeviceKind.Sensor),
            new("LED strip 001", "led-001", DeviceKind.LedOutput)
        };
        foreach (var device in resources) _devices[device.Id] = device;
        var product = new Product("DEMO-001", "Demo product", 25, DateTime.UtcNow.AddDays(30));
        _products[product.Id] = product;
        var rule = new EvaluationRule("Critical low stock", RuleMetric.InventoryPercent,
            RuleOperator.LessThanOrEqual, 10, ShelfStatus.Critical, LedColor.Red, 100);
        _rules[rule.Id] = rule;
    }

    private static Shelf Clone(Shelf shelf)
        => Shelf.Restore(shelf.Id, shelf.Name, shelf.Location, shelf.Enabled, shelf.Version,
            shelf.CreatedAt, shelf.UpdatedAt,
            shelf.Bindings.Select(binding => ShelfResourceBinding.Restore(binding.Id, binding.Kind, binding.ResourceId)));
    private static Product Clone(Product product)
        => Product.Restore(product.Id, product.SKU, product.Name, product.Quantity, product.ExpirationDate, product.CreatedAt, product.UpdatedAt);
    private static Device Clone(Device device)
        => Device.Restore(device.Id, device.Name, device.SerialNumber, device.Kind, device.Status, device.LastSeen, device.CreatedAt, device.UpdatedAt);
    private static EvaluationRule Clone(EvaluationRule rule)
        => EvaluationRule.Restore(rule.Id, rule.Name, rule.Metric, rule.Operator, rule.Threshold,
            rule.ResultStatus, rule.LedColor, rule.Priority, rule.CreatedAt, rule.UpdatedAt);
}
