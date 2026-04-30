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
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Order.Worker.Infra.MessageBroker.Consumers;

public class RabbitMqConsumer : BackgroundService
{
    private readonly IAsyncPolicy _resiliencePolicy;
    private readonly IAsyncPolicy _messageProcessingPolicy;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly IAppTracing _tracer;
    private readonly RabbitMqOptions _options;
    private readonly ConnectionFactory _factory;
    
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConsumer(
        IServiceProvider serviceProvider,
        ILogger<RabbitMqConsumer> logger,
        IAppTracing tracer,
        RabbitMqOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _tracer = tracer;
        _options = options;
        _factory = new()
        {
            HostName = options.Hostname,
            UserName = options.Username,
            Password = options.Password,
            AutomaticRecoveryEnabled = true
        };

        _resiliencePolicy = Policy.WrapAsync(
            Policy.Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    retryAttempt => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, retryAttempt), 30)),
                    (ex, retryCount, time) => 
                    {
                        _logger.LogWarning(ex, "Falha na conexão com RabbitMQ. Tentativa {RetryCount}. Retentando em {Time}s...", retryCount, time.TotalSeconds);
                        var activity = Activity.Current;
                        var tags = new ActivityTagsCollection 
                        { 
                            { "retry.count", retryCount },
                            { "retry.wait_seconds", time.TotalSeconds },
                            { "exception.message", ex.Message }
                        };
                        activity?.AddEvent(new ActivityEvent("rabbitmq.connection.retry", tags: tags));
                        return Task.CompletedTask;
                    }),
            Policy.Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, time) => 
                    {
                        _logger.LogCritical(ex, "CIRCUITO ABERTO! RabbitMQ inacessível. Pausando tentativas por {Time}s", time.TotalSeconds);
                        var activity = Activity.Current;
                        activity?.SetTag("circuit_breaker.state", "open");
                        var tags = new ActivityTagsCollection { { "break.duration", time.TotalSeconds } };
                        activity?.AddEvent(new ActivityEvent("circuit_breaker.opened", tags: tags));
                    },
                    onReset: () => 
                    {
                        _logger.LogInformation("Circuito fechado. Tentando reconectar...");
                        var activity = Activity.Current;
                        activity?.SetTag("circuit_breaker.state", "closed");
                        activity?.AddEvent(new ActivityEvent("circuit_breaker.closed"));
                    })
        );

        _messageProcessingPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(3, 
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time, retryCount, context) => 
                {
                    _logger.LogWarning(ex, "Falha no processamento da mensagem. Tentativa {RetryCount}. Retentando em {Time}s...", retryCount, time.TotalSeconds);
                    var activity = Activity.Current;
                    var tags = new ActivityTagsCollection 
                    { 
                        { "retry.count", retryCount },
                        { "retry.wait_seconds", time.TotalSeconds },
                        { "exception.type", ex.GetType().Name },
                        { "exception.message", ex.Message }
                    };
                    activity?.AddEvent(new ActivityEvent("message.processing.retry", tags: tags));
                });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _resiliencePolicy.ExecuteAsync(async () =>
                {
                    using var activity = _tracer.StartActivity("RabbitMQ Connection Attempt", ActivityKind.Internal);
                    await InitializeRabbitMq(stoppingToken);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Se o Circuit Breaker lançar BrokenCircuitException, ele cai aqui
                // O loop while garante que após o tempo de break, ele tente novamente via Política
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

        await _channel.BasicConsumeAsync("orders-processor", false, consumer, token);
        _logger.LogInformation("Listening for messages on queue 'orders-processor'...");
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

        using var activity = _tracer.Source.StartActivity("Process Order", ActivityKind.Consumer, parentContext.ActivityContext);
        
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", ea.RoutingKey);
        activity?.SetTag("messaging.message_id", ea.BasicProperties.MessageId);

        try
        {
            var tenantId = ObterTenantIdDoHeader(ea.BasicProperties);
            
            activity?.SetTag("tenant.id", tenantId);

            await using var scope = _serviceProvider.CreateAsyncScope();
            
            var tenantService = scope.ServiceProvider.GetRequiredService<TenantService>();
            tenantService.SetTenant(tenantId);
            
            var processor = scope.ServiceProvider.GetRequiredService<MessageProcessor>();

            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            
            _logger.LogDebug("Processing message for Tenant {TenantId}: {msg}", tenantId, json);

            OrderMessage? message;
            try 
            {
                message = JsonSerializer.Deserialize<OrderMessage>(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON Deserialization failed");
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false, token);
                activity?.SetStatus(ActivityStatusCode.Error, "Invalid JSON");
                return;
            }

            if (message is null)
            {
                _logger.LogWarning("Message was null after deserialization");
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false, token);
                return;
            }

            await _messageProcessingPolicy.ExecuteAsync(async () => await processor.Process((OrderMessage)message));

            await _channel!.BasicAckAsync(ea.DeliveryTag, false, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message after retries");
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
        _logger.LogInformation("Stopping RabbitMQ Consumer...");
        if (_channel?.IsOpen == true) await _channel.CloseAsync(cancellationToken);
        if (_connection?.IsOpen == true) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}