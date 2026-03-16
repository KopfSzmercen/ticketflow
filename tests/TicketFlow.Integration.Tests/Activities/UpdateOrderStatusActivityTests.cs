using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TicketFlow.Core.Models;
using TicketFlow.Functions.Activities;
using TicketFlow.Functions.DTO;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Activities;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class UpdateOrderStatusActivityTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task RunActivity_WhenStatusIsConfirmed_ShouldRaiseOrderCompletedEvent()
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = "event-123",
            AttendeeName = "Test User",
            AttendeeEmail = "test@ticketflow.dev",
            TicketPrice = new Money(50m, "USD"),
            Status = OrderStatus.Paying
        };

        await using (var seedScope = Fixture.Services.CreateAsyncScope())
        {
            var seedDbContext = seedScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            seedDbContext.Orders.Add(order);
            await seedDbContext.SaveChangesAsync();
        }

        var publisher = new CapturingOrderCompletedEventPublisher();

        await using (var activityScope = Fixture.Services.CreateAsyncScope())
        {
            var activityDbContext = activityScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            var activity = new UpdateOrderStatusActivity(activityDbContext, publisher);

            await activity.RunActivity(
                new UpdateOrderStatusActivity.Input(order.Id, OrderStatus.Confirmed),
                null!
            );
        }

        await using (var assertScope = Fixture.Services.CreateAsyncScope())
        {
            var assertDbContext = assertScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            var updatedOrder = await assertDbContext.Orders
                .WithPartitionKey(order.Id)
                .SingleAsync(o => o.Id == order.Id);

            updatedOrder.Status.ShouldBe(OrderStatus.Confirmed);
            updatedOrder.UpdatedAt.ShouldNotBeNull();
        }

        publisher.PublishedEvents.Count.ShouldBe(1);

        var publishedEvent = publisher.PublishedEvents[0];
        publishedEvent.OrderId.ShouldBe(order.Id);
        publishedEvent.CustomerEmail.ShouldBe(order.AttendeeEmail);
        publishedEvent.EventName.ShouldBe(order.EventId);
    }

    private sealed class CapturingOrderCompletedEventPublisher : IOrderCompletedEventPublisher
    {
        public List<OrderCompletedEvent> PublishedEvents { get; } = [];

        public Task PublishAsync(Order order, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(new OrderCompletedEvent(
                order.Id,
                order.AttendeeEmail,
                order.EventId,
                1,
                order.TicketPrice.Amount,
                order.TicketPrice.Currency
            ));

            return Task.CompletedTask;
        }
    }
}
