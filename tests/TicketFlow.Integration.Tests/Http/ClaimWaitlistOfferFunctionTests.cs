using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Http;
using TicketFlow.Functions.Waitlist;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Http;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class ClaimWaitlistOfferFunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Run_ShouldAcceptOfferAndMarkWaitlistEntryAsClaimed()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var eventId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var offerInstanceId = Guid.NewGuid().ToString("N");

        var offeredEntry = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            AttendeeId = "attendee-accept",
            AttendeeContact = "accept@example.com",
            Status = WaitlistStatus.Offered,
            EnqueuedAt = now.AddMinutes(-10),
            OfferedAt = now.AddMinutes(-2),
            OfferExpiresAt = now.AddMinutes(10),
            OfferInstanceId = offerInstanceId
        };

        await dbContext.WaitlistEntries.AddAsync(offeredEntry);
        await dbContext.SaveChangesAsync();

        var function = new ClaimWaitlistOfferFunction(
            dbContext, 
            new WaitlistOfferCoordinator(dbContext),
            Options.Create(new WaitlistOptions())
        );
        var request = new ClaimWaitlistOfferFunction.Request("accept");

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(request, offerInstanceId);
        await result.ExecuteAsync(httpContext);

        // Assert: response contract
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<WaitlistEntryResponse>(responseJson, JsonOptions);

        response.ShouldNotBeNull();
        response.Id.ShouldBe(offeredEntry.Id);
        response.Status.ShouldBe(nameof(WaitlistStatus.Claimed));
        response.ClaimedAt.ShouldNotBeNull();

        // Assert: persisted state
        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var persisted = await verifyContext.WaitlistEntries
            .WithPartitionKey(eventId)
            .SingleAsync(w => w.Id == offeredEntry.Id);

        persisted.Status.ShouldBe(WaitlistStatus.Claimed);
        persisted.ClaimedAt.ShouldNotBeNull();
        persisted.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Run_ShouldRejectOfferAndMarkWaitlistEntryAsDeclined()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var eventId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var offerInstanceId = Guid.NewGuid().ToString("N");

        var offeredEntry = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            AttendeeId = "attendee-reject",
            AttendeeContact = "reject@example.com",
            Status = WaitlistStatus.Offered,
            EnqueuedAt = now.AddMinutes(-10),
            OfferedAt = now.AddMinutes(-1),
            OfferExpiresAt = now.AddMinutes(10),
            OfferInstanceId = offerInstanceId
        };

        var nextWaitingEntry = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            AttendeeId = "attendee-next",
            AttendeeContact = "next@example.com",
            Status = WaitlistStatus.Waiting,
            EnqueuedAt = now.AddMinutes(-5)
        };

        await dbContext.WaitlistEntries.AddRangeAsync(offeredEntry, nextWaitingEntry);
        await dbContext.SaveChangesAsync();

        var function = new ClaimWaitlistOfferFunction(
            dbContext, 
            new WaitlistOfferCoordinator(dbContext),
            Options.Create(new WaitlistOptions())
        );
        var request = new ClaimWaitlistOfferFunction.Request("reject");

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(request, offerInstanceId);
        await result.ExecuteAsync(httpContext);

        // Assert: response contract
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<WaitlistEntryResponse>(responseJson, JsonOptions);

        response.ShouldNotBeNull();
        response.Id.ShouldBe(offeredEntry.Id);
        response.Status.ShouldBe(nameof(WaitlistStatus.OfferDeclined));
        response.ClaimedAt.ShouldBeNull();

        // Assert: persisted state
        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var persisted = await verifyContext.WaitlistEntries
            .WithPartitionKey(eventId)
            .SingleAsync(w => w.Id == offeredEntry.Id);

        persisted.Status.ShouldBe(WaitlistStatus.OfferDeclined);
        persisted.ClaimedAt.ShouldBeNull();
        persisted.UpdatedAt.ShouldNotBeNull();

        var rolledOverOffer = await verifyContext.WaitlistEntries
            .WithPartitionKey(eventId)
            .SingleAsync(w => w.Id == nextWaitingEntry.Id);

        rolledOverOffer.Status.ShouldBe(WaitlistStatus.Offered);
        rolledOverOffer.OfferInstanceId.ShouldNotBeNullOrWhiteSpace();
        rolledOverOffer.OfferedAt.ShouldNotBeNull();
        rolledOverOffer.OfferExpiresAt.ShouldNotBeNull();
    }
}
