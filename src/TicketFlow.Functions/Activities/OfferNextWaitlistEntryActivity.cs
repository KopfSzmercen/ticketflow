using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using TicketFlow.Core.Models;
using TicketFlow.Functions.Waitlist;

namespace TicketFlow.Functions.Activities;

public sealed class OfferNextWaitlistEntryActivity(
    IWaitlistOfferCoordinator waitlistOfferCoordinator)
{
    [Function(nameof(OfferNextWaitlistEntryActivity))]
    public async Task<Result?> RunActivity(
        [ActivityTrigger] Input input,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var now = DateTimeOffset.UtcNow;
        var nextEntry = await waitlistOfferCoordinator.OfferNextWaitingEntryAsync(
            input.EventId,
            input.OfferDurationInMinutes,
            now,
            input.OfferedTicketPrice,
            true,
            client
        );

        if (nextEntry is null)
            return null;

        return new Result(
            nextEntry.Id,
            nextEntry.EventId,
            nextEntry.AttendeeId,
            nextEntry.AttendeeEmail,
            nextEntry.OfferInstanceId!,
            nextEntry.OfferedAt!.Value,
            nextEntry.OfferExpiresAt!.Value
        );
    }

    public sealed record Input(
        string EventId,
        int OfferDurationInMinutes,
        Money? OfferedTicketPrice = null);

    public sealed record Result(
        string WaitlistEntryId,
        string EventId,
        string AttendeeId,
        string AttendeeEmail,
        string OfferInstanceId,
        DateTimeOffset OfferedAt,
        DateTimeOffset OfferExpiresAt
    );
}
