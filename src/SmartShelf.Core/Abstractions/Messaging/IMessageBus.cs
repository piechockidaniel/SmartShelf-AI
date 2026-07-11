namespace SmartShelf.Core.Abstractions.Messaging;

    public interface IMessageBus
{
    Task PublishAsync<T>(
        string topic,
        T message);

    Task SubscribeAsync(
        string topic);
}