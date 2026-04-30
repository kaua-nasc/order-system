using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Order.Input.Infra.MultiTenant;
using Order.Input.Observability.Metrics;
using Polly;
using RabbitMQ.Client;

namespace Order.Input.Infra.MessageBroker;

public class RabbitMqPublisher : IAsyncDisposable
{
    private readonly AppMetrics _metrics;
    private readonly ConnectionFactory _factory;
    private readonly TenantService _tenantService;
    private readonly string _queueName;
    private readonly IAsyncPolicy _retryPolicy;
    private IConnection? _connection;   
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public RabbitMqPublisher(
        AppMetrics metrics,
        ConnectionFactory factory,
        TenantService tenantService,
        IConfiguration configuration)
    {
        _metrics = metrics;
        _factory = factory;
        _tenantService = tenantService;
        _queueName = configuration["rabbitmq:queue_name"] ?? "orders";
        
        _retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(3, 
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time, retryCount, context) => 
                {
                    var activity = Activity.Current;
                    var tags = new ActivityTagsCollection 
                    { 
                        { "retry.count", retryCount },
                        { "retry.wait_seconds", time.TotalSeconds },
                        { "exception.type", ex.GetType().Name },
                        { "exception.message", ex.Message }
                    };
                    activity?.AddEvent(new ActivityEvent("rabbitmq.publish.retry", tags: tags));
                });
    }

    public async Task InitializeAsync()
    {
        if (_connection is not null) return;
        
        await _connectionLock.WaitAsync();
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
    
    public async Task PublishAsync<T>(T message)
    {
        await _retryPolicy.ExecuteAsync(async () => 
        {
            if (_connection is null) await InitializeAsync();
            await using var channel = await _connection!.CreateChannelAsync();

            var tenantId = _tenantService.TenantId;
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
            
            try
            {
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _queueName,
                    basicProperties: properties,
                    mandatory: false,
                    body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message))
                );
                
                _metrics.IncrementPublishedMessages(tenantId);
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        });
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