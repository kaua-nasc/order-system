namespace Order.Input.Domain.ValueObjects;

public readonly record struct OrderItemValueObject(Guid ProductId, int Quantity);