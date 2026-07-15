using SmartShelf.Domain.Entities;

namespace SmartShelf.Application.Abstractions.Persistence;

public interface IResourceCatalogStore
{
    Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationRule>> GetRulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationRule>> GetRulesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task SaveProductAsync(Product product, CancellationToken cancellationToken = default);
    Task SaveDeviceAsync(Device device, CancellationToken cancellationToken = default);
    Task SaveRuleAsync(EvaluationRule rule, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(SmartShelf.Domain.Enums.ShelfResourceKind kind, Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(SmartShelf.Domain.Enums.ShelfResourceKind kind, Guid id, CancellationToken cancellationToken = default);
}
