using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Core.Models;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Functions.Orchestrators;

namespace TicketFlow.Functions.Waitlist;

public interface IWaitlistOfferCoordinator
{
    Task<WaitlistEntry?> OfferNextWaitingEntryAsync(
        string eventId,
        int offerDurationInMinutes,
        DateTimeOffset now,
        Money? offeredTicketPrice,
        bool saveChanges,
        DurableTaskClient durableTaskClient
    );
}

public sealed class WaitlistOfferCoordinator(
    TicketFlowDbContext dbContext) : IWaitlistOfferCoordinator
{
    public async Task<WaitlistEntry?> OfferNextWaitingEntryAsync(
        string eventId,
        int offerDurationInMinutes,
        DateTimeOffset now,
        Money? offeredTicketPrice,
        bool saveChanges,
        DurableTaskClient durableTaskClient)
    {
        if (offerDurationInMinutes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(offerDurationInMinutes),
                offerDurationInMinutes,
                "Offer duration must be greater than zero."
            );

        var nextEntry = await dbContext.WaitlistEntries
            .WithPartitionKey(eventId)
            .Where(w => w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.EnqueuedAt)
            .ThenBy(w => w.Id)
            .FirstOrDefaultAsync();

        if (nextEntry is null)
            return null;

        nextEntry.Status = WaitlistStatus.Offered;
        nextEntry.OfferInstanceId = Guid.NewGuid().ToString("N");
        nextEntry.OfferedAt = now;
        nextEntry.OfferExpiresAt = now.AddMinutes(offerDurationInMinutes);
        nextEntry.OfferedTicketPrice = offeredTicketPrice;
        nextEntry.UpdatedAt = now;

        if (saveChanges)
            await dbContext.SaveChangesAsync();

        try
        {
            await durableTaskClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(WaitlistOfferOrchestrator),
                new WaitlistOfferOrchestrator.Input(
                    nextEntry.Id,
                    nextEntry.EventId,
                    nextEntry.OfferInstanceId,
                    nextEntry.OfferExpiresAt.Value,
                    offerDurationInMinutes,
                    nextEntry.OfferedTicketPrice
                ),
                new StartOrchestrationOptions { InstanceId = nextEntry.OfferInstanceId }
            );
        }
        catch
        {
            nextEntry.Status = WaitlistStatus.Waiting;
            nextEntry.OfferInstanceId = null;
            nextEntry.OfferedAt = null;
            nextEntry.OfferExpiresAt = null;
            nextEntry.OfferedTicketPrice = null;
            nextEntry.UpdatedAt = now;

            if (saveChanges)
                await dbContext.SaveChangesAsync();

            throw;
        }

        return nextEntry;
    }
}
