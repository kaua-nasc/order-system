namespace Order.Input.Domain.Specification.Models;

public record ValidationError(
    string Code,
    string Message,
    string? PropertyName = null
);