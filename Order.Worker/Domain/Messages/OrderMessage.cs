namespace Order.Worker.Domain.Messages;

public readonly record struct OrderItemMessage(Guid ProductId, int Quantity);
    
public readonly record struct OrderMessage(
    Guid OrderId, 
    Guid CustomerId, 
    decimal TotalAmount, 
    OrderItemMessage[] Items);