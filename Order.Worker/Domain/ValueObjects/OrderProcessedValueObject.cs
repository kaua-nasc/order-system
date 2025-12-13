using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Order.Worker.Domain.Messages;

namespace Order.Worker.Domain.ValueObjects;

[Table("orders_processed")]
public record OrderProcessedValueObject(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount)
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
    
    public OrderProcessedValueObject(OrderMessage order) 
        : this(order.OrderId, order.CustomerId, order.TotalAmount)
    {
    }
}