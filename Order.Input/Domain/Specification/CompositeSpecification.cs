using Order.Input.Domain.Specification.Models;

namespace Order.Input.Domain.Specification;

public class AndSpecification<T>(BaseSpecification<T> left, BaseSpecification<T> right) : BaseSpecification<T>
{
    protected override void AddErrors(T entity, SpecificationResult result)
    {
        var leftResult = left.Validate(entity);
        var rightResult = right.Validate(entity);
        
        result.Errors.AddRange(leftResult.Errors);
        result.Errors.AddRange(rightResult.Errors);
    }
    
    protected override async Task AddErrorsAsync(T entity, SpecificationResult result,
        CancellationToken cancellationToken)
    {
        var leftResult = await left.ValidateAsync(entity, cancellationToken);
        var rightResult = await right.ValidateAsync(entity, cancellationToken);
        
        result.Errors.AddRange(leftResult.Errors);
        result.Errors.AddRange(rightResult.Errors);
    }
}

public class OrSpecification<T>(BaseSpecification<T> left, BaseSpecification<T> right) : BaseSpecification<T>
{
    protected override void AddErrors(T entity, SpecificationResult result)
    {
        var leftResult = left.Validate(entity);
        var rightResult = right.Validate(entity);
        
        if (leftResult.IsValid || rightResult.IsValid) return;
        
        result.Errors.AddRange(leftResult.Errors);
        result.Errors.AddRange(rightResult.Errors);
    }
}

public class NotSpecification<T>(BaseSpecification<T> specification) : BaseSpecification<T>
{
    protected override void AddErrors(T entity, SpecificationResult result)
    {
        var specResult = specification.Validate(entity);
        
        if (specResult.IsValid)
        {
            result.AddError("NOT_VIOLATION", "Condition should not be satisfied", nameof(entity));
        }
    }
}