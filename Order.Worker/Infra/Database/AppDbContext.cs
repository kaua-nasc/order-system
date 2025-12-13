using Microsoft.EntityFrameworkCore;
using Order.Worker.Domain.ValueObjects;

namespace Order.Worker.Infra.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OrderProcessedValueObject> OrdersProcessed => Set<OrderProcessedValueObject>();
}