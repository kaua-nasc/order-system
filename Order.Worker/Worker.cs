using Order.Worker.Infra;

namespace Order.Worker;

public class Worker(ILogger<Worker> logger, RabbitMqConsumer consumer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker started and waiting for messages");

        try
        {
            await consumer.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Worker crashed");
            throw;
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopping");
        await consumer.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}