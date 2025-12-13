using System.Diagnostics.Metrics;

namespace Order.Worker.Observability.Metrics;

public class AppMetrics : IDisposable
{
    private readonly Meter _meter = new("Order.Worker", "1.0.0");
    
    // Contadores principais
    private readonly Counter<long> _messagesConsumedCounter;
    private readonly Counter<long> _duplicateMessagesCounter;
    private readonly Counter<long> _ordersProcessedCounter;
    private readonly Counter<decimal> _totalRevenueCounter;
    
    // Histogramas
    private readonly Histogram<double> _processingDurationHistogram;
    private readonly Histogram<decimal> _orderValueHistogram;
    
    // Gauges
    private readonly ObservableGauge<long> _activeProcessingGauge;
    
    // Contadores de erro
    private readonly Counter<long> _processingErrorsCounter;
    
    // Contadores por faixa de valor (cacheados)
    private readonly Counter<long> _ordersSmallCounter;
    private readonly Counter<long> _ordersMediumCounter;
    private readonly Counter<long> _ordersLargeCounter;
    private readonly Counter<long> _ordersXLargeCounter;
    
    private long _activeProcessingCount;

    public AppMetrics()
    {
        // IMPORTANTE: Use nomes compatíveis com Prometheus/Grafana
        // Prometheus recomenda: snake_case, sem pontos, prefixo
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
    public void IncrementMessagesConsumed() => _messagesConsumedCounter.Add(1);
    public void IncrementDuplicateMessages() => _duplicateMessagesCounter.Add(1);
    public void IncrementOrdersProcessed() => _ordersProcessedCounter.Add(1);
    public void IncrementProcessingErrors() => _processingErrorsCounter.Add(1);
    
    public void RecordOrderValue(decimal amount)
    {
        _totalRevenueCounter.Add(amount);
        _orderValueHistogram.Record(amount);
    }
    
    public void RecordProcessingDuration(double seconds)
    {
        _processingDurationHistogram.Record(seconds);
    }
    
    public void RecordProcessingDuration(TimeSpan duration)
    {
        _processingDurationHistogram.Record(duration.TotalSeconds);
    }
    
    public void IncrementActiveProcessing()
    {
        Interlocked.Increment(ref _activeProcessingCount);
    }
    
    public void DecrementActiveProcessing()
    {
        Interlocked.Decrement(ref _activeProcessingCount);
    }
    
    public void RecordOrderByValueRange(decimal amount)
    {
        switch (amount)
        {
            case < 50:
                _ordersSmallCounter.Add(1);
                break;
            case < 200:
                _ordersMediumCounter.Add(1);
                break;
            case < 1000:
                _ordersLargeCounter.Add(1);
                break;
            default:
                _ordersXLargeCounter.Add(1);
                break;
        }
    }
    
    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }
}