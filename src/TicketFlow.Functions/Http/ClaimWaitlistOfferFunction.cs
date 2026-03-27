using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Waitlist;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class ClaimWaitlistOfferFunction(
    TicketFlowDbContext dbContext,
    IOptions<WaitlistOptions> waitlistOptions)
{
    private readonly int _offerDurationInMinutes = waitlistOptions.Value.OfferDurationInMinutes;

    [Function("ClaimWaitlistOffer")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/claim/{instanceId}")] [FromBody]
        Request request,
        string instanceId,
        [DurableClient] DurableTaskClient durableTaskClient
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
            return Results.Conflict(new
            {
                error = "waitlist_offer_expired",
                message = "This waitlist offer has already expired."
            });
        }

        try
        {
            await durableTaskClient.RaiseEventAsync(
                instanceId,
                WaitlistOfferDecisionContract.EventName,
                decision.ToEventPayload()
            );
        }
        catch (Exception ex) when (IsOrchestrationNotFound(ex))
        {
            return Results.NotFound(new
            {
                error = "waitlist_offer_orchestration_not_found",
                message = $"No orchestration instance exists for '{instanceId}'."
            });
        }
        catch
        {
            return Results.Json(new
            {
                error = "waitlist_offer_orchestration_unavailable",
                message = "Could not process this decision because orchestration is currently unavailable. Please retry."
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        // Note: the orchestrator now handles updating the database asynchronously
        return Results.Accepted(
            $"/orders/claim/{instanceId}",
            new { message = "Decision accepted and is being processed." }
        );
    }

    private static bool TryParseDecision(string? decision, out WaitlistOfferDecision parsedDecision)
        => WaitlistOfferDecisionContract.TryParse(decision, out parsedDecision);

    private static bool IsOrchestrationNotFound(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) &&
                (message.Contains("instance", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("orchestration", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public sealed record Request(string Decision);
}
