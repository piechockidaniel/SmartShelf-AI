namespace SmartShelf.Api;

public sealed record ShelfEnabledRequest(bool Enabled, int ExpectedVersion);
