using SmartShelf.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartShelf.Core.Commands;
public sealed class CreateShelfHandler
    : IRequestHandler<CreateShelfCommand, Guid>
{
    private readonly IShelfRepository repository;

    private readonly IUnitOfWork unitOfWork;

    public CreateShelfHandler(
        IShelfRepository repository,
        IUnitOfWork unitOfWork)
    {
        this.repository = repository;
        this.unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(
        CreateShelfCommand request,
        CancellationToken cancellationToken)
    {
        var shelf = new Shelf(
            request.Name,
            request.Location);

        await repository.AddAsync(shelf);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return shelf.Id;
    }
}