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
public class CreateEventFunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Run_ShouldCreateEventAndReturnCreated()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var createEventFunction = new CreateEventFunction(dbContext);

        var request = new CreateEventFunction.Request(
            "Test Event",
            "Test Venue",
            new Money(50, "USD"),
            100,
            new DateTimeOffset(2024, 12, 31, 20, 0, 0, TimeSpan.Zero)
        );

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response =
            {
                Body = new MemoryStream()
            }
        };

        // Act
        var result = await createEventFunction.Run(request);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);

        var responseBody = httpContext.Response.Body;
        responseBody.Seek(0, SeekOrigin.Begin);

        var responseJson = await new StreamReader(responseBody).ReadToEndAsync();

        var createdEvent = JsonSerializer.Deserialize<TicketEventResponse>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        createdEvent.ShouldNotBeNull();
        createdEvent.Name.ShouldBe(request.Name);
        createdEvent.Venue.ShouldBe(request.Venue);
        createdEvent.TicketPrice.ShouldBeEquivalentTo(request.TicketPrice);
        createdEvent.TotalCapacity.ShouldBe(request.TotalCapacity);
        createdEvent.Date.ShouldBe(request.Date);
    }
}
