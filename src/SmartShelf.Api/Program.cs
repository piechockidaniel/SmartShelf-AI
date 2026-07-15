using SmartShelf.Api;
using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Abstractions.Telemetry;
using SmartShelf.Application.Contracts;
using SmartShelf.Application.Exceptions;
using SmartShelf.Application.Features.Alerts;
using SmartShelf.Application.Features.Catalog;
using SmartShelf.Application.Features.ShelfObservations;
using SmartShelf.Application.Features.Shelves;
using SmartShelf.Domain.Enums;
using SmartShelf.Infrastructure.Device;
using SmartShelf.Infrastructure.InMemory.DependencyInjection;
using SmartShelf.Infrastructure.Sqlite.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("Configurator", policy => policy
    .SetIsOriginAllowed(origin =>
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        return uri.Host is "localhost" or "127.0.0.1";
    })
    .AllowAnyHeader()
    .AllowAnyMethod()));

var provider = PersistenceProvider.Normalize(builder.Configuration.GetValue<string>("Persistence:Provider"));
var connectionString = builder.Configuration.GetConnectionString("SmartShelf")
    ?? "Data Source=data/smartshelf.db;Cache=Shared";
switch (provider)
{
    case "sqlite":
        builder.Services.AddSqlitePersistence(connectionString);
        break;
    case "inmemory":
    case "memory":
        builder.Services.AddInMemoryPersistence();
        break;
}

builder.Services.AddSingleton<ILedController, VirtualLedController>();
builder.Services.AddScoped<RecordShelfObservationHandler>();
builder.Services.AddScoped<AcknowledgeAlertHandler>();
builder.Services.AddScoped<ShelfManagementService>();
builder.Services.AddScoped<ResourceCatalogService>();
builder.Services.AddScoped<GetShelfConfigurationHandler>();
builder.Services.AddScoped<UpdateShelfConfigurationHandler>();
builder.Services.AddScoped<GetShelfResourceSchemaHandler>();
builder.Services.AddScoped<GetShelfOverviewsHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    if (provider == "sqlite")
    {
        await app.Services.SeedSqliteDevelopmentDataAsync();
    }
}

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception exception)
    {
        var (status, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            PersistenceConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
            ShelfHasOperationalHistoryException => (StatusCodes.Status409Conflict, "Shelf has operational history"),
            ResourceBindingValidationException => (StatusCodes.Status400BadRequest, "Invalid shelf configuration"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Validation failed"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Operation rejected"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: title, detail: exception.Message).ExecuteAsync(context);
    }
});

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartShelf API v1");
    options.DocumentTitle = "SmartShelf API";
});
app.UseCors("Configurator");
app.UseHttpsRedirection();

var shelfStatus = app.MapGroup("/api/v1/shelf-status").WithTags("Shelf Status");
shelfStatus.MapGet("", async (int? limit, IShelfObservationStore store, CancellationToken ct) =>
    Results.Ok(await store.GetSummariesAsync(limit ?? 100, ct)));

var shelves = app.MapGroup("/api/v1/shelves").WithTags("Shelves");
shelves.MapGet("", async (IShelfQueries queries, CancellationToken ct) =>
    Results.Ok(await queries.GetShelvesAsync(ct)));
