using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Domain.Entities;

namespace SmartShelf.Application.Features.Shelves;

public sealed class ShelfManagementService(IShelfRepository repository)
{
    public async Task<ShelfDto> CreateAsync(
        CreateShelfCommand command, CancellationToken cancellationToken = default)
    {
        var shelf = new Shelf(command.Name, command.Location);
        await repository.AddAsync(shelf, cancellationToken);
        return Map(shelf);
    }

    public async Task<IReadOnlyList<ShelfDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => [.. (await repository.GetAllAsync(cancellationToken)).Select(Map)];

    public async Task<ShelfDto?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var shelf = await repository.GetByIdAsync(id, cancellationToken);
        return shelf is null ? null : Map(shelf);
    }

    public async Task<ShelfDto?> UpdateAsync(
        Guid id, UpdateShelfCommand command, CancellationToken cancellationToken = default)
    {
        var shelf = await repository.GetByIdAsync(id, cancellationToken);
        if (shelf is null)
        {
            return null;
        }

        shelf.UpdateConfiguration(command.Name, command.Location);
        await repository.UpdateAsync(shelf, command.ExpectedVersion ?? shelf.Version - 1, cancellationToken);
        return Map(shelf);
    }

    public async Task<ShelfDto?> SetEnabledAsync(
        Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var shelf = await repository.GetByIdAsync(id, cancellationToken);
        if (shelf is null)
        {
            return null;
        }

        shelf.SetEnabled(enabled);
        await repository.UpdateAsync(shelf, shelf.Version - 1, cancellationToken);
        return Map(shelf);
    }

    public async Task<bool> DeleteAsync(
        Guid id, int? expectedVersion = null, CancellationToken cancellationToken = default)
    {
        var shelf = await repository.GetByIdAsync(id, cancellationToken);
        if (shelf is null)
        {
            return false;
        }
        if (await repository.HasOperationalHistoryAsync(id, cancellationToken))
        {
            throw new SmartShelf.Application.Exceptions.ShelfHasOperationalHistoryException(id);
        }
        await repository.DeleteAsync(id, expectedVersion ?? shelf.Version, cancellationToken);
        return true;
    }

    private static ShelfDto Map(Shelf shelf)
        => new(
            shelf.Id, shelf.Name, shelf.Location.Warehouse, shelf.Location.Aisle,
            shelf.Location.Shelf, shelf.Location.Position, null, null,
            shelf.Enabled, shelf.CreatedAt, shelf.UpdatedAt, shelf.Version);
}
