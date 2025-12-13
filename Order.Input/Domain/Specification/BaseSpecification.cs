using Order.Input.Domain.Specification.Interfaces;
using Order.Input.Domain.Specification.Models;

namespace Order.Input.Domain.Specification;

public abstract class BaseSpecification<T> : IAsyncSpecification<T>
{
    protected virtual string ErrorCode => GetType().Name;
    protected virtual string ErrorMessage => "Validation failed";

    public bool IsSatisfiedBy(T entity)
    {
        return Validate(entity).IsValid;
    }

    public SpecificationResult Validate(T entity)
    {
        var result = new SpecificationResult();

        try
        {
            AddErrors(entity, result);
        }
        catch (Exception e)
        {
            result.AddError("VALIDATION_EXCEPTION", $"Validation error: {e.Message}", nameof(entity));
        }
        
        return result;
    }

    public virtual async Task<bool> IsSatisfiedByAsync(T entity, 
        CancellationToken cancellationToken = default)
    {
        var result = await ValidateAsync(entity, cancellationToken);
        return result.IsValid;
    }
    
    public virtual async Task<SpecificationResult> ValidateAsync(T entity,
        CancellationToken cancellationToken = default)
    {
        var result = new SpecificationResult();
        
        try
        {
            await AddErrorsAsync(entity, result, cancellationToken);
        }
        catch (Exception ex)
        {
            result.AddError("VALIDATION_EXCEPTION", $"Validation error: {ex.Message}", nameof(entity));
        }
        
        return result;
    }
    
    protected abstract void AddErrors(T entity, SpecificationResult result);
    
    protected virtual Task AddErrorsAsync(T entity, SpecificationResult result,
        CancellationToken cancellationToken)
    {
        AddErrors(entity, result);
        return Task.CompletedTask;
    }
    
    public BaseSpecification<T> And(BaseSpecification<T> other)
        => new AndSpecification<T>(this, other);
    
    public BaseSpecification<T> Or(BaseSpecification<T> other)
        => new OrSpecification<T>(this, other);
    
    public BaseSpecification<T> Not()
        => new NotSpecification<T>(this);
}