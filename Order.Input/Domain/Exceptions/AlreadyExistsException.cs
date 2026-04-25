namespace Order.Input.Domain.Exceptions;

public class AlreadyExistsException(string message) : Exception(message);