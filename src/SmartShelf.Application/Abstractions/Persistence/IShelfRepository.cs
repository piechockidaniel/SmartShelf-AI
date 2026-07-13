using SmartShelf.Domain.Entities;

namespace SmartShelf.Application.Abstractions.Persistence;

public interface IShelfRepository
{
    Task<Shelf?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default);

    Task UpdateAsync(Shelf shelf, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
