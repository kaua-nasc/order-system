using Order.Input.Domain.Specification.Models;

namespace Order.Input.Domain.Specification.Interfaces;

public interface ISpecification<T>
{
    bool IsSatisfiedBy(T entity);
    SpecificationResult Validate(T entity);
}

public interface IAsyncSpecification<T> : ISpecification<T>
{
    Task<bool> IsSatisfiedByAsync(T entity,  CancellationToken cancellationToken = default);
    Task<SpecificationResult> ValidateAsync(T entity, CancellationToken cancellationToken = default);
}