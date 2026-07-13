namespace SmartShelf.Application.Abstractions.Messaging;

public interface IMessageBus
{
    Task PublishAsync<T>(
        string topic,
        T message,
        CancellationToken cancellationToken = default);
}
