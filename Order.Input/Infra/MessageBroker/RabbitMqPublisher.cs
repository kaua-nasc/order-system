using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Order.Input.Infra.MultiTenant;
using Order.Input.Observability.Metrics;
using RabbitMQ.Client;

namespace Order.Input.Infra.MessageBroker;

public class RabbitMqPublisher(
    AppMetrics metrics,
    ConnectionFactory factory,
    TenantService tenantService) : IAsyncDisposable
{
    private IConnection? _connection;   
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public async Task InitializeAsync()
    {
        if (_connection is not null) return;
        
        await _connectionLock.WaitAsync();
        _connection = await factory.CreateConnectionAsync();
        try
        {
            if (_connection is not null) return;
            _connection = await factory.CreateConnectionAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    public async Task PublishAsync<T>(T message)
    {
        if (_connection is null) await InitializeAsync();
        await using var channel = await _connection!.CreateChannelAsync();


        var tenantId = tenantService.TenantId;
        if (tenantId is null) throw new InvalidOperationException($"nameof(tenantId) can't be null.");
        
        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                {"tenant-key", tenantId},
            },
            Persistent = true
        };
        
        var activity = Activity.Current;
        if (activity is not null)
        {
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                properties.Headers,
                (headers, key, value) => headers[key] = value);
        }
        
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "orders",
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