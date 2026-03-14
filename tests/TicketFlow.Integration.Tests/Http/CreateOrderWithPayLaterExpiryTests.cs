using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Http;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Http;

[Collection("DurableIntegrationTests")]
[Trait("Category", "Integration")]
[Trait("IntegrationType", "DurableE2E")]
public sealed class CreateOrderWithPayLaterExpiryTests(DurableFunctionsHostFixture fixture) : IAsyncLifetime
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
    public async Task PayLater_WhenTimerExpires_ShouldMarkOrderAsFailed()
    {
        // Arrange
        var createEventRequest = new CreateEventFunction.Request(
            "Pay Later Event",
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
            false,
            true
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
        finalOrder.FailureReason.ShouldContain("not completed");
    }

    [Fact]
    public async Task PayLater_WhenOrderIsConfirmedBeforeTimerExpires_ShouldNotMarkOrderAsFailed()
    {
        // Arrange
        var createEventRequest = new CreateEventFunction.Request(
            "Pay Later Event",
            "Test Venue",
            new Money(50, "USD"),
            100,
            new DateTimeOffset(2027, 12, 31, 20, 0, 0, TimeSpan.Zero),
            10
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
            true,
            true
        );

        // Act
        var createOrderResponse = await fixture.HttpClient.PostAsJsonAsync("/api/orders", createOrderRequest);

        // Assert
        createOrderResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var createdOrder = await createOrderResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        createdOrder.ShouldNotBeNull();

        var finalOrder = await WaitForTerminalOrderStateAsync(createdOrder.Id, TimeSpan.FromSeconds(30));
        finalOrder.Status.ShouldBe(nameof(OrderStatus.Confirmed));
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
