using Order.Worker.Domain.Messages;

namespace Order.Worker.Tests.Integration.Builders.Message;

public class OrderMessageBuilder
{
    private Guid _orderId = Guid.NewGuid();
    private Guid _customerId = Guid.NewGuid();
    private decimal _totalAmount = 10;
    private OrderItemMessage[] _items = [];

    public OrderMessageBuilder WithOrderId(Guid orderId)
    {
        _orderId = orderId;
        return this;
    }
    
    public OrderMessageBuilder WithCustomerId(Guid customerId)
    {
        _customerId = customerId;
        return this;
    }
    
    public OrderMessageBuilder WithTotalAmount(decimal totalAmount)
    {
        _totalAmount = totalAmount;
        return this;
    }

    public OrderMessageBuilder WithItems(OrderItemMessage[] items)
    {
        _items = items;
        return this;
    }
    
    public OrderMessage Build()
    {
        return new OrderMessage(
            _orderId,
            _customerId,
            _totalAmount,
            _items
        );
    }
}