using Microsoft.Extensions.DependencyInjection;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Infrastructure.Sqlite.Persistence;

namespace SmartShelf.Infrastructure.Sqlite.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddSqlitePersistence(this IServiceCollection services, string connectionString)
    {
        var database = new SqliteDatabase(connectionString);
        services.AddSingleton(database);
        services.AddSingleton(provider => new SqliteShelfRepository(provider.GetRequiredService<SqliteDatabase>()));
        services.AddSingleton(provider => new SqliteResourceCatalogStore(provider.GetRequiredService<SqliteDatabase>()));
        services.AddSingleton(provider => new SqliteShelfObservationStore(provider.GetRequiredService<SqliteDatabase>()));
        services.AddSingleton(provider => new SqliteAlertStore(provider.GetRequiredService<SqliteDatabase>()));
        services.AddSingleton<SqliteShelfQueries>();

        services.AddSingleton<IShelfRepository>(provider => provider.GetRequiredService<SqliteShelfRepository>());
        services.AddSingleton<IShelfCommandStore>(provider => provider.GetRequiredService<SqliteShelfRepository>());
        services.AddSingleton<IResourceCatalogStore>(provider => provider.GetRequiredService<SqliteResourceCatalogStore>());
        services.AddSingleton<IShelfObservationStore>(provider => provider.GetRequiredService<SqliteShelfObservationStore>());
        services.AddSingleton<IObservationCommandStore>(provider => provider.GetRequiredService<SqliteShelfObservationStore>());
        services.AddSingleton<IAlertStore>(provider => provider.GetRequiredService<SqliteAlertStore>());
        services.AddSingleton<IShelfQueries>(provider => provider.GetRequiredService<SqliteShelfQueries>());
        return services;
    }

    public static Task SeedSqliteDevelopmentDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
        => services.GetRequiredService<SqliteResourceCatalogStore>().SeedDevelopmentAsync(cancellationToken);
}
