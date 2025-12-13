using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Order.Worker.Domain;
using Order.Worker.Domain.Messages;
using Order.Worker.Observability.Tracing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Order.Worker.Infra;

public class RabbitMqConsumer(
    ILogger<Worker> logger, 
    MessageProcessor processor,
    AppTracing tracer)
{
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory {HostName = "localhost", UserName = "admin", Password = "admin"};
        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken:  cancellationToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken
        );
        await _channel.QueueDeclareAsync("test", false, false, false, arguments: null, cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            logger.LogInformation("Received message: {msg}", json);

            var parentContext = Propagators
                .DefaultTextMapPropagator.Extract(default, ea.BasicProperties.Headers,
                    (headers, key) =>
                    {
                        if (headers.TryGetValue(key, out var value) && value is byte[] bytes)
                            return [Encoding.UTF8.GetString(bytes)];
                        return [];
                    });

            Baggage.Current = parentContext.Baggage;

            using var activity = tracer.Source.StartActivity(
                "RabbitMQ Consume",
                ActivityKind.Consumer,
                parentContext.ActivityContext
            );

            if (activity != null)
            {
                activity.SetTag("messaging.system", "rabbitmq");
                activity.SetTag("messaging.destination", ea.RoutingKey);
                activity.SetTag("messaging.destination_kind", "queue");
            }

            OrderMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<OrderMessage>(json);
            }
            catch (JsonException)
            {
                logger.LogWarning("Invalid message format: {msg}", json);
                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
                return;
            }

            try
            {
                if (message is null) return;
            
                await processor.Process((OrderMessage)message);
                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Error processing message");

                await _channel.BasicNackAsync(
                    ea.DeliveryTag,
                    false,
                    requeue: false,
                    cancellationToken
                );
            }
        };
        
        await _channel.BasicConsumeAsync(
            queue: "test",
            autoAck: false,
            consumer: consumer,
            cancellationToken);
        
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_channel?.IsOpen == true)
                await _channel.CloseAsync(cancellationToken);

            if (_connection?.IsOpen == true)
                await _connection.CloseAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while closing RabbitMQ connection");
        }
    }
}