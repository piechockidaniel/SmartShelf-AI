namespace SmartShelf.Application.Contracts;

public sealed record ShelfBindingDto(Guid Id, string Kind, Guid ResourceId);

public sealed record ResourceNodeDto(
    Guid Id,
    string Label,
    string Kind,
    string Category,
    string? ExternalKey = null);

public sealed record ShelfConfigurationDto(
    ShelfDto Shelf,
    IReadOnlyList<ShelfBindingDto> Bindings,
    IReadOnlyList<ResourceNodeDto> Resources);

public sealed record UpdateShelfConfigurationRequest(
    int ExpectedVersion,
    IReadOnlyList<UpdateShelfBindingRequest> Bindings);

public sealed record UpdateShelfBindingRequest(string Kind, Guid ResourceId);
