using Order.Input.Domain.Entities;
using Order.Input.Domain.Specification.Models;

namespace Order.Input.Domain.Specification.Validations;

public class OrderSpecification : BaseSpecification<OrderEntity>
{
    protected override void AddErrors(OrderEntity entity, SpecificationResult result)
    {
        if (entity.TotalAmount < 0)
        {
            result.AddError("UNDERAGE", "Order total amount cannot be negative", nameof(entity.TotalAmount));
        }

        if (entity.CreatedAt > DateTime.UtcNow)
        {
            result.AddError("", "", nameof(entity.CreatedAt));
        }

        if (entity.Items.Count == 0)
        {
            result.AddError("", "", nameof(entity.Items));
        }
    }
}