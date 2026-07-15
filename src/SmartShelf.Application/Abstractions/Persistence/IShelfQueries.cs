using SmartShelf.Application.Contracts;

namespace SmartShelf.Application.Abstractions.Persistence;

public interface IShelfQueries
{
    Task<IReadOnlyList<ShelfDto>> GetShelvesAsync(CancellationToken cancellationToken = default);
    Task<ShelfConfigurationDto?> GetConfigurationAsync(Guid shelfId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceNodeDto>> GetResourceSchemaAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShelfOverviewDto>> GetOverviewsAsync(CancellationToken cancellationToken = default);
    Task<ShelfOverviewDto?> GetOverviewAsync(Guid shelfId, CancellationToken cancellationToken = default);
}
