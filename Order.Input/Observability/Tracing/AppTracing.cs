using System.Diagnostics;
using System.Reflection;

namespace Order.Input.Observability.Tracing;

public class AppTracing : IDisposable
{
    private ActivitySource Source { get; }
    public AppTracing(IHostEnvironment env)
    {
        var serviceName = env.ApplicationName;
        var serviceVersion =
            Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? throw new InvalidOperationException();

        Source = new ActivitySource(serviceName, serviceVersion);
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return Source.StartActivity(name, kind);
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}