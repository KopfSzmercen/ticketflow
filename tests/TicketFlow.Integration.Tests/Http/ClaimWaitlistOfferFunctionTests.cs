using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.DurableTask.Client;
using Shouldly;
using TicketFlow.Core.Models;
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
    [Fact]
    public async Task Run_ShouldRaiseEventAndReturnAccepted()
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
            AttendeeName = "Accept Attendee",
            AttendeeEmail = "accept@example.com",
            Status = WaitlistStatus.Offered,
            EnqueuedAt = now.AddMinutes(-10),
            OfferedAt = now.AddMinutes(-2),
            OfferExpiresAt = now.AddMinutes(10),
            OfferInstanceId = offerInstanceId
        };

        await dbContext.WaitlistEntries.AddAsync(offeredEntry);
        await dbContext.SaveChangesAsync();

        var durableClient = new NoOpDurableTaskClient();
        durableClient.InstanceMetadataToReturn = new OrchestrationMetadata("WaitlistOfferOrchestrator", offerInstanceId);

        var function = new ClaimWaitlistOfferFunction(
            dbContext
        );
        var request = new ClaimWaitlistOfferFunction.Request(WaitlistOfferDecisionContract.AcceptValue);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(request, offerInstanceId, durableClient);
        await result.ExecuteAsync(httpContext);

        // Assert: response contract
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();

        responseJson.ShouldContain("Decision accepted and is being processed");

        // Assert: orchestration event raised with expected payload
        durableClient.RaiseEventCalls.Count.ShouldBe(1);
        var raisedEvent = durableClient.RaiseEventCalls[0];
        raisedEvent.InstanceId.ShouldBe(offerInstanceId);
        raisedEvent.EventName.ShouldBe(WaitlistOfferDecisionContract.EventName);
        raisedEvent.EventPayload.ShouldBe(WaitlistOfferDecisionContract.AcceptValue);
    }

    [Fact]
    public async Task Run_ShouldReturnNotFound_WhenOrchestrationInstanceDoesNotExist()
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
            AttendeeId = "attendee-missing",
            AttendeeName = "Missing Orchestrator",
            AttendeeEmail = "missing@example.com",
            Status = WaitlistStatus.Offered,
            EnqueuedAt = now.AddMinutes(-10),
            OfferedAt = now.AddMinutes(-2),
            OfferExpiresAt = now.AddMinutes(10),
            OfferInstanceId = offerInstanceId
        };

        await dbContext.WaitlistEntries.AddAsync(offeredEntry);
        await dbContext.SaveChangesAsync();

        var durableClient = new NoOpDurableTaskClient
        {
            InstanceMetadataToReturn = null
        };

        var function = new ClaimWaitlistOfferFunction(
            dbContext
        );
        var request = new ClaimWaitlistOfferFunction.Request(WaitlistOfferDecisionContract.AcceptValue);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(request, offerInstanceId, durableClient);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();

        responseJson.ShouldContain("waitlist_offer_orchestration_not_found");
    }

    [Fact]
    public async Task Run_ShouldReturnServiceUnavailable_WhenRaiseEventFails()
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
            AttendeeId = "attendee-failure",
            AttendeeName = "Failure Attendee",
            AttendeeEmail = "failure@example.com",
            Status = WaitlistStatus.Offered,
            EnqueuedAt = now.AddMinutes(-10),
            OfferedAt = now.AddMinutes(-2),
            OfferExpiresAt = now.AddMinutes(10),
            OfferInstanceId = offerInstanceId
        };

        await dbContext.WaitlistEntries.AddAsync(offeredEntry);
        await dbContext.SaveChangesAsync();

        var durableClient = new NoOpDurableTaskClient
        {
            InstanceMetadataToReturn = new OrchestrationMetadata("WaitlistOfferOrchestrator", offerInstanceId),
            RaiseEventExceptionToThrow = new TimeoutException("Durable backend timeout while raising event.")
        };

        var function = new ClaimWaitlistOfferFunction(
            dbContext
        );
        var request = new ClaimWaitlistOfferFunction.Request(WaitlistOfferDecisionContract.RejectValue);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(request, offerInstanceId, durableClient);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();

        responseJson.ShouldContain("waitlist_offer_orchestration_unavailable");
    }
}
