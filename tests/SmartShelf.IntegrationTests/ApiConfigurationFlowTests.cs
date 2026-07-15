using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SmartShelf.Application.Contracts;
using SmartShelf.Api;
using Xunit;

namespace SmartShelf.IntegrationTests;

public sealed class ApiConfigurationFlowTests
{
    [Theory]
    [InlineData("inmemory")]
    [InlineData("sqlite")]
    public async Task Configured_provider_supports_create_connect_reload_and_overview(string provider)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"smartshelf-api-{Guid.NewGuid():N}.db");
        await using var factory = new ProviderApiFactory(provider, databasePath);
        using var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var deviceResponse = await client.PostAsJsonAsync("/api/v1/devices", new
        {
            name = "Test controller",
            serialNumber = $"controller-{Guid.NewGuid():N}",
            kind = "Controller"
        }, ct);
        deviceResponse.EnsureSuccessStatusCode();

        var createResponse = await client.PostAsJsonAsync("/api/v1/shelves", new
        {
            name = "API shelf",
            location = new { warehouse = "WH", aisle = "A", shelf = "S", position = "P" }
        }, ct);
        createResponse.EnsureSuccessStatusCode();
        var shelf = (await createResponse.Content.ReadFromJsonAsync<ShelfDto>(ct))!;

        var schema = (await client.GetFromJsonAsync<ResourceNodeDto[]>("/api/v1/shelf-resource-schema", ct))!;
        var controller = schema.First(resource => resource.Kind == "Controller");
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/shelves/{shelf.Id:D}/configuration", new
        {
            expectedVersion = shelf.Version,
            bindings = new[] { new { kind = controller.Kind, resourceId = controller.Id } }
        }, ct);
        updateResponse.EnsureSuccessStatusCode();

        var configuration = await client.GetFromJsonAsync<ShelfConfigurationDto>(
            $"/api/v1/shelves/{shelf.Id:D}/configuration", ct);
        var overview = await client.GetFromJsonAsync<ShelfOverviewDto>(
            $"/api/v1/shelf-overviews/{shelf.Id:D}", ct);
        Assert.Single(configuration!.Bindings);
        Assert.Equal(shelf.Version + 1, configuration.Shelf.Version);
        Assert.Single(overview!.Resources);
    }

    [Fact]
    public void Unknown_provider_fails_during_startup()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"smartshelf-api-{Guid.NewGuid():N}.db");
        using var factory = new ProviderApiFactory("invalid", databasePath);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Persistence:Provider", exception.Message, StringComparison.Ordinal);
    }

    private sealed class ProviderApiFactory(string provider, string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Persistence:Provider", provider);
            builder.UseSetting("ConnectionStrings:SmartShelf", $"Data Source={databasePath};Pooling=False");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = provider,
                    ["ConnectionStrings:SmartShelf"] = $"Data Source={databasePath};Pooling=False"
                }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            File.Delete(databasePath);
            File.Delete(databasePath + "-wal");
            File.Delete(databasePath + "-shm");
        }
    }
}
