using Order.Input.Domain.Entities;

namespace Order.Input.Domain.Messages;

public readonly record struct OrderItemMessage(Guid ProductId, int Quantity)
{
    public OrderItemMessage(OrderItemEntity orderItem) : this(orderItem.ProductId, orderItem.Quantity) {}
};

public readonly record struct OrderMessage(Guid OrderId, Guid CustomerId, decimal TotalAmount, OrderItemMessage[] Items)
{
    public OrderMessage(OrderEntity order) 
        : this(
            order.OrderId,
            order.CustomerId,
            order.TotalAmount,
            order.Items?
                .Select(x => new OrderItemMessage(x))
                .ToArray() ?? []) { }
}