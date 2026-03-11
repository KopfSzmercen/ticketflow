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
public class GetEventFunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Run_ShouldReturnEventWhenFound()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var ticketEvent = new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Event",
            Venue = "Test Venue",
            TicketPrice = new Money(50, "USD"),
            TotalCapacity = 100,
            Date = new DateTimeOffset(2024, 12, 31, 20, 0, 0, TimeSpan.Zero),
            AvailableTickets = 100
        };

        await dbContext.Events.AddAsync(ticketEvent);
        await dbContext.SaveChangesAsync();

        var getEventFunction = new GetEventFunction(dbContext);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response =
            {
                Body = new MemoryStream()
            }
        };

        // Act
        var result = await getEventFunction.Run(httpContext.Request, ticketEvent.Id);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        var responseBody = httpContext.Response.Body;
        responseBody.Seek(0, SeekOrigin.Begin);

        var responseJson = await new StreamReader(responseBody).ReadToEndAsync();

        var retrievedEvent = JsonSerializer.Deserialize<TicketEventResponse>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        retrievedEvent.ShouldNotBeNull();
        retrievedEvent.Id.ShouldBe(ticketEvent.Id);
        retrievedEvent.Name.ShouldBe(ticketEvent.Name);
        retrievedEvent.Venue.ShouldBe(ticketEvent.Venue);
        retrievedEvent.TicketPrice.ShouldBeEquivalentTo(ticketEvent.TicketPrice);
        retrievedEvent.TotalCapacity.ShouldBe(ticketEvent.TotalCapacity);
        retrievedEvent.Date.ShouldBe(ticketEvent.Date);
        retrievedEvent.AvailableTickets.ShouldBe(ticketEvent.AvailableTickets);
    }
}
