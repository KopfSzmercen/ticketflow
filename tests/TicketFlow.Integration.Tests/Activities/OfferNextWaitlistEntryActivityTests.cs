using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TicketFlow.Core.Models;
using TicketFlow.Functions.Activities;
using TicketFlow.Functions.Waitlist;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Activities;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class OfferNextWaitlistEntryActivityTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task RunActivity_ShouldOfferEarliestWaitingEntry_WhenQueueHasWaitingEntries()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var firstWaiting = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            AttendeeId = "attendee-first",
            AttendeeContact = "first@example.com",
            Status = WaitlistStatus.Waiting,
            EnqueuedAt = now.AddMinutes(-20)
        };

        var secondWaiting = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            AttendeeId = "attendee-second",
            AttendeeContact = "second@example.com",
            Status = WaitlistStatus.Waiting,
            EnqueuedAt = now.AddMinutes(-10)
        };

        var alreadyOffered = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            AttendeeId = "attendee-offered",
            AttendeeContact = "offered@example.com",
            Status = WaitlistStatus.Offered,
            EnqueuedAt = now.AddMinutes(-30),
            OfferedAt = now.AddMinutes(-5),
            OfferExpiresAt = now.AddMinutes(5),
            OfferInstanceId = Guid.NewGuid().ToString("N")
        };

        await using (var seedScope = Fixture.Services.CreateAsyncScope())
        {
            var seedDbContext = seedScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            seedDbContext.WaitlistEntries.AddRange(firstWaiting, secondWaiting, alreadyOffered);
            await seedDbContext.SaveChangesAsync();
        }

        OfferNextWaitlistEntryActivity.Result? result;
        await using (var activityScope = Fixture.Services.CreateAsyncScope())
        {
            var activityDbContext = activityScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            var activity = new OfferNextWaitlistEntryActivity(new WaitlistOfferCoordinator(activityDbContext));
            result = await activity.RunActivity(
                new OfferNextWaitlistEntryActivity.Input(eventId, OfferDurationInMinutes: 15),
                null!
            );
        }

        // Assert: returned result corresponds to the earliest waiting entry
        result.ShouldNotBeNull();
        result.WaitlistEntryId.ShouldBe(firstWaiting.Id);
        result.EventId.ShouldBe(eventId);
        result.AttendeeId.ShouldBe(firstWaiting.AttendeeId);
        result.AttendeeContact.ShouldBe(firstWaiting.AttendeeContact);
        result.OfferInstanceId.ShouldNotBeNullOrWhiteSpace();
        result.OfferExpiresAt.ShouldBeGreaterThan(result.OfferedAt);

        var offerDuration = result.OfferExpiresAt - result.OfferedAt;
        offerDuration.TotalMinutes.ShouldBeGreaterThanOrEqualTo(14.0d);
        offerDuration.TotalMinutes.ShouldBeLessThanOrEqualTo(16.0d);

        // Assert: only the first waiting entry moved to Offered
        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var persistedFirst = await verifyDbContext.WaitlistEntries
            .WithPartitionKey(eventId)
            .SingleAsync(w => w.Id == firstWaiting.Id);

        persistedFirst.Status.ShouldBe(WaitlistStatus.Offered);
        persistedFirst.OfferInstanceId.ShouldNotBeNullOrWhiteSpace();
        persistedFirst.OfferedAt.ShouldNotBeNull();
        persistedFirst.OfferExpiresAt.ShouldNotBeNull();
        persistedFirst.UpdatedAt.ShouldNotBeNull();

        var persistedSecond = await verifyDbContext.WaitlistEntries
            .WithPartitionKey(eventId)
            .SingleAsync(w => w.Id == secondWaiting.Id);

        persistedSecond.Status.ShouldBe(WaitlistStatus.Waiting);
        persistedSecond.OfferInstanceId.ShouldBeNull();
        persistedSecond.OfferedAt.ShouldBeNull();
        persistedSecond.OfferExpiresAt.ShouldBeNull();

        var persistedOffered = await verifyDbContext.WaitlistEntries
            .WithPartitionKey(eventId)
            .SingleAsync(w => w.Id == alreadyOffered.Id);

        persistedOffered.Status.ShouldBe(WaitlistStatus.Offered);
    }

    [Fact]
    public async Task RunActivity_ShouldReturnNull_WhenNoWaitingEntriesExist()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        var alreadyOffered = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            AttendeeId = "attendee-offered",
            AttendeeContact = "offered@example.com",
            Status = WaitlistStatus.Offered,
            EnqueuedAt = now.AddMinutes(-30),
            OfferedAt = now.AddMinutes(-10),
            OfferExpiresAt = now.AddMinutes(5),
            OfferInstanceId = Guid.NewGuid().ToString("N")
        };

        await using (var seedScope = Fixture.Services.CreateAsyncScope())
        {
            var seedDbContext = seedScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            seedDbContext.WaitlistEntries.Add(alreadyOffered);
            await seedDbContext.SaveChangesAsync();
        }

        // Act
        OfferNextWaitlistEntryActivity.Result? result;
        await using (var activityScope = Fixture.Services.CreateAsyncScope())
        {
            var activityDbContext = activityScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            var activity = new OfferNextWaitlistEntryActivity(new WaitlistOfferCoordinator(activityDbContext));
            result = await activity.RunActivity(
                new OfferNextWaitlistEntryActivity.Input(eventId, OfferDurationInMinutes: 15),
                null!
            );
        }

        // Assert
        result.ShouldBeNull();

        await using var verifyScope = Fixture.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var persisted = await verifyDbContext.WaitlistEntries
            .WithPartitionKey(eventId)
            .SingleAsync(w => w.Id == alreadyOffered.Id);

        persisted.Status.ShouldBe(WaitlistStatus.Offered);
    }
}