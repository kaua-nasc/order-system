using Order.Input.Domain.Entities;

namespace Order.Input.Domain.Messages;

public readonly record struct OrderMessage(Guid OrderId)
{
    public OrderMessage(OrderEntity order) 
        : this(order.OrderId) { }
}