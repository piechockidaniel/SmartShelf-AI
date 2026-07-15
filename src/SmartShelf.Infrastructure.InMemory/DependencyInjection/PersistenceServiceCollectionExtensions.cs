using Microsoft.Extensions.DependencyInjection;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Infrastructure.InMemory.Persistence;

namespace SmartShelf.Infrastructure.InMemory.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryPersistence(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryPersistenceStore>();
        services.AddSingleton<IShelfRepository>(provider => provider.GetRequiredService<InMemoryPersistenceStore>());
        services.AddSingleton<IShelfCommandStore>(provider => provider.GetRequiredService<InMemoryPersistenceStore>());
        services.AddSingleton<IShelfQueries>(provider => provider.GetRequiredService<InMemoryPersistenceStore>());
        services.AddSingleton<IResourceCatalogStore>(provider => provider.GetRequiredService<InMemoryPersistenceStore>());
        services.AddSingleton<IShelfObservationStore>(provider => provider.GetRequiredService<InMemoryPersistenceStore>());
        services.AddSingleton<IObservationCommandStore>(provider => provider.GetRequiredService<InMemoryPersistenceStore>());
        services.AddSingleton<IAlertStore>(provider => provider.GetRequiredService<InMemoryPersistenceStore>());
        return services;
    }
}
