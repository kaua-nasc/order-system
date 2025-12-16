using Testcontainers.PostgreSql;

namespace Order.Worker.Tests.Integration.Fixtures;

public class StackFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } =
        new PostgreSqlBuilder()
            .WithImage("postgres:alpine")
            .WithUsername("admin")
            .WithPassword("admin")
            .WithDatabase("orders")
            .Build();

    public async Task InitializeAsync()
    {
        await Postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Postgres.DisposeAsync();
    }
}