namespace Order.Input.Infra.MessageBroker;

public class PublisherInitializerHostedService(RabbitMqPublisher publisher) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await publisher.InitializeAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return publisher.DisposeAsync().AsTask();
    }
}