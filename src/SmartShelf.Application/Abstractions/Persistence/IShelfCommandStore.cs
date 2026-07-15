using SmartShelf.Domain.Entities;

namespace SmartShelf.Application.Abstractions.Persistence;

public interface IShelfCommandStore
{
    Task<Shelf?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default);
    Task UpdateAsync(Shelf shelf, int expectedVersion, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default);
    Task<bool> HasOperationalHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
