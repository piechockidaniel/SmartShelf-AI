using SmartShelf.Domain.Entities;

namespace SmartShelf.Application.Abstractions.Persistence;

public interface IShelfRepository : IShelfCommandStore
{
    Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(Shelf shelf, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
