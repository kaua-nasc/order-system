namespace Order.Input.Domain.Specification.Models;

public class SpecificationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationError> Errors { get; } = [];

    public static SpecificationResult Success() => new();

    public static SpecificationResult Failure(params ValidationError[] errors)
    {
        var result = new SpecificationResult();
        result.Errors.AddRange(errors);
        return result;
    }

    public void AddError(string code, string message, string propertyName)
    {
        Errors.Add(new ValidationError(code ,message, propertyName));
    }
}