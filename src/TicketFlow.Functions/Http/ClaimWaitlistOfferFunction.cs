using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Waitlist;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class ClaimWaitlistOfferFunction(
    TicketFlowDbContext dbContext,
    IWaitlistOfferCoordinator waitlistOfferCoordinator,
    IOptions<WaitlistOptions> waitlistOptions)
{
    private readonly int _offerDurationInMinutes = waitlistOptions.Value.OfferDurationInMinutes;

    [Function("ClaimWaitlistOffer")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/claim/{instanceId}")] [FromBody]
        Request request,
        string instanceId
    )
    {
        if (!TryParseDecision(request.Decision, out var decision))
            return Results.BadRequest(new
            {
                error = "invalid_decision",
                message = "Decision must be either 'accept' or 'reject'."
            });

        var entry = await dbContext.WaitlistEntries
            .FirstOrDefaultAsync(w => w.OfferInstanceId == instanceId);

        if (entry is null)
            return Results.NotFound(new
            {
                error = "waitlist_offer_not_found",
                message = $"No waitlist offer exists for instance '{instanceId}'."
            });

        if (entry.Status != WaitlistStatus.Offered)
            return Results.Conflict(new
            {
                error = "waitlist_offer_not_claimable",
                message = $"Waitlist offer is already in '{entry.Status}' status."
            });

        var now = DateTimeOffset.UtcNow;

        if (entry.OfferExpiresAt is not null && entry.OfferExpiresAt <= now)
        {
            entry.Status = WaitlistStatus.OfferExpired;
            entry.UpdatedAt = now;
            await dbContext.SaveChangesAsync();

            return Results.Conflict(new
            {
                error = "waitlist_offer_expired",
                message = "This waitlist offer has already expired."
            });
        }

        if (decision == ClaimDecision.Accept)
        {
            entry.Status = WaitlistStatus.Claimed;
            entry.ClaimedAt = now;
            entry.UpdatedAt = now;
        }
        else
        {
            entry.Status = WaitlistStatus.OfferDeclined;
            entry.UpdatedAt = now;

            await waitlistOfferCoordinator.OfferNextWaitingEntryAsync(
                entry.EventId,
                _offerDurationInMinutes,
                now,
                false
            );
        }

        await dbContext.SaveChangesAsync();

        return Results.Accepted(
            $"/orders/claim/{instanceId}",
            WaitlistEntryResponse.FromWaitlistEntry(entry)
        );
    }

    private static bool TryParseDecision(string? decision, out ClaimDecision parsedDecision)
    {
        if (string.Equals(decision, "accept", StringComparison.OrdinalIgnoreCase))
        {
            parsedDecision = ClaimDecision.Accept;
            return true;
        }

        if (string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase))
        {
            parsedDecision = ClaimDecision.Reject;
            return true;
        }

        parsedDecision = default;
        return false;
    }

    private enum ClaimDecision
    {
        Accept,
        Reject
    }

    public sealed record Request(string Decision);
}
