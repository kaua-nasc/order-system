using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Order.Worker.Domain;
using Order.Worker.Domain.Messages;
using Order.Worker.Infra.MessageBroker.Models;
using Order.Worker.Infra.MultiTenant;
using Order.Worker.Observability.Tracing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Order.Worker.Infra.MessageBroker.Consumers;

public class RabbitMqConsumer(
    IServiceProvider serviceProvider,
    ILogger<RabbitMqConsumer> logger,
    IAppTracing tracer,
    RabbitMqOptions options) : BackgroundService
{
    private readonly ConnectionFactory _factory = new()
    {
        HostName = options.Hostname,
        UserName = options.Username,
        Password = options.Password,
        AutomaticRecoveryEnabled = true
    };

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await InitializeRabbitMq(stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start RabbitMQ Consumer. Retrying in 5s...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task InitializeRabbitMq(CancellationToken token)
    {
        _connection = await _factory.CreateConnectionAsync(token);
        _channel = await _connection.CreateChannelAsync(cancellationToken: token);

        await _channel.BasicQosAsync(0, 1, false, token);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) => await HandleMessage(ea, token);

        await _channel.BasicConsumeAsync("orders", false, consumer, token);
        logger.LogInformation("Listening for messages on queue 'orders'...");
    }

    private async Task HandleMessage(BasicDeliverEventArgs ea, CancellationToken token)
    {
        var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, ea.BasicProperties.Headers, (headers, key) =>
        {
            if (headers != null && headers.TryGetValue(key, out var value) && value is byte[] bytes)
                return [Encoding.UTF8.GetString(bytes)];
            return [];
        });
        Baggage.Current = parentContext.Baggage;

        using var activity = tracer.Source.StartActivity("Process Order", ActivityKind.Consumer, parentContext.ActivityContext);
        
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", ea.RoutingKey);
        activity?.SetTag("messaging.message_id", ea.BasicProperties.MessageId);

        try
        {
            var tenantId = ObterTenantIdDoHeader(ea.BasicProperties);
            
            activity?.SetTag("tenant.id", tenantId);

            await using var scope = serviceProvider.CreateAsyncScope();
            
            var tenantService = scope.ServiceProvider.GetRequiredService<TenantService>();
            tenantService.SetTenant(tenantId);
            
            var processor = scope.ServiceProvider.GetRequiredService<MessageProcessor>();

            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            
            logger.LogDebug("Processing message for Tenant {TenantId}: {msg}", tenantId, json);

            OrderMessage? message;
            try 
            {
                message = JsonSerializer.Deserialize<OrderMessage>(json);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "JSON Deserialization failed");
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false, token);
                activity?.SetStatus(ActivityStatusCode.Error, "Invalid JSON");
                return;
            }

            if (message is null)
            {
                logger.LogWarning("Message was null after deserialization");
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false, token);
                return;
            }

            await processor.Process((OrderMessage)message);

            await _channel!.BasicAckAsync(ea.DeliveryTag, false, token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            if (_channel!.IsOpen)
                await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, token);
        }
    }

    private static string ObterTenantIdDoHeader(IReadOnlyBasicProperties properties)
    {
        const string headerKey = "tenant-key"; 

        if (properties.Headers is null)
        {
            throw new InvalidOperationException("Headers da mensagem estão vazios. TenantId obrigatório.");
        }

        if (!properties.Headers.TryGetValue(headerKey, out var value) || value is not byte[] bytes)
            throw new InvalidOperationException($"Header '{headerKey}' não encontrado na mensagem.");
        
        var tenantId = Encoding.UTF8.GetString(bytes);
            
        return !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId
            : throw new InvalidOperationException("TenantId veio vazio no Header.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping RabbitMQ Consumer...");
        if (_channel?.IsOpen == true) await _channel.CloseAsync(cancellationToken);
        if (_connection?.IsOpen == true) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}