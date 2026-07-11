using SmartShelf.Domain.Entities;

public interface IShelfRepository
{
    Task<Shelf?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<Shelf>> GetAllAsync();

    Task AddAsync(Shelf shelf);

    Task UpdateAsync(Shelf shelf);

    Task DeleteAsync(Guid id);
}