using Order.Worker.Observability.Metrics;

namespace Order.Worker.Tests.Integration.Noops;

public sealed class NoopMetrics : IAppMetrics
{
    public void IncrementMessagesConsumed() { }
    public void IncrementDuplicateMessages() { }
    public void IncrementOrdersProcessed() { }
    public void IncrementProcessingErrors() { }

    public void RecordOrderValue(decimal amount) { }

    public void RecordProcessingDuration(TimeSpan duration) { }

    public void IncrementActiveProcessing() { }
    public void DecrementActiveProcessing() { }

    public void RecordOrderByValueRange(decimal amount) { }
}