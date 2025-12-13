using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Order.Input.Domain.Commons;
using Order.Input.Observability.Metrics;
using Order.Input.Observability.Tracing;
using RabbitMQ.Client;

namespace Order.Input.Infra.MessageBroker;

public class RabbitMqPublisher(AppTracing tracer, AppMetrics metrics, IConfiguration configuration) : IAsyncDisposable
{
    private readonly ConnectionFactory _factory = new()
    {
        HostName = configuration["RabbitMQ:Host"] 
            ?? throw new ArgumentNullException($"{nameof(RabbitMQ)}:Host"), 
        UserName = configuration["RabbitMQ:User"] 
            ?? throw new ArgumentNullException($"{nameof(RabbitMQ)}:User"), 
        Password = configuration["RabbitMQ:Pass"] 
            ?? throw new ArgumentNullException($"{nameof(RabbitMQ)}:Pass")
    };
    private IConnection? _connection;   
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public async Task InitializeAsync()
    {
        if (_connection is not null) return;
        
        await _connectionLock.WaitAsync();
        _connection = await _factory.CreateConnectionAsync();
        try
        {
            if (_connection is not null) return;
            _connection = await _factory.CreateConnectionAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    public async Task PublishAsync<T>(T message) where T : IMessage
    {
        if (_connection is null) await InitializeAsync();
        await using var channel = await _connection!.CreateChannelAsync();

        using var activity = tracer.StartActivity("RabbitMQ Publish", ActivityKind.Producer);
        
        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object>(),
            Persistent = true
        };
        
        if (activity is not null)
        {
            var propagator = Propagators.DefaultTextMapPropagator;
            var context = new PropagationContext(activity.Context, Baggage.Current);
            propagator.Inject(
                context, properties.Headers, 
                (headers, key, value) => headers[key] = value);
        }
        
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "test",
            basicProperties: properties,
            mandatory: false,
            body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message))
        );
        
        metrics.IncrementPublishedMessages();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}