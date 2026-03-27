using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using TicketFlow.Functions.Activities;
using TicketFlow.Functions.Waitlist;

namespace TicketFlow.Functions.Orchestrators;

public sealed class WaitlistOfferOrchestrator
{
    [Function(nameof(WaitlistOfferOrchestrator))]
    public async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<Input>() ?? throw new ArgumentNullException("Input missing");

        var expirationTimer = context.CreateTimer(input.OfferExpiresAt.UtcDateTime, CancellationToken.None);
        var claimEvent = context.WaitForExternalEvent<string>(WaitlistOfferDecisionContract.EventName);

        var winner = await Task.WhenAny(expirationTimer, claimEvent);

        if (winner == expirationTimer)
        {
            await context.CallActivityAsync(
                nameof(WaitlistStateActivities.UpdateWaitlistState),
                new WaitlistStateActivities.Input(input.EventId, input.OfferInstanceId, "OfferExpired")
            );

            await context.CallActivityAsync<OfferNextWaitlistEntryActivity.Result?>(
                nameof(OfferNextWaitlistEntryActivity),
                new OfferNextWaitlistEntryActivity.Input(input.EventId, input.OfferDurationInMinutes)
            );
        }
        else
        {
            if (!WaitlistOfferDecisionContract.TryParse(claimEvent.Result, out var decision))
                throw new InvalidOperationException($"Unsupported waitlist decision: '{claimEvent.Result}'.");

            if (decision == WaitlistOfferDecision.Reject)
            {
                await context.CallActivityAsync(
                    nameof(WaitlistStateActivities.UpdateWaitlistState),
                    new WaitlistStateActivities.Input(input.EventId, input.OfferInstanceId, "OfferDeclined")
                );

                await context.CallActivityAsync<OfferNextWaitlistEntryActivity.Result?>(
                    nameof(OfferNextWaitlistEntryActivity),
                    new OfferNextWaitlistEntryActivity.Input(input.EventId, input.OfferDurationInMinutes)
                );
            }
            else if (decision == WaitlistOfferDecision.Accept)
            {
                await context.CallActivityAsync(
                    nameof(WaitlistStateActivities.UpdateWaitlistState),
                    new WaitlistStateActivities.Input(input.EventId, input.OfferInstanceId, "Claimed")
                );
                
                // Triggers purchase logic or PlaceOrderOrchestrator
                // For now, simple transition
            }
        }
    }

    public sealed record Input(string WaitlistEntryId, string EventId, string OfferInstanceId, DateTimeOffset OfferExpiresAt, int OfferDurationInMinutes);
}
