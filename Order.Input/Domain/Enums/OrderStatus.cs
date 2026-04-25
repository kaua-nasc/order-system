namespace Order.Input.Domain.Enums;

public sealed class OrderStatus : IEquatable<OrderStatus>
{
    public string Value { get; }

    public static readonly OrderStatus Waiting = new OrderStatus("Waiting");
    public static readonly OrderStatus Processing = new OrderStatus("Processing");
    public static readonly OrderStatus Completed = new OrderStatus("Completed");
    public static readonly OrderStatus Error = new OrderStatus("Error");

    private OrderStatus(string value)
    {
        Value = value;
    }

    public static OrderStatus FromString(string value)
    {
        return value switch
        {
            "Waiting" => Waiting,
            "Processing" => Processing,
            "Completed" => Completed,
            "Error" => Error,
            _ => throw new ArgumentException($"'{value}' é um status inválido.")
        };
    }

    public bool Equals(OrderStatus? other) => 
        other is not null && Value == other.Value;

    public override bool Equals(object? obj) => 
        obj is OrderStatus other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(OrderStatus? left, OrderStatus? right) => 
        ReferenceEquals(left, right) || (left?.Equals(right) ?? false);

    public static bool operator !=(OrderStatus? left, OrderStatus? right) => !(left == right);

    public override string ToString() => Value;
}
