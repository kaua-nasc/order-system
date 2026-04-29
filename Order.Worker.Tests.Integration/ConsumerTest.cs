using Microsoft.Extensions.DependencyInjection;
using Order.Worker.Domain;
using Order.Worker.Domain.Entities;
using Order.Worker.Infra.Database;
using Order.Worker.Tests.Integration.Builders.Message;
using Order.Worker.Tests.Integration.Fixtures;
using Order.Worker.Domain.Exceptions;
using Order.Worker.Domain.Enums;

namespace Order.Worker.Tests.Integration;

public class ConsumerTest(StackFixture fixture) : IClassFixture<StackFixture>
{
    [Fact]
    public async Task Should_Process_Message_Successfully_And_Update_Status_When_Order_Exists()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var message = new OrderMessageBuilder()
            .WithOrderId(orderId)
            .Build();
            
        using var host = await TestHostBuilder.CreateAsync(fixture.Postgres);
        await using var scope = host.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = new OrderEntity(Guid.NewGuid(), DateTime.UtcNow, 100) { OrderId = orderId };
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var processor = scope.ServiceProvider.GetRequiredService<MessageProcessor>();
        
        // Act
        await processor.Process(message);

        // Assert
        var updatedOrder = await db.Orders.FindAsync(orderId);
        Assert.NotNull(updatedOrder);
        Assert.Equal(OrderStatus.Completed, updatedOrder.Status);
    }
    
    [Fact]
    public async Task Should_Throw_NotFoundException_When_Order_Does_Not_Exist()
    {
        // Arrange
        var message = new OrderMessageBuilder()
            .Build();
            
        using var host = await TestHostBuilder.CreateAsync(fixture.Postgres);
        await using var scope = host.Services.CreateAsyncScope();

        var processor = scope.ServiceProvider.GetRequiredService<MessageProcessor>();
        
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => processor.Process(message));
    }
}
