using SmartShelf.Application.Abstractions.Persistence;
using SmartShelf.Application.Abstractions.Telemetry;
using SmartShelf.Application.Contracts;
using SmartShelf.Application.Features.Alerts;
using SmartShelf.Application.Features.ShelfObservations;
using SmartShelf.Infrastructure.Device;
using SmartShelf.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IShelfObservationStore>(_ =>
    new SqliteShelfObservationStore(GetConnectionString(builder.Configuration)));
builder.Services.AddSingleton<IAlertStore>(_ =>
    new SqliteAlertStore(GetConnectionString(builder.Configuration)));
builder.Services.AddSingleton<ILedController, VirtualLedController>();
builder.Services.AddScoped<RecordShelfObservationHandler>();
builder.Services.AddScoped<AcknowledgeAlertHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseHttpsRedirection();

var shelves = app.MapGroup("/api/v1/shelves");

shelves.MapGet("", async (int? limit, IShelfObservationStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.GetSummariesAsync(limit ?? 100, cancellationToken)));

shelves.MapPost("/{shelfId:guid}/observations", async (
    Guid shelfId, ShelfObservationDto observation, RecordShelfObservationHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(
        new RecordShelfObservationCommand(shelfId, observation), cancellationToken);
    return Results.Created($"/api/v1/shelves/{shelfId}/observations/latest", result);
});

shelves.MapGet("/{shelfId:guid}/observations", async (
    Guid shelfId, int? limit, IShelfObservationStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.GetRecentAsync(shelfId, limit ?? 50, cancellationToken)));

shelves.MapGet("/{shelfId:guid}/observations/latest", async (
    Guid shelfId, IShelfObservationStore store, CancellationToken cancellationToken) =>
{
    var result = await store.GetLatestAsync(shelfId, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

var alerts = app.MapGroup("/api/v1/alerts");

alerts.MapGet("", async (
    bool? openOnly, int? limit, IAlertStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.GetAsync(openOnly ?? true, limit ?? 100, cancellationToken)));

alerts.MapPost("/{alertId:guid}/acknowledge", async (
    Guid alertId, AcknowledgeAlertHandler handler, CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(alertId, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/", () => Results.Ok(new { service = "SmartShelf API", status = "Healthy", dashboard = "http://127.0.0.1:5100/", health = "/health" }));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();

static string GetConnectionString(IConfiguration configuration)
    => configuration.GetConnectionString("SmartShelf")
        ?? "Data Source=data/smartshelf.db;Cache=Shared";

