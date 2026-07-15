namespace SmartShelf.Application.Exceptions;

public sealed class PersistenceConcurrencyException(string message) : Exception(message);

public sealed class ShelfHasOperationalHistoryException(Guid shelfId)
    : Exception($"Shelf '{shelfId:D}' has operational history and must be disabled instead of deleted.");

public sealed class ResourceBindingValidationException(string message) : Exception(message);
