using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Http;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Http;

[Collection("DurableIntegrationTests")]
[Trait("Category", "Integration")]
[Trait("IntegrationType", "DurableE2E")]
public class CreateOrderWithPayNowTests(DurableFunctionsHostFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await fixture.ClearDatabaseAsync();
    }

    [Fact]
    public async Task PayNow_WhenPaymentSucceeds_ShouldMarkOrderAsCompleted_AndReserveTickets()
    {
        // Arrange
        var createEventRequest = new CreateEventFunction.Request(
            "Pay Now Event",
            "Test Venue",
            new Money(50, "USD"),
            100,
            new DateTimeOffset(2027, 12, 31, 20, 0, 0, TimeSpan.Zero),
            1
        );

        var createEventResponse = await fixture.HttpClient.PostAsJsonAsync("/api/events", createEventRequest);
        createEventResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<TicketEventResponse>(JsonOptions);
        createdEvent.ShouldNotBeNull();

        var createOrderRequest = new CreateOrderFunction.Request(
            createdEvent.Id,
            "Test User",
            "test@t.com",
            new Money(50, "USD")
        );

        // Act
        var createOrderResponse = await fixture.HttpClient.PostAsJsonAsync("/api/orders", createOrderRequest);

        // Assert
        createOrderResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var createdOrder = await createOrderResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        createdOrder.ShouldNotBeNull();

        var finalOrder = await WaitForTerminalOrderStateAsync(createdOrder.Id, TimeSpan.FromSeconds(30));

        finalOrder.Status.ShouldBe(nameof(OrderStatus.Confirmed));
        finalOrder.FailureReason.ShouldBeNull();

        var getEventResponse = await fixture.HttpClient.GetAsync($"/api/events/{createdEvent.Id}");
        getEventResponse.IsSuccessStatusCode.ShouldBeTrue();
        var updatedEvent = await getEventResponse.Content.ReadFromJsonAsync<TicketEventResponse>(JsonOptions);

        updatedEvent.ShouldNotBeNull();
        updatedEvent.AvailableTickets.ShouldBe(createdEvent.AvailableTickets - 1);
    }

    [Fact]
    public async Task PayNow_WhenPaymentFails_ShouldMarkOrderAsFailed_AndNotReserveTickets()
    {
        // Arrange
        var createEventRequest = new CreateEventFunction.Request(
            "Pay Now",
            "Test Venue",
            new Money(50, "USD"),
            100,
            new DateTimeOffset(2027, 12, 31, 20, 0, 0, TimeSpan.Zero),
            1
        );

        var createEventResponse = await fixture.HttpClient.PostAsJsonAsync("/api/events", createEventRequest);
        createEventResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<TicketEventResponse>(JsonOptions);
        createdEvent.ShouldNotBeNull();

        var createOrderRequest = new CreateOrderFunction.Request(
            createdEvent.Id,
            "Test User",
            "test@t.com",
            new Money(50, "USD"),
            false
        );

        // Act
        var createOrderResponse = await fixture.HttpClient.PostAsJsonAsync("/api/orders", createOrderRequest);

        // Assert
        createOrderResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var createdOrder = await createOrderResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        createdOrder.ShouldNotBeNull();

        var finalOrder = await WaitForTerminalOrderStateAsync(createdOrder.Id, TimeSpan.FromSeconds(30));

        finalOrder.Status.ShouldBe(nameof(OrderStatus.Failed));
        finalOrder.FailureReason.ShouldNotBeNull();
        finalOrder.FailureReason.ShouldContain("Payment was declined.");

        var getEventResponse = await fixture.HttpClient.GetAsync($"/api/events/{createdEvent.Id}");
        getEventResponse.IsSuccessStatusCode.ShouldBeTrue();
        var updatedEvent = await getEventResponse.Content.ReadFromJsonAsync<TicketEventResponse>(JsonOptions);

        updatedEvent.ShouldNotBeNull();
        updatedEvent.AvailableTickets.ShouldBe(createdEvent.AvailableTickets);
    }

    [Fact]
    public async Task PayNow_WhenPaymentFails_AndWaitlistExists_ShouldOfferFirstWaitingEntry()
    {
        // Arrange
        var createEventRequest = new CreateEventFunction.Request(
            "Waitlist Trigger Event",
            "Test Venue",
            new Money(50, "USD"),
            100,
            new DateTimeOffset(2027, 12, 31, 20, 0, 0, TimeSpan.Zero),
            1
        );

        var createEventResponse = await fixture.HttpClient.PostAsJsonAsync("/api/events", createEventRequest);
        createEventResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<TicketEventResponse>(JsonOptions);
        createdEvent.ShouldNotBeNull();

        var firstInQueue = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = createdEvent.Id,
            AttendeeId = "attendee-first",
            AttendeeName = "First Queue Attendee",
            AttendeeEmail = "first@example.com",
            Status = WaitlistStatus.Waiting,
            EnqueuedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        };

        var secondInQueue = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = createdEvent.Id,
            AttendeeId = "attendee-second",
            AttendeeName = "Second Queue Attendee",
            AttendeeEmail = "second@example.com",
            Status = WaitlistStatus.Waiting,
            EnqueuedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        await using (var seedScope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            dbContext.WaitlistEntries.AddRange(firstInQueue, secondInQueue);
            await dbContext.SaveChangesAsync();
        }

        var createOrderRequest = new CreateOrderFunction.Request(
            createdEvent.Id,
            "Test User",
            "test@t.com",
            new Money(50, "USD"),
            false
        );

        // Act
        var createOrderResponse = await fixture.HttpClient.PostAsJsonAsync("/api/orders", createOrderRequest);

        // Assert
        createOrderResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var createdOrder = await createOrderResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        createdOrder.ShouldNotBeNull();

        var finalOrder = await WaitForTerminalOrderStateAsync(createdOrder.Id, TimeSpan.FromSeconds(30));
        finalOrder.Status.ShouldBe(nameof(OrderStatus.Failed));

        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var offeredEntry = await verifyDbContext.WaitlistEntries
            .WithPartitionKey(createdEvent.Id)
            .SingleAsync(w => w.Id == firstInQueue.Id);

        offeredEntry.Status.ShouldBe(WaitlistStatus.Offered);
        offeredEntry.OfferInstanceId.ShouldNotBeNullOrWhiteSpace();
        offeredEntry.OfferedAt.ShouldNotBeNull();
        offeredEntry.OfferExpiresAt.ShouldNotBeNull();

        var stillWaitingEntry = await verifyDbContext.WaitlistEntries
            .WithPartitionKey(createdEvent.Id)
            .SingleAsync(w => w.Id == secondInQueue.Id);

        stillWaitingEntry.Status.ShouldBe(WaitlistStatus.Waiting);
    }

    private async Task<OrderResponse> WaitForTerminalOrderStateAsync(string orderId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var orderResponse = await fixture.HttpClient.GetAsync($"/api/orders/{orderId}");

            if (orderResponse.IsSuccessStatusCode)
            {
                var order = await orderResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

                if (order?.Status is nameof(OrderStatus.Confirmed) or nameof(OrderStatus.Failed))
                    return order;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"Order '{orderId}' did not reach terminal state within {timeout.TotalSeconds} seconds.");
    }
}
