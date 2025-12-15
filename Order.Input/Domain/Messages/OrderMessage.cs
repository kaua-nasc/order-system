using Order.Input.Domain.ValueObjects;

namespace Order.Input.Domain.Messages;

public readonly record struct OrderItemMessage(Guid ProductId, int Quantity)
{
    public OrderItemMessage(OrderItemValueObject orderItem) : this(orderItem.ProductId, orderItem.Quantity) {}
};

public readonly record struct OrderMessage(Guid OrderId, Guid CustomerId, decimal TotalAmount, OrderItemMessage[] Items)
{
    public OrderMessage(OrderValueObject order) 
        : this(
            order.OrderId,
            order.CustomerId,
            order.TotalAmount,
            order.Items
                .Select(x => new OrderItemMessage(x))
                .ToArray()) { }
}