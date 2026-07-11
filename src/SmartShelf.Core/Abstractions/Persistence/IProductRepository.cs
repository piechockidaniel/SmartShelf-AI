using SmartShelf.Domain.Entities;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id);

    Task<Product?> GetBySkuAsync(string sku);

    Task<IReadOnlyList<Product>> GetAllAsync();

    Task AddAsync(Product product);

    Task UpdateAsync(Product product);
}