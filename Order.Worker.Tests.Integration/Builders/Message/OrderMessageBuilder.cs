using Order.Worker.Domain.Messages;

namespace Order.Worker.Tests.Integration.Builders.Message;

public class OrderMessageBuilder
{
    private Guid _orderId = Guid.NewGuid();

    public OrderMessageBuilder WithOrderId(Guid orderId)
    {
        _orderId = orderId;
        return this;
    }
    
    public OrderMessage Build()
    {
        return new OrderMessage(
            _orderId
        );
    }
}
