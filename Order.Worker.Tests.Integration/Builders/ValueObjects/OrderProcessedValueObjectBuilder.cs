using Order.Worker.Domain.Messages;
using Order.Worker.Domain.ValueObjects;

namespace Order.Worker.Tests.Integration.Builders.ValueObjects;

public class OrderProcessedValueObjectBuilder
{
    private Guid _orderId = Guid.NewGuid();
    private Guid _customerId = Guid.NewGuid();
    private decimal _totalAmount = 10;

    public OrderProcessedValueObjectBuilder WithOrderId(Guid orderId)
    {
        _orderId = orderId;
        return this;
    }
    
    public OrderProcessedValueObjectBuilder WithCustomerId(Guid customerId)
    {
        _customerId = customerId;
        return this;
    }
    
    public OrderProcessedValueObjectBuilder WithTotalAmount(decimal totalAmount)
    {
        _totalAmount = totalAmount;
        return this;
    }

    public OrderProcessedValueObjectBuilder FromMessage(OrderMessage message)
    {
        _orderId = message.OrderId;
        _customerId = message.CustomerId;
        _totalAmount = message.TotalAmount;
        return this;
    }
    
    public OrderProcessedValueObject Build()
    {
        return new OrderProcessedValueObject(
            _orderId,
            _customerId,
            _totalAmount
        );
    }
}