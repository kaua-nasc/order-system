using System.Diagnostics;
using Order.Worker.Observability.Tracing;

namespace Order.Worker.Tests.Integration.Noops;

public sealed class NoopTracing : IAppTracing
{
    private static readonly ActivitySource _source =
        new("noop");

    public ActivitySource Source => _source;

    public Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext parentContext = default)
    {
        return null;
    }
}