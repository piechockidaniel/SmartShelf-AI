using SmartShelf.Application.Abstractions.Messaging;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Contracts;
using SmartShelf.Application.Exceptions;
using SmartShelf.Domain.Entities;
using SmartShelf.Domain.Enums;

namespace SmartShelf.Application.Features.Shelves;

public sealed record GetShelfConfigurationQuery(Guid ShelfId)
    : IQuery<ShelfConfigurationDto?>;

public sealed class GetShelfConfigurationHandler(IShelfQueries queries)
    : IQueryHandler<GetShelfConfigurationQuery, ShelfConfigurationDto?>
{
    public Task<ShelfConfigurationDto?> HandleAsync(
        GetShelfConfigurationQuery query,
        CancellationToken cancellationToken = default)
        => queries.GetConfigurationAsync(query.ShelfId, cancellationToken);
}

public sealed record UpdateShelfConfigurationCommand(
    Guid ShelfId,
    int ExpectedVersion,
    IReadOnlyList<UpdateShelfBindingRequest> Bindings)
    : ICommand<ShelfConfigurationDto?>;

public sealed class UpdateShelfConfigurationHandler(
    IShelfRepository shelves,
    IShelfQueries queries,
    IResourceCatalogStore resources)
    : ICommandHandler<UpdateShelfConfigurationCommand, ShelfConfigurationDto?>
{
    public async Task<ShelfConfigurationDto?> HandleAsync(
        UpdateShelfConfigurationCommand command,
        CancellationToken cancellationToken = default)
    {
        var shelf = await shelves.GetByIdAsync(command.ShelfId, cancellationToken);
        if (shelf is null)
        {
            return null;
        }

        if (shelf.Version != command.ExpectedVersion)
        {
            throw new PersistenceConcurrencyException(
                $"Shelf '{command.ShelfId:D}' is version {shelf.Version}, not {command.ExpectedVersion}.");
        }

        var bindings = new List<ShelfResourceBinding>(command.Bindings.Count);
        foreach (var requested in command.Bindings)
        {
            if (!Enum.TryParse<ShelfResourceKind>(requested.Kind, true, out var kind))
            {
                throw new ResourceBindingValidationException($"Unknown resource kind '{requested.Kind}'.");
            }

            if (!await resources.ExistsAsync(kind, requested.ResourceId, cancellationToken))
            {
                throw new ResourceBindingValidationException(
                    $"{kind} resource '{requested.ResourceId:D}' does not exist.");
            }

            bindings.Add(new ShelfResourceBinding(kind, requested.ResourceId));
        }

        try
        {
            shelf.ReplaceBindings(bindings);
        }
        catch (InvalidOperationException exception)
        {
            throw new ResourceBindingValidationException(exception.Message);
        }

        if (shelf.Version != command.ExpectedVersion)
        {
            await shelves.UpdateAsync(shelf, command.ExpectedVersion, cancellationToken);
        }

        return await queries.GetConfigurationAsync(command.ShelfId, cancellationToken);
    }
}

public sealed record GetShelfResourceSchemaQuery : IQuery<IReadOnlyList<ResourceNodeDto>>;

public sealed class GetShelfResourceSchemaHandler(IShelfQueries queries)
    : IQueryHandler<GetShelfResourceSchemaQuery, IReadOnlyList<ResourceNodeDto>>
{
    public Task<IReadOnlyList<ResourceNodeDto>> HandleAsync(
        GetShelfResourceSchemaQuery query,
        CancellationToken cancellationToken = default)
        => queries.GetResourceSchemaAsync(cancellationToken);
}

public sealed record GetShelfOverviewsQuery : IQuery<IReadOnlyList<ShelfOverviewDto>>;

public sealed class GetShelfOverviewsHandler(IShelfQueries queries)
    : IQueryHandler<GetShelfOverviewsQuery, IReadOnlyList<ShelfOverviewDto>>
{
    public Task<IReadOnlyList<ShelfOverviewDto>> HandleAsync(
        GetShelfOverviewsQuery query,
        CancellationToken cancellationToken = default)
        => queries.GetOverviewsAsync(cancellationToken);
}
