using Order.Input.Domain.Specification.Models;

namespace Order.Input.Domain.Specification.Services;

public class Validator<T>
{
    private readonly List<BaseSpecification<T>> _specifications = [];
    
    public Validator<T> AddSpecification(BaseSpecification<T> specification)
    {
        _specifications.Add(specification);
        return this;
    }
    
    public SpecificationResult Validate(T entity)
    {
        var result = new SpecificationResult();
        
        foreach (var specResult in _specifications.Select(spec => spec.Validate(entity)))
        {
            result.Errors.AddRange(specResult.Errors);
        }
        
        return result;
    }
    
    public async Task<SpecificationResult> ValidateAsync(T entity, 
        CancellationToken cancellationToken = default)
    {
        var result = new SpecificationResult();
        
        foreach (var spec in _specifications)
        {
            var specResult = await spec.ValidateAsync(entity, cancellationToken);
            result.Errors.AddRange(specResult.Errors);
        }
        
        return result;
    }
}