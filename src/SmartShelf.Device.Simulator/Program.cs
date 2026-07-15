using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

var options = new EmulatorOptions(args);
var emulator = new ShelfEmulator(options);
await emulator.RunAsync(CancellationToken.None);

internal sealed class EmulatorOptions(string[] args)
{
    public string Scenario { get; } = Read(args, "--scenario") ?? "demo";
    public int Cycles { get; } = int.TryParse(Read(args, "--cycles"), out var cycles) ? cycles : 12;
    public int DelayMs { get; } = int.TryParse(Read(args, "--delay-ms"), out var delayMs) ? delayMs : 750;
    public Guid ShelfId { get; } = Guid.TryParse(Read(args, "--shelf-id"), out var id)
        ? id
        : Guid.Parse("11111111-1111-1111-1111-111111111111");
    public bool Offline { get; } = args.Contains("--offline", StringComparer.OrdinalIgnoreCase);
    public string ApiUrl { get; } = Read(args, "--api-url")
        ?? Environment.GetEnvironmentVariable("SMARTSHELF_API_URL")
        ?? "http://127.0.0.1:5247";

    private static string? Read(string[] args, string name)
    {
        var index = Array.FindIndex(args, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

internal sealed class ShelfEmulator(EmulatorOptions options)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var httpClient = options.Offline
            ? null
            : new HttpClient { BaseAddress = new Uri(options.ApiUrl), Timeout = TimeSpan.FromSeconds(5) };

        Console.WriteLine("SmartShelf ARM64 edge emulator");
        Console.WriteLine($"shelf={options.ShelfId:D} scenario={options.Scenario} cycles={options.Cycles} delayMs={options.DelayMs}");
        Console.WriteLine(options.Offline ? "transport=console" : $"transport=http api={options.ApiUrl}");

        for (var index = 0; index < options.Cycles && !cancellationToken.IsCancellationRequested; index++)
        {
            var timestamp = DateTimeOffset.UtcNow;
            var reading = NextReading(index);
            var decision = EdgeDecisionEngine.Evaluate(reading);
            var envelope = new EmulatedShelfEvent(
                timestamp, options.ShelfId, RuntimeInformation.ProcessArchitecture.ToString(), reading, decision);

            Console.WriteLine(JsonSerializer.Serialize(envelope, _jsonOptions));

            if (httpClient is not null)
            {
                await PostObservationAsync(httpClient, timestamp, reading, cancellationToken);
            }

            await Task.Delay(options.DelayMs, cancellationToken);
        }
    }

    private async Task PostObservationAsync(
        HttpClient httpClient,
        DateTimeOffset timestamp,
        ShelfReading reading,
        CancellationToken cancellationToken)
    {
        var request = new ApiShelfObservation(
            reading.InventoryPercent, reading.DaysUntilExpiration,
            reading.ExpiredProductDetected, reading.SensorOnline, timestamp);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"/api/v1/shelves/{options.ShelfId:D}/observations", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            Console.WriteLine($"posted={response.StatusCode} shelf={options.ShelfId:D}");
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"api_error={exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("api_error=request timed out");
        }
    }

    private ShelfReading NextReading(int index)
    {
        return options.Scenario.ToLowerInvariant() switch
        {
            "normal" => new ShelfReading(82, 32, false, true),
            "warning" => new ShelfReading(24, 5, false, true),
            "critical" => new ShelfReading(7, -1, true, true),
            "offline" => new ShelfReading(0, 0, false, false),
            _ => DemoReading(index)
        };
    }

    private static ShelfReading DemoReading(int index)
    {
        return (index % 4) switch
        {
            0 => new ShelfReading(86, 30, false, true),
            1 => new ShelfReading(28, 5, false, true),
            2 => new ShelfReading(9, -1, true, true),
            _ => new ShelfReading(0, 0, false, false)
        };
    }
}

internal static class EdgeDecisionEngine
{
    public static ShelfDecision Evaluate(ShelfReading reading)
    {
        if (!reading.SensorOnline)
        {
            return new("Offline", "Blue", "Sensor unavailable; keep last known state and request inspection.", 0.95);
        }

        if (reading.InventoryPercent <= 10 || reading.ExpiredProductDetected)
        {
            return new("Critical", "Red", "Immediate action required at the shelf edge.", 0.98);
        }

        if (reading.InventoryPercent < 30 || reading.DaysUntilExpiration < 7)
        {
            return new("Warning", "Yellow", "Local ARM64 controller predicts replenishment or expiry risk.", 0.86);
        }

        return new("Healthy", "Green", "Shelf state is normal; no cloud round trip required.", 0.99);
    }
}

internal sealed record ShelfReading(
    int InventoryPercent, int DaysUntilExpiration, bool ExpiredProductDetected, bool SensorOnline);
internal sealed record ShelfDecision(string Status, string LedColor, string Reason, double Confidence);
internal sealed record EmulatedShelfEvent(
    DateTimeOffset Timestamp, Guid ShelfId, string ProcessArchitecture, ShelfReading Reading, ShelfDecision Decision);
internal sealed record ApiShelfObservation(
    int InventoryPercent, int DaysUntilExpiration, bool ExpiredProductDetected,
    bool SensorOnline, DateTimeOffset CapturedAt);

