using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Order.Worker.Observability.Metrics;

public interface IAppMetrics
{
    void IncrementMessagesConsumed(string tenantId);
    void IncrementDuplicateMessages(string tenantId);
    void IncrementOrdersProcessed(string tenantId);
    void IncrementProcessingErrors(string tenantId);
    void RecordOrderValue(decimal amount, string tenantId);
    void RecordProcessingDuration(TimeSpan duration, string tenantId);
    void IncrementActiveProcessing(string tenantId);
    void DecrementActiveProcessing(string tenantId);
    void RecordOrderByValueRange(decimal amount, string tenantId);
}

public class AppMetrics : IAppMetrics, IDisposable
{
    private readonly Meter _meter;
    
    private readonly Counter<long> _messagesConsumedCounter;
    private readonly Counter<long> _duplicateMessagesCounter;
    private readonly Counter<long> _ordersProcessedCounter;
    private readonly Counter<decimal> _totalRevenueCounter;
    
    private readonly Histogram<double> _processingDurationHistogram;
    private readonly Histogram<decimal> _orderValueHistogram;

    private readonly ObservableGauge<long> _activeProcessingGauge;
    
    private readonly Counter<long> _processingErrorsCounter;
    
    private readonly Counter<long> _ordersSmallCounter;
    private readonly Counter<long> _ordersMediumCounter;
    private readonly Counter<long> _ordersLargeCounter;
    private readonly Counter<long> _ordersXLargeCounter;
    
    private long _activeProcessingCount;

    public AppMetrics(IHostEnvironment env)
    {
        var serviceName = env.ApplicationName;
        var serviceVersion =
            Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? throw new InvalidOperationException();
        
        _meter = new Meter(serviceName, serviceVersion);
        
        _messagesConsumedCounter = _meter.CreateCounter<long>(
            name: "worker_messages_consumed_total",
            unit: "messages",
            description: "Total messages consumed from queue");
        
        _duplicateMessagesCounter = _meter.CreateCounter<long>(
            name: "worker_messages_duplicate_total",
            unit: "messages", 
            description: "Total duplicate messages identified");
        
        _ordersProcessedCounter = _meter.CreateCounter<long>(
            name: "worker_orders_processed_total",
            unit: "orders",
            description: "Total orders successfully processed");
        
        _totalRevenueCounter = _meter.CreateCounter<decimal>(
            name: "worker_orders_revenue_total",
            unit: "BRL",
            description: "Total monetary value of processed orders");
        
        _orderValueHistogram = _meter.CreateHistogram<decimal>(
            name: "worker_order_value",
            unit: "BRL",
            description: "Distribution of order values");
        
        _processingDurationHistogram = _meter.CreateHistogram<double>(
            name: "worker_processing_duration_seconds",
            unit: "seconds",
            description: "Time taken to process messages");
        
        _processingErrorsCounter = _meter.CreateCounter<long>(
            name: "worker_processing_errors_total",
            unit: "errors",
            description: "Total processing errors");
        
        _activeProcessingGauge = _meter.CreateObservableGauge(
            name: "worker_processing_active",
            observeValue: () => new Measurement<long>(_activeProcessingCount),
            unit: "processes",
            description: "Currently active message processing");
        
        // Contadores por faixa de valor (cacheados)
        _ordersSmallCounter = _meter.CreateCounter<long>(
            name: "worker_orders_range_small_total",
            unit: "orders",
            description: "Total small orders (< 50 BRL)");
        
        _ordersMediumCounter = _meter.CreateCounter<long>(
            name: "worker_orders_range_medium_total",
            unit: "orders",
            description: "Total medium orders (50-199.99 BRL)");
        
        _ordersLargeCounter = _meter.CreateCounter<long>(
            name: "worker_orders_range_large_total",
            unit: "orders",
            description: "Total large orders (200-999.99 BRL)");
        
        _ordersXLargeCounter = _meter.CreateCounter<long>(
            name: "worker_orders_range_xlarge_total",
            unit: "orders",
            description: "Total xlarge orders (>= 1000 BRL)");
    }
    
    // Métodos públicos
    public void IncrementMessagesConsumed(string tenantId) => _messagesConsumedCounter.Add(1, new TagList { { "tenant_id", tenantId } });
    public void IncrementDuplicateMessages(string tenantId) => _duplicateMessagesCounter.Add(1, new TagList { { "tenant_id", tenantId } });
    public void IncrementOrdersProcessed(string tenantId) => _ordersProcessedCounter.Add(1, new TagList { { "tenant_id", tenantId } });
    public void IncrementProcessingErrors(string tenantId) => _processingErrorsCounter.Add(1, new TagList { { "tenant_id", tenantId } });
    
    public void RecordOrderValue(decimal amount, string tenantId)
    {
        _totalRevenueCounter.Add(amount, new TagList { { "tenant_id", tenantId } });
        _orderValueHistogram.Record(amount, new TagList { { "tenant_id", tenantId } });
    }
    
    public void RecordProcessingDuration(TimeSpan duration, string tenantId)
    {
        _processingDurationHistogram.Record(duration.TotalSeconds, new TagList { { "tenant_id", tenantId } });
    }
    
    public void IncrementActiveProcessing(string tenantId)
    {
        Interlocked.Increment(ref _activeProcessingCount);
        // Observables gauges don't take tags in Record/Add because they are observed, 
        // but we could use a regular Gauge or just accept that the gauge is global 
        // for now unless we change it to a non-observable one. 
        // For simplicity, keeping it as is but accepting the param.
    }
    
    public void DecrementActiveProcessing(string tenantId)
    {
        Interlocked.Decrement(ref _activeProcessingCount);
    }
    
    public void RecordOrderByValueRange(decimal amount, string tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId } };
        switch (amount)
        {
            case < 50:
                _ordersSmallCounter.Add(1, tags);
                break;
            case < 200:
                _ordersMediumCounter.Add(1, tags);
                break;
            case < 1000:
                _ordersLargeCounter.Add(1, tags);
                break;
            default:
                _ordersXLargeCounter.Add(1, tags);
                break;
        }
    }
    
    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }
}