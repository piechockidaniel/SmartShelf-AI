namespace SmartShelf.Core.Abstractions.Telemetry;

public interface ILedController
{
    Task SetGreenAsync();

    Task SetYellowAsync();

    Task SetRedAsync();

    Task TurnOffAsync();
}
