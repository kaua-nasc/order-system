using System.Diagnostics;

namespace Order.Input.Tracing;

public class AppTracing : IDisposable
{
    public const string ActivitySourceName = "Order.Input";
    public const string ActivitySourceVersion = "1.0.0";
    public ActivitySource Source { get; } = new(ActivitySourceName, ActivitySourceVersion);

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return Source.StartActivity(name, kind);
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}