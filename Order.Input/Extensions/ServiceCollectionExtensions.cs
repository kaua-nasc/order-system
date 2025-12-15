using System.Reflection;
using Order.Input.Domain.Specification;
using RabbitMQ.Client;

namespace Order.Input.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        private IServiceCollection AddSpecificationsFromAssembly(Assembly assembly)
        {
            var specificationTypes = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false, BaseType.IsGenericType: true } &&
                            t.BaseType.GetGenericTypeDefinition() == typeof(BaseSpecification<>));

            foreach (var type in specificationTypes)  services.AddScoped(type);

            return services;
        }

        public IServiceCollection AddSpecificationsFromAssemblyContaining<TMarker>()
        {
            return services.AddSpecificationsFromAssembly(typeof(TMarker).Assembly);
        }

        public IServiceCollection AddRabbitMqConnection(string host, string user, string password)
        {
            var rabbitConnection = new ConnectionFactory()
            {
                HostName = host, 
                UserName = user, 
                Password = password
            };
            return services.AddSingleton(rabbitConnection);
        }
    }
}