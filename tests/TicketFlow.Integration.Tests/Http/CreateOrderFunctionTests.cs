using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Http;
using TicketFlow.Functions.Orders;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Http;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class CreateOrderFunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Run_ShouldCreatePendingOrderAndReturn202()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var ticketEvent = new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Order Test Event",
            Venue = "Test Venue",
            TicketPrice = new Money(75, "USD"),
            TotalCapacity = 50,
            Date = new DateTimeOffset(2025, 6, 15, 19, 0, 0, TimeSpan.Zero),
            AvailableTickets = 50,
            ReservationExpirationInSeconds = 30
        };

        await dbContext.Events.AddAsync(ticketEvent);
        await dbContext.SaveChangesAsync();

        var durableClient = new NoOpDurableTaskClient();
        var createOrderFunction = new CreateOrderFunction(new OrderCreationService(dbContext));

        var request = new CreateOrderFunction.Request(
            ticketEvent.Id,
            "Jane Smith",
            "jane.smith@example.com",
            new Money(75, "USD")
        );

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await createOrderFunction.Run(request, durableClient);
        await result.ExecuteAsync(httpContext);

        // Assert — HTTP 202 Accepted
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<OrderResponse>(json, JsonOptions);

        response.ShouldNotBeNull();
        response.Id.ShouldNotBeNullOrEmpty();
        response.EventId.ShouldBe(ticketEvent.Id);
        response.AttendeeName.ShouldBe("Jane Smith");
        response.AttendeeEmail.ShouldBe("jane.smith@example.com");
        response.Status.ShouldBe(nameof(OrderStatus.Pending));

        // Assert — Order persisted in Cosmos before orchestration starts
        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var persisted = await verifyContext.Orders
            .WithPartitionKey(response.Id)
            .FirstOrDefaultAsync(o => o.Id == response.Id);

        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(OrderStatus.Pending);
        persisted.EventId.ShouldBe(ticketEvent.Id);
        persisted.AttendeeName.ShouldBe("Jane Smith");
    }

    [Fact]
    public async Task Run_ShouldSetPaymentFailureFlag_WhenSimulatePaymentSuccessIsFalse()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var ticketEvent = new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Failure Test Event",
            Venue = "Test Venue",
            TicketPrice = new Money(50, "USD"),
            TotalCapacity = 10,
            Date = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero),
            AvailableTickets = 10,
            ReservationExpirationInSeconds = 30
        };

        await dbContext.Events.AddAsync(ticketEvent);
        await dbContext.SaveChangesAsync();

        var durableClient = new NoOpDurableTaskClient();
        var createOrderFunction = new CreateOrderFunction(new OrderCreationService(dbContext));

        var request = new CreateOrderFunction.Request(
            ticketEvent.Id,
            "John Doe",
            "john.doe@example.com",
            new Money(50, "USD"),
            false
        );

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await createOrderFunction.Run(request, durableClient);
        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<OrderResponse>(json, JsonOptions);

        // Assert — order created with Pending status regardless of payment flag
        response.ShouldNotBeNull();
        response.Status.ShouldBe(nameof(OrderStatus.Pending));

        // Assert — flag persisted so the orchestrator can use it
        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var persisted = await verifyContext.Orders
            .WithPartitionKey(response.Id)
            .FirstOrDefaultAsync(o => o.Id == response.Id);

        persisted.ShouldNotBeNull();
        persisted.SimulatePaymentSuccess.ShouldBeFalse();
    }
}
