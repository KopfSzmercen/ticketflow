using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Http;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Http;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class GetOrderFunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Run_ShouldReturnOrderWhenFound()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            EventId = Guid.NewGuid().ToString(),
            AttendeeName = "Alice Tester",
            AttendeeEmail = "alice@example.com",
            TicketPrice = new Money(100, "USD"),
            SimulatePaymentSuccess = true,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        var durableClient = new NoOpDurableTaskClient();
        var getOrderFunction = new GetOrderFunction(dbContext);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await getOrderFunction.Run(httpContext.Request, order.Id, durableClient);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<OrderResponse>(json, JsonOptions);

        response.ShouldNotBeNull();
        response.Id.ShouldBe(order.Id);
        response.EventId.ShouldBe(order.EventId);
        response.AttendeeName.ShouldBe(order.AttendeeName);
        response.AttendeeEmail.ShouldBe(order.AttendeeEmail);
        response.Status.ShouldBe(nameof(OrderStatus.Confirmed));
        response.OrchestrationStatus.ShouldBeNull();
    }

    [Fact]
    public async Task Run_ShouldReturn404_WhenOrderNotFound()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var durableClient = new NoOpDurableTaskClient();
        var getOrderFunction = new GetOrderFunction(dbContext);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await getOrderFunction.Run(
            httpContext.Request,
            Guid.NewGuid().ToString(),
            durableClient);

        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Run_ShouldBeAvailableImmediatelyAfterPost()
    {
        // Demonstrates that because the Order is created in Cosmos before the
        // orchestration starts, GET works the instant 202 is returned.
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var ticketEvent = new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Immediate Availability Event",
            Venue = "Main Stage",
            TicketPrice = new Money(60, "USD"),
            TotalCapacity = 20,
            Date = new DateTimeOffset(2025, 9, 1, 20, 0, 0, TimeSpan.Zero),
            AvailableTickets = 20
        };

        await dbContext.Events.AddAsync(ticketEvent);
        await dbContext.SaveChangesAsync();

        var durableClient = new NoOpDurableTaskClient();
        var createOrderFunction = new CreateOrderFunction(dbContext);

        var postRequest = new CreateOrderFunction.Request(
            ticketEvent.Id,
            "Bob Buyer",
            "bob@example.com",
            new Money(60, "USD")
        );

        // Act — POST /orders
        var createHttpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };
        var createResult = await createOrderFunction.Run(postRequest, durableClient);
        await createResult.ExecuteAsync(createHttpContext);

        createHttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var createJson = await new StreamReader(createHttpContext.Response.Body).ReadToEndAsync();
        var createdOrder = JsonSerializer.Deserialize<OrderResponse>(createJson, JsonOptions);
        createdOrder.ShouldNotBeNull();

        // Act — GET /orders/{orderId} immediately after POST
        var getHttpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Use a new dbContext scope to ensure we're reading from Cosmos, not cache
        await using var getScope = Fixture.Services.CreateAsyncScope();
        var getDbContext = getScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var getFunction = new GetOrderFunction(getDbContext);

        var getResult = await getFunction.Run(getHttpContext.Request, createdOrder.Id, durableClient);
        await getResult.ExecuteAsync(getHttpContext);

        // Assert — order exists immediately; no race window
        getHttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        getHttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var getJson = await new StreamReader(getHttpContext.Response.Body).ReadToEndAsync();
        var retrievedOrder = JsonSerializer.Deserialize<OrderResponse>(getJson, JsonOptions);

        retrievedOrder.ShouldNotBeNull();
        retrievedOrder.Id.ShouldBe(createdOrder.Id);
        retrievedOrder.Status.ShouldBe(nameof(OrderStatus.Pending));
    }
}
