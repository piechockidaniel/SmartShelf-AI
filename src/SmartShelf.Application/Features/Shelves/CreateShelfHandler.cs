using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Domain.Entities;

namespace SmartShelf.Application.Features.Shelves;

public sealed class CreateShelfHandler(
    IShelfRepository repository,
    IUnitOfWork unitOfWork)
{
    public async Task<Guid> HandleAsync(
        CreateShelfCommand request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentNullException.ThrowIfNull(request.Location);

        var shelf = new Shelf(request.Name, request.Location);
        await repository.AddAsync(shelf, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return shelf.Id;
    }
}
