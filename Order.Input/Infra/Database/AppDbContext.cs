using Microsoft.EntityFrameworkCore;
using Order.Input.Domain.Entities;
using Order.Input.Domain.Enums;
using Order.Input.Infra.MultiTenant;

namespace Order.Input.Infra.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantDatabaseProvider connectionProvider) : DbContext(options)
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        
        var connectionString = connectionProvider.GetConnectionString();
        optionsBuilder.UseNpgsql(connectionString);
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderEntity>()
            .HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId);

        modelBuilder.Entity<OrderEntity>()
            .Property(o => o.Status)
            .HasConversion(
                status => status.Value,
                value => OrderStatus.FromString(value));
            
        base.OnModelCreating(modelBuilder);
    }
}
