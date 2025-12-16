using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Order.Worker.Domain;
using Order.Worker.Infra.Database;
using Order.Worker.Infra.MultiTenant;
using Order.Worker.Observability.Metrics;
using Order.Worker.Observability.Tracing;
using Order.Worker.Tests.Integration.Noops;
using Testcontainers.PostgreSql;

namespace Order.Worker.Tests.Integration;

public static class TestHostBuilder
{
    public static async Task<IHost> CreateAsync(PostgreSqlContainer postgres)
    {
        var host = Host.CreateDefaultBuilder()
            .UseEnvironment("Test")
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IAppTracing, NoopTracing>();
                services.AddSingleton<IAppMetrics, NoopMetrics>();
                
                services.AddScoped<MessageProcessor>();
                
                services.AddScoped<TenantService>();

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseNpgsql(postgres.GetConnectionString());
                });
                
                services.AddSingleton<ITenantConnectionProvider>(
                    new StaticTenantConnectionProvider(
                        postgres.GetConnectionString()
                    )
                );
            })
            .Build();

        await host.StartAsync();

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        return host;
    }
}
