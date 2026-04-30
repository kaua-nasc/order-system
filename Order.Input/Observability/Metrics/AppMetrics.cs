using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Order.Input.Observability.Metrics;

public class AppMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _messagePublished;
    private readonly Counter<long> _orderCounter;
    private readonly Counter<long> _orderErrorCounter;
    private readonly Histogram<double> _processingTime;

    public AppMetrics(IHostEnvironment env)
    {
        var serviceName = env.ApplicationName;
        var serviceVersion =
            Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? throw new InvalidOperationException();
        
        _meter = new Meter(serviceName, serviceVersion);
        
        _messagePublished = _meter.CreateCounter<long>("messages_published");
        _orderCounter =  _meter.CreateCounter<long>("order_counter");
        _orderErrorCounter =  _meter.CreateCounter<long>("order_error_counter");
        _processingTime = _meter.CreateHistogram<double>("processing_time");
    }

    public void IncrementPublishedMessages(string tenantId)
    {
        _messagePublished.Add(1, new TagList { { "tenant_id", tenantId } });
    }

    public void IncrementOrderCounter(string tenantId)
    {
        _orderCounter.Add(1, new TagList { { "tenant_id", tenantId } });
    }
    
    public void IncrementOrderErrorCounter(string tenantId)
    {
        _orderErrorCounter.Add(1, new TagList { { "tenant_id", tenantId } });
    }

    public void AddProcessingTime(double ms, string tenantId)
    {
        _processingTime.Record(ms, new TagList { { "tenant_id", tenantId } });
    }
    
    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }
}