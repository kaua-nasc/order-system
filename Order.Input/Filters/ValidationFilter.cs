using Order.Input.Domain.Specification;
using Order.Input.Domain.Specification.Services;

namespace Order.Input.Filters;

public class ValidationFilter<T>(Validator<T> validator, IServiceProvider serviceProvider) : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var argument = context.Arguments
            .OfType<T>()
            .FirstOrDefault();
        
        if (EqualityComparer<T>.Default.Equals(argument, default))
        {
            return Results.BadRequest("Invalid request body or missing required data");
        }
        
        var specificationType = typeof(BaseSpecification<>).MakeGenericType(typeof(T));
        var specifications = serviceProvider.GetServices(specificationType);
        
        foreach (var spec in specifications)
        {
            if (spec is BaseSpecification<T> typedSpec)
            {
                validator.AddSpecification(typedSpec);
            }
        }
        
        var result = await validator.ValidateAsync(argument);
        if (!result.IsValid)
        {
            return Results.BadRequest(new
            {
                errors = result.Errors.Select(e => new
                {
                    e.Code,
                    e.Message,
                    e.PropertyName
                })
            });
        }
        
        return await next(context);
    }
}