namespace Order.Worker.Domain.Exceptions;

public class NotFoundException(string message) : Exception(message);