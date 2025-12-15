using Microsoft.EntityFrameworkCore;
using Order.Worker.Domain.ValueObjects;
using Order.Worker.Infra.MultiTenant;

namespace Order.Worker.Infra.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantConnectionProvider connectionProvider) : DbContext(options)
{
    public DbSet<OrderProcessedValueObject> OrdersProcessed => Set<OrderProcessedValueObject>();
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        
        var connectionString = connectionProvider.GetConnectionString();
        optionsBuilder.UseNpgsql(connectionString);
    }
}