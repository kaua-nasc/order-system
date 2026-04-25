using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Order.Input.Domain.Enums;

namespace Order.Input.Domain.Entities;

[Table("orders")]
public class OrderEntity
{
    [Key]
    public Guid OrderId { get; init; } = Guid.CreateVersion7();
    
    public Guid CustomerId { get; private set; }
    
    public List<OrderItemEntity> Items { get; private set; } = [];
    
    public DateTime CreatedAt { get; private set; }
    
    public decimal TotalAmount { get; private set; }
    
    public DateTime LastUpdatedAt { get; private set; }
    public OrderStatus Status { get ; private set;} = OrderStatus.Waiting;

    // Construtor usado pelo EF Core (Privado para não poluir sua API)
    private OrderEntity() { }

    // Construtor usado por você no código e pelo Serializador JSON
    public OrderEntity(Guid customerId, DateTime createdAt, decimal totalAmount, List<OrderItemEntity>? items = null)
    {
        CustomerId = customerId;
        CreatedAt = createdAt;
        LastUpdatedAt = createdAt;
        TotalAmount = totalAmount;
        Status = OrderStatus.Waiting;
        if (items != null) Items = items;
    }
}
