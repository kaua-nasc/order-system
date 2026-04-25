using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Order.Input.Domain.Entities;

[Table("order_items")]
public class OrderItemEntity
{
    [Key]
    public Guid Id { get; init; } = Guid.CreateVersion7();
    
    public Guid ProductId { get; private set; }
    
    public int Quantity { get; private set; }
    
    [ForeignKey("OrderId")]
    public Guid OrderId { get; private set; }

    private OrderItemEntity() { }

    public OrderItemEntity(Guid productId, int quantity, Guid orderId)
    {
        ProductId = productId;
        Quantity = quantity;
        OrderId = orderId;
    }
}
