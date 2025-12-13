namespace Order.Input.Domain.ValueObjects;

public readonly record struct OrderValueObject(Guid OrderId, Guid CustomerId, decimal TotalAmount, OrderItemValueObject[] Items, DateTime CreatedAt) : IValueObject;