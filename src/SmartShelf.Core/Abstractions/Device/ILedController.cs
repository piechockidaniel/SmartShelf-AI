public interface ILedController
{
    Task SetGreenAsync();

    Task SetYellowAsync();

    Task SetRedAsync();

    Task TurnOffAsync();
}