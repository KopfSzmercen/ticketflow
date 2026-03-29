using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketFlow.Core.Models;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Activities;

public sealed class WaitlistStateActivities(
    TicketFlowDbContext dbContext,
    ILogger<WaitlistStateActivities> logger)
{
    [Function(nameof(UpdateWaitlistState))]
    public async Task UpdateWaitlistState(
        [ActivityTrigger] Input input,
        FunctionContext executionContext)
    {
        var entry = await dbContext.WaitlistEntries
            .WithPartitionKey(input.EventId)
            .FirstOrDefaultAsync(w => w.OfferInstanceId == input.OfferInstanceId);

        if (entry is null)
        {
            logger.LogWarning(
                "Waitlist entry not found while updating state. EventId: {EventId}, OfferInstanceId: {OfferInstanceId}, Status: {Status}",
                input.EventId,
                input.OfferInstanceId,
                input.Status);

            throw new InvalidOperationException(
                $"Waitlist entry not found for event '{input.EventId}' and offer instance '{input.OfferInstanceId}'.");
        }

        var now = DateTimeOffset.UtcNow;
        switch (input.Status)
        {
            case WaitlistStatus.OfferExpired:
                if (entry.Status != WaitlistStatus.Offered) return; // safeguard
                entry.Status = WaitlistStatus.OfferExpired;
                entry.UpdatedAt = now;
                break;
            case WaitlistStatus.OfferDeclined:
                if (entry.Status != WaitlistStatus.Offered) return; // safeguard
                entry.Status = WaitlistStatus.OfferDeclined;
                entry.UpdatedAt = now;
                break;
            case WaitlistStatus.Claimed:
                if (entry.Status != WaitlistStatus.Offered) return; // safeguard
                entry.Status = WaitlistStatus.Claimed;
                entry.ClaimedAt = now;
                entry.UpdatedAt = now;
                break;
        }

        await dbContext.SaveChangesAsync();
    }

    public sealed record Input(string EventId, string OfferInstanceId, WaitlistStatus Status);
}