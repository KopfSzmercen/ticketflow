using Microsoft.EntityFrameworkCore;
using TicketFlow.Core.Models;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Waitlist;

public interface IWaitlistOfferCoordinator
{
    Task<WaitlistEntry?> OfferNextWaitingEntryAsync(
        string eventId,
        int offerDurationInMinutes,
        DateTimeOffset now,
        bool saveChanges
    );
}

public sealed class WaitlistOfferCoordinator(TicketFlowDbContext dbContext) : IWaitlistOfferCoordinator
{
    public async Task<WaitlistEntry?> OfferNextWaitingEntryAsync(
        string eventId,
        int offerDurationInMinutes,
        DateTimeOffset now,
        bool saveChanges = true)
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
        nextEntry.UpdatedAt = now;

        if (saveChanges)
            await dbContext.SaveChangesAsync();

        return nextEntry;
    }
}
