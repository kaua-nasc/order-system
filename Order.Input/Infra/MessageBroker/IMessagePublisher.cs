using Order.Input.Domain.Commons;

namespace Order.Input.Infra.MessageBroker;

public interface IMessagePublisher
{
     Task InitializeAsync();
     Task PublishAsync<T>(T message) where T : IMessage;
     ValueTask DisposeAsync();
}