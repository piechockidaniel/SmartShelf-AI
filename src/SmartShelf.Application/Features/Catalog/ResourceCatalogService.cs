using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Application.Features.Catalog;

public sealed class ResourceCatalogService(IResourceCatalogStore store)
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
        => [.. (await store.GetProductsAsync(cancellationToken)).Select(Map)];

    public async Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default)
        => [.. (await store.GetDevicesAsync(cancellationToken)).Select(Map)];

    public async Task<IReadOnlyList<EvaluationRuleDto>> GetRulesAsync(CancellationToken cancellationToken = default)
        => [.. (await store.GetRulesAsync(cancellationToken)).Select(Map)];

    public async Task<ProductDto> SaveProductAsync(SaveProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product(request.Sku, request.Name, request.Quantity, request.ExpirationDate);
        await store.SaveProductAsync(product, cancellationToken);
        return Map(product);
    }

    public async Task<ProductDto> SaveProductAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = Product.Restore(id, request.Sku, request.Name, request.Quantity,
            request.ExpirationDate, DateTime.UtcNow, DateTime.UtcNow);
        await store.SaveProductAsync(product, cancellationToken);
        return Map(product);
    }

    public async Task<DeviceDto> SaveDeviceAsync(SaveDeviceRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<DeviceKind>(request.Kind, true, out var kind))
        {
            throw new ArgumentException($"Unknown device kind '{request.Kind}'.");
        }
        var device = new Device(request.Name, request.SerialNumber, kind);
        await store.SaveDeviceAsync(device, cancellationToken);
        return Map(device);
    }

    public async Task<DeviceDto> SaveDeviceAsync(Guid id, SaveDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var kind = Enum.Parse<DeviceKind>(request.Kind, true);
        var device = Device.Restore(id, request.Name, request.SerialNumber, kind,
            DeviceStatus.Online, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
        await store.SaveDeviceAsync(device, cancellationToken);
        return Map(device);
    }

    public async Task<EvaluationRuleDto> SaveRuleAsync(
        SaveEvaluationRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = new EvaluationRule(
            request.Name,
            Enum.Parse<RuleMetric>(request.Metric, true),
            Enum.Parse<RuleOperator>(request.Operator, true),
            request.Threshold,
            Enum.Parse<ShelfStatus>(request.ResultStatus, true),
            Enum.Parse<LedColor>(request.LedColor, true),
            request.Priority);
        await store.SaveRuleAsync(rule, cancellationToken);
        return Map(rule);
    }

    public async Task<EvaluationRuleDto> SaveRuleAsync(
        Guid id, SaveEvaluationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = EvaluationRule.Restore(
            id, request.Name, Enum.Parse<RuleMetric>(request.Metric, true),
            Enum.Parse<RuleOperator>(request.Operator, true), request.Threshold,
            Enum.Parse<ShelfStatus>(request.ResultStatus, true),
            Enum.Parse<LedColor>(request.LedColor, true), request.Priority,
            DateTime.UtcNow, DateTime.UtcNow);
        await store.SaveRuleAsync(rule, cancellationToken);
        return Map(rule);
    }

    public Task<bool> DeleteAsync(ShelfResourceKind kind, Guid id, CancellationToken cancellationToken = default)
        => store.DeleteAsync(kind, id, cancellationToken);

    private static ProductDto Map(Product product)
        => new(product.Id, product.SKU, product.Name, product.Quantity, product.ExpirationDate,
            product.CreatedAt, product.UpdatedAt);

    private static DeviceDto Map(Device device)
        => new(device.Id, device.Name, device.SerialNumber, device.Kind.ToString(), device.Status.ToString(),
            device.LastSeen, device.CreatedAt, device.UpdatedAt);

    private static EvaluationRuleDto Map(EvaluationRule rule)
        => new(rule.Id, rule.Name, rule.Metric.ToString(), rule.Operator.ToString(), rule.Threshold,
            rule.ResultStatus.ToString(), rule.LedColor.ToString(), rule.Priority,
            rule.CreatedAt, rule.UpdatedAt);
}
