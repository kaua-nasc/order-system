using Order.Input.Domain.Specification.Models;
using Order.Input.Domain.ValueObjects;

namespace Order.Input.Domain.Specification.Validations;

public class OrderSpecification : BaseSpecification<OrderValueObject>
{
    protected override void AddErrors(OrderValueObject entity, SpecificationResult result)
    {
        if (entity.TotalAmount < 0)
        {
            result.AddError("UNDERAGE", "Order total amount cannot be negative", nameof(entity.TotalAmount));
        }

        if (entity.CreatedAt > DateTime.UtcNow)
        {
            result.AddError("", "", nameof(entity.CreatedAt));
        }

        if (entity.Items.Length == 0)
        {
            result.AddError("", "", nameof(entity.Items));
        }
    }
}