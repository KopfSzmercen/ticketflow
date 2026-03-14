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
public class BrowseEventsFunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Run_ShouldReturnListOfEvents()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        dbContext.Events.Add(new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Event 1",
            TicketPrice = new Money(30, "USD"),
            Venue = "Venue 1",
            TotalCapacity = 100,
            Date = new DateTimeOffset(2024, 11, 30, 19, 0, 0, TimeSpan.Zero),
            AvailableTickets = 100,
            ReservationExpirationInSeconds = 30
        });

        dbContext.Events.Add(new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Event 2",
            TicketPrice = new Money(45, "USD"),
            Venue = "Venue 2",
            TotalCapacity = 150,
            Date = new DateTimeOffset(2024, 12, 15, 20, 0, 0, TimeSpan.Zero),
            AvailableTickets = 150,
            ReservationExpirationInSeconds = 30
        });

        await dbContext.SaveChangesAsync();

        var browseEventsFunction = new BrowseEventsFunction(dbContext);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response =
            {
                Body = new MemoryStream()
            }
        };

        // Act
        var result = await browseEventsFunction.Run(httpContext.Request);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        var responseBody = httpContext.Response.Body;
        responseBody.Seek(0, SeekOrigin.Begin);

        var responseJson = await new StreamReader(responseBody).ReadToEndAsync();

        var eventsResponse = JsonSerializer.Deserialize<List<TicketEventResponse>>(responseJson,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        eventsResponse.ShouldNotBeNull();
        eventsResponse.Count.ShouldBe(2);
    }
}
