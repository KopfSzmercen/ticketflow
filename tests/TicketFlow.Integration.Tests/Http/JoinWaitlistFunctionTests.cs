using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
public class JoinWaitlistFunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Run_ShouldJoinWaitlistAndReturnCreated_WhenEventIsSoldOut()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var soldOutEvent = new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Sold Out Event",
            Venue = "Main Hall",
            TicketPrice = new Money(99, "USD"),
            TotalCapacity = 100,
            AvailableTickets = 0,
            Date = new DateTimeOffset(2026, 4, 15, 20, 0, 0, TimeSpan.Zero),
            ReservationExpirationInSeconds = 20
        };

        await dbContext.Events.AddAsync(soldOutEvent);
        await dbContext.SaveChangesAsync();

        var function = new JoinWaitlistFunction(dbContext, NullLogger<JoinWaitlistFunction>.Instance);
        var request = new JoinWaitlistFunction.Request("attendee-123", "attendee@example.com");

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(request, soldOutEvent.Id);
        await result.ExecuteAsync(httpContext);

        // Assert: response contract
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<WaitlistEntryResponse>(responseJson, JsonOptions);

        response.ShouldNotBeNull();
        response.EventId.ShouldBe(soldOutEvent.Id);
        response.AttendeeId.ShouldBe(request.AttendeeId);
        response.AttendeeContact.ShouldBe(request.AttendeeContact);
        response.Status.ShouldBe(nameof(WaitlistStatus.Waiting));
        response.OfferInstanceId.ShouldBeNull();
        response.OfferedAt.ShouldBeNull();
        response.OfferExpiresAt.ShouldBeNull();
        response.ClaimedAt.ShouldBeNull();

        // Assert: persisted to Cosmos waitlist container
        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var persisted = await verifyContext.WaitlistEntries
            .WithPartitionKey(soldOutEvent.Id)
            .FirstOrDefaultAsync(w => w.Id == response.Id);

        persisted.ShouldNotBeNull();
        persisted.EventId.ShouldBe(soldOutEvent.Id);
        persisted.AttendeeId.ShouldBe(request.AttendeeId);
        persisted.AttendeeContact.ShouldBe(request.AttendeeContact);
        persisted.Status.ShouldBe(WaitlistStatus.Waiting);
    }

    [Fact]
    public async Task Run_ShouldReturnNotFound_WhenEventDoesNotExist()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var function = new JoinWaitlistFunction(dbContext, NullLogger<JoinWaitlistFunction>.Instance);
        var request = new JoinWaitlistFunction.Request("attendee-404", "attendee404@example.com");
        var missingEventId = Guid.NewGuid().ToString();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(request, missingEventId);
        await result.ExecuteAsync(httpContext);

        // Assert: response contract
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson, JsonOptions);

        response.GetProperty("error").GetString().ShouldBe("event_not_found");
        response.GetProperty("message").GetString().ShouldBe($"Event '{missingEventId}' was not found.");

        // Assert: waitlist write did not happen
        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var allWaitlistEntries = await verifyContext.WaitlistEntries.ToListAsync();
        allWaitlistEntries.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Run_ShouldReturnConflict_WhenEventHasAvailableTickets()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var eventWithAvailableSeats = new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Available Seats Event",
            Venue = "Main Hall",
            TicketPrice = new Money(45, "USD"),
            TotalCapacity = 200,
            AvailableTickets = 7,
            Date = new DateTimeOffset(2026, 5, 20, 18, 30, 0, TimeSpan.Zero),
            ReservationExpirationInSeconds = 20
        };

        await dbContext.Events.AddAsync(eventWithAvailableSeats);
        await dbContext.SaveChangesAsync();

        var function = new JoinWaitlistFunction(dbContext, NullLogger<JoinWaitlistFunction>.Instance);
        var request = new JoinWaitlistFunction.Request("attendee-409", "attendee409@example.com");

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(request, eventWithAvailableSeats.Id);
        await result.ExecuteAsync(httpContext);

        // Assert: response contract
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson, JsonOptions);

        response.GetProperty("error").GetString().ShouldBe("event_not_sold_out");
        response.GetProperty("message").GetString().ShouldBe("Waitlist can only be joined when the event is sold out.");

        // Assert: waitlist write did not happen
        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var createdForEvent = await verifyContext.WaitlistEntries
            .WithPartitionKey(eventWithAvailableSeats.Id)
            .ToListAsync();

        createdForEvent.Count.ShouldBe(0);
    }
}