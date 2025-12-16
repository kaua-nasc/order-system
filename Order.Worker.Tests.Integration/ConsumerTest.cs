using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Worker.Domain;
using Order.Worker.Domain.Messages;
using Order.Worker.Infra.Database;
using Order.Worker.Tests.Integration.Builders.Message;
using Order.Worker.Tests.Integration.Builders.ValueObjects;
using Order.Worker.Tests.Integration.Fixtures;

namespace Order.Worker.Tests.Integration;

public class ConsumerTest(StackFixture fixture) : IClassFixture<StackFixture>
{
    [Fact]
    public async Task Should_Process_Message_And_Save_To_Database_A_Manual_Coupon()
    {
        var message = new OrderMessageBuilder()
            .WithItems([new OrderItemMessage(Guid.NewGuid(), 10)])
            .Build();
        using var host = await TestHostBuilder.CreateAsync(fixture.Postgres);
        await using var scope = host.Services.CreateAsyncScope();

        var processor = scope.ServiceProvider.GetRequiredService<MessageProcessor>();
        
        await processor.Process(message);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var quantity = await db.OrdersProcessed
            .CountAsync(x => x.OrderId == message.OrderId);
        
        Assert.Equal(1, quantity);
    }
    
    [Fact]
    public async Task Should_Process_Message_And_Ignore_When_Order_Already_Processed()
    {
        var message = new OrderMessageBuilder()
            .WithItems([new OrderItemMessage(Guid.NewGuid(), 10)])
            .Build();
        var order = new OrderProcessedValueObjectBuilder()
            .FromMessage(message)
            .Build();
        using var host = await TestHostBuilder.CreateAsync(fixture.Postgres);
        await using var scope = host.Services.CreateAsyncScope();

        var processor = scope.ServiceProvider.GetRequiredService<MessageProcessor>();
        
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await db.OrdersProcessed.AddAsync(order);

        await db.SaveChangesAsync();
        
        await processor.Process(message);

        var quantity = await db.OrdersProcessed
            .CountAsync(x => x.OrderId == message.OrderId);
        
        Assert.Equal(1, quantity);
    }
}
