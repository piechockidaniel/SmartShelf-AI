namespace SmartShelf.Api;

public static class PersistenceProvider
{
    public static string Normalize(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "sqlite" => "sqlite",
            "inmemory" or "memory" => "inmemory",
            var provider => throw new InvalidOperationException(
                $"Unsupported Persistence:Provider '{provider}'. Expected 'sqlite' or 'inmemory'.")
        };
}
