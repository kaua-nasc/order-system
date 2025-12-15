namespace Order.Worker.Infra.MessageBroker.Models;

public record RabbitMqOptions(string Hostname, string Username, string Password);