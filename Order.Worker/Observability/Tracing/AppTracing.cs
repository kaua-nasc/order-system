using System.Diagnostics;
using System.Reflection;

namespace Order.Worker.Observability.Tracing;

public interface IAppTracing
{
    ActivitySource Source { get; }
    
    Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext parentContext = default);
}

public sealed class AppTracing : IAppTracing, IDisposable
{
    public ActivitySource Source { get; }

    public AppTracing(IHostEnvironment env)
    {
        var serviceName = env.ApplicationName;
        var serviceVersion =
            Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? throw new InvalidOperationException();

        Source = new ActivitySource(serviceName, serviceVersion);
    }

    public Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext parentContext = default)
    {
        return Source.StartActivity(name, kind, parentContext);
    }

    public void Dispose()
    {
        Source.Dispose();
    }
}