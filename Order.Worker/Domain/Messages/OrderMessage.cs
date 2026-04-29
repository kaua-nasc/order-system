namespace Order.Worker.Domain.Messages;

public readonly record struct OrderMessage(Guid OrderId);