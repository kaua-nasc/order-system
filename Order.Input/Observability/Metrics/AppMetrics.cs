using System.Diagnostics.Metrics;

namespace Order.Input.Observability.Metrics;

public class AppMetrics : IDisposable
{
    private readonly Meter _meter = new("Order.Input", "1.0");
    private readonly Counter<long> _messagePublished;
    private readonly Counter<long> _orderCounter;
    private readonly Counter<long> _orderErrorCounter;
    private readonly Histogram<double> _processingTime;

    public AppMetrics()
    {
        _messagePublished = _meter.CreateCounter<long>("messages_published");
        _orderCounter =  _meter.CreateCounter<long>("order_counter");
        _orderErrorCounter =  _meter.CreateCounter<long>("order_error_counter");
        _processingTime = _meter.CreateHistogram<double>("processing_time");
    }

    public void IncrementPublishedMessages()
    {
        _messagePublished.Add(1);
    }

    public void IncrementOrderCounter()
    {
        _orderCounter.Add(1);
    }
    
    public void IncrementOrderErrorCounter()
    {
        _orderErrorCounter.Add(1);
    }

    public void AddProcessingTime(double ms)
    {
        _processingTime.Record(ms);
    }
    
    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }
}