shelves.MapPost("", async (CreateShelfCommand command, ShelfManagementService service, CancellationToken ct) =>
{
    var result = await service.CreateAsync(command, ct);
    return Results.Created($"/api/v1/shelves/{result.Id:D}", result);
});
shelves.MapGet("/{shelfId:guid}", async (Guid shelfId, ShelfManagementService service, CancellationToken ct) =>
{
    var result = await service.GetAsync(shelfId, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});
shelves.MapPut("/{shelfId:guid}", async (
    Guid shelfId, UpdateShelfCommand command, ShelfManagementService service, CancellationToken ct) =>
{
    if (command.ExpectedVersion is null) return Results.BadRequest(new { error = "expectedVersion is required." });
    var result = await service.UpdateAsync(shelfId, command, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});
shelves.MapPatch("/{shelfId:guid}/enabled", async (
    Guid shelfId, ShelfEnabledRequest request, IShelfRepository repository, CancellationToken ct) =>
{
    var shelf = await repository.GetByIdAsync(shelfId, ct);
    if (shelf is null) return Results.NotFound();
    if (shelf.Version != request.ExpectedVersion)
        throw new PersistenceConcurrencyException($"Shelf '{shelfId:D}' is version {shelf.Version}, not {request.ExpectedVersion}.");
    shelf.SetEnabled(request.Enabled);
    if (shelf.Version != request.ExpectedVersion)
        await repository.UpdateAsync(shelf, request.ExpectedVersion, ct);
    return Results.Ok((await app.Services.GetRequiredService<IShelfQueries>().GetShelvesAsync(ct)).Single(item => item.Id == shelfId));
});
shelves.MapDelete("/{shelfId:guid}", async (
    Guid shelfId, int? expectedVersion, ShelfManagementService service, CancellationToken ct) =>
{
    if (expectedVersion is null) return Results.BadRequest(new { error = "expectedVersion is required." });
    return await service.DeleteAsync(shelfId, expectedVersion, ct) ? Results.NoContent() : Results.NotFound();
});
shelves.MapGet("/{shelfId:guid}/configuration", async (
    Guid shelfId, GetShelfConfigurationHandler handler, CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new GetShelfConfigurationQuery(shelfId), ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});
shelves.MapPut("/{shelfId:guid}/configuration", async (
    Guid shelfId, UpdateShelfConfigurationRequest request,
    UpdateShelfConfigurationHandler handler, CancellationToken ct) =>
{
    var result = await handler.HandleAsync(
        new UpdateShelfConfigurationCommand(shelfId, request.ExpectedVersion, request.Bindings), ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});
shelves.MapPost("/{shelfId:guid}/observations", async (
    Guid shelfId, ShelfObservationDto observation, RecordShelfObservationHandler handler, CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new RecordShelfObservationCommand(shelfId, observation), ct);
    return Results.Created($"/api/v1/shelves/{shelfId}/observations/latest", result);
});
shelves.MapGet("/{shelfId:guid}/observations", async (
    Guid shelfId, int? limit, IShelfObservationStore store, CancellationToken ct) =>
    Results.Ok(await store.GetRecentAsync(shelfId, limit ?? 50, ct)));
shelves.MapGet("/{shelfId:guid}/observations/latest", async (
    Guid shelfId, IShelfObservationStore store, CancellationToken ct) =>
{
    var result = await store.GetLatestAsync(shelfId, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/api/v1/shelf-resource-schema", async (GetShelfResourceSchemaHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.HandleAsync(new GetShelfResourceSchemaQuery(), ct))).WithTags("Shelf Configuration");

var overviews = app.MapGroup("/api/v1/shelf-overviews").WithTags("Shelf Overviews");
overviews.MapGet("", async (GetShelfOverviewsHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.HandleAsync(new GetShelfOverviewsQuery(), ct)));
overviews.MapGet("/{shelfId:guid}", async (Guid shelfId, IShelfQueries queries, CancellationToken ct) =>
{
    var result = await queries.GetOverviewAsync(shelfId, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

var products = app.MapGroup("/api/v1/products").WithTags("Products");
products.MapGet("", async (ResourceCatalogService service, CancellationToken ct) => Results.Ok(await service.GetProductsAsync(ct)));
products.MapPost("", async (SaveProductRequest request, ResourceCatalogService service, CancellationToken ct) =>
{
    var result = await service.SaveProductAsync(request, ct);
    return Results.Created($"/api/v1/products/{result.Id:D}", result);
});
products.MapPut("/{id:guid}", async (Guid id, SaveProductRequest request, ResourceCatalogService service, CancellationToken ct) =>
    Results.Ok(await service.SaveProductAsync(id, request, ct)));
products.MapDelete("/{id:guid}", async (Guid id, ResourceCatalogService service, CancellationToken ct) =>
    await service.DeleteAsync(ShelfResourceKind.Product, id, ct) ? Results.NoContent() : Results.Conflict());

var devices = app.MapGroup("/api/v1/devices").WithTags("Devices");
devices.MapGet("", async (ResourceCatalogService service, CancellationToken ct) => Results.Ok(await service.GetDevicesAsync(ct)));
devices.MapPost("", async (SaveDeviceRequest request, ResourceCatalogService service, CancellationToken ct) =>
{
    var result = await service.SaveDeviceAsync(request, ct);
    return Results.Created($"/api/v1/devices/{result.Id:D}", result);
});
devices.MapPut("/{id:guid}", async (Guid id, SaveDeviceRequest request, ResourceCatalogService service, CancellationToken ct) =>
    Results.Ok(await service.SaveDeviceAsync(id, request, ct)));
devices.MapDelete("/{id:guid}", async (Guid id, string kind, ResourceCatalogService service, CancellationToken ct) =>
    await service.DeleteAsync(Enum.Parse<ShelfResourceKind>(kind, true), id, ct) ? Results.NoContent() : Results.Conflict());

var rules = app.MapGroup("/api/v1/evaluation-rules").WithTags("Evaluation Rules");
rules.MapGet("", async (ResourceCatalogService service, CancellationToken ct) => Results.Ok(await service.GetRulesAsync(ct)));
rules.MapPost("", async (SaveEvaluationRuleRequest request, ResourceCatalogService service, CancellationToken ct) =>
{
    var result = await service.SaveRuleAsync(request, ct);
    return Results.Created($"/api/v1/evaluation-rules/{result.Id:D}", result);
});
rules.MapPut("/{id:guid}", async (Guid id, SaveEvaluationRuleRequest request, ResourceCatalogService service, CancellationToken ct) =>
    Results.Ok(await service.SaveRuleAsync(id, request, ct)));
rules.MapDelete("/{id:guid}", async (Guid id, ResourceCatalogService service, CancellationToken ct) =>
    await service.DeleteAsync(ShelfResourceKind.EvaluationRule, id, ct) ? Results.NoContent() : Results.Conflict());

var alerts = app.MapGroup("/api/v1/alerts").WithTags("Alerts");
alerts.MapGet("", async (bool? openOnly, int? limit, IAlertStore store, CancellationToken ct) =>
    Results.Ok(await store.GetAsync(openOnly ?? true, limit ?? 100, ct)));
alerts.MapPost("/{alertId:guid}/acknowledge", async (Guid alertId, AcknowledgeAlertHandler handler, CancellationToken ct) =>
{
    var result = await handler.HandleAsync(alertId, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/", () => Results.Ok(new
{
    service = "SmartShelf API", status = "Healthy", dashboard = "http://127.0.0.1:5100/",
    configurator = "http://127.0.0.1:5173/", swagger = "/swagger/index.html", health = "/health"
}));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", persistence = provider }));
app.Run();

public partial class Program;
