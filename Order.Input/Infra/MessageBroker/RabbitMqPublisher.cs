using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Order.Input.Domain.Commons;
using Order.Input.Observability.Metrics;
using Order.Input.Tracing;
using RabbitMQ.Client;

namespace Order.Input.Infra.MessageBroker;

public class RabbitMqPublisher(AppTracing tracer, AppMetrics metrics) : IMessagePublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _factory = new() { HostName = "localhost", UserName = "admin", Password = "admin"};
    private IConnection? _connection;   
    private IChannel? _channel;

    public async Task InitializeAsync()
    {
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(
            queue: "test",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }
    
    public async Task PublishAsync<T>(T message) where T : IMessage
    {
        if (_channel == null) throw new InvalidOperationException("Channel is not initialized.");

        using var activity = tracer.StartActivity("RabbitMQ Publish", ActivityKind.Producer);
        if (activity is null) return;
        
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        
        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object>(),
            Persistent = true
        };
        
        var propagator = Propagators.DefaultTextMapPropagator;
        var context = new PropagationContext(activity.Context, Baggage.Current);
        propagator.Inject(context, properties.Headers, 
            (headers, key, value) => headers[key] = value);
        
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "test",
            basicProperties: properties,
            mandatory: false,
            body: body
        );
        
        metrics.IncrementPublishedMessages();
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is null || _connection is null)
        {
            return;
        }
        
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}