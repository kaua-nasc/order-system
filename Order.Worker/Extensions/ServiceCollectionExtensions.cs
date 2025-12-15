using Order.Worker.Infra.MessageBroker.Models;

namespace Order.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRabbitMqConnection(string host, string user, string password)
        {
            var rabbitConnection = new RabbitMqOptions(host, user, password);
            return services.AddSingleton(rabbitConnection);
        }
    }
}