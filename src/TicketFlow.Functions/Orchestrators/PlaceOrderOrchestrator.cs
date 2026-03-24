using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Options;
using TicketFlow.Core.Models;
using TicketFlow.Functions.Activities;
using TicketFlow.Functions.Waitlist;

namespace TicketFlow.Functions.Orchestrators;

public sealed record PlaceOrderInput(string EventId, bool SimulatePaymentSuccess, bool PayLater = false);

/// <summary>
///     Durable orchestrator that coordinates the single-ticket purchase flow.
///     The Durable instance id is always equal to the <c>orderId</c> stored in Cosmos,
///     so <see cref="TaskOrchestrationContext.InstanceId" /> can be used as the order
///     identifier in every activity call.
/// </summary>
public class PlaceOrderOrchestrator(IOptions<WaitlistOptions> waitlistOptions)
{
    private readonly int _offerDurationInMinutes = waitlistOptions.Value.OfferDurationInMinutes;

    [Function(nameof(PlaceOrderOrchestrator))]
    public async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        var input = context.GetInput<PlaceOrderInput>()
                    ?? throw new InvalidOperationException("Orchestrator input is required.");

        if (input.PayLater)
            await OrchestratePayLaterProcess(context, input);
        else
            await OrchestratePayNowProcess(context, input);
    }

    private async Task OrchestratePayNowProcess(
        TaskOrchestrationContext context,
        PlaceOrderInput input
    )
    {
        var orderId = context.InstanceId;

        await context.CallActivityAsync(
            nameof(UpdateOrderStatusActivity),
            new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Reserving)
        );

        var reserved = await context.CallActivityAsync<bool>(
            nameof(ReserveTicketActivity),
            new ReserveTicketActivity.Input(orderId, input.EventId)
        );

        if (!reserved)
        {
            await context.CallActivityAsync(
                nameof(UpdateOrderStatusActivity),
                new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Failed,
                    "Ticket reservation failed — no available tickets.")
            );
            return;
        }

        await context.CallActivityAsync(
            nameof(UpdateOrderStatusActivity),
            new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Paying));

        var paymentSucceeded = await context.CallActivityAsync<bool>(
            nameof(ProcessPaymentActivity),
            new ProcessPaymentActivity.Input(orderId, input.SimulatePaymentSuccess)
        );

        if (paymentSucceeded)
        {
            await context.CallActivityAsync(
                nameof(UpdateOrderStatusActivity),
                new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Confirmed)
            );
        }
        else
        {
            await context.CallActivityAsync(
                nameof(ReleaseTicketActivity),
                new ReleaseTicketActivity.Input(orderId, input.EventId)
            );

            await context.CallActivityAsync<OfferNextWaitlistEntryActivity.Result?>(
                nameof(OfferNextWaitlistEntryActivity),
                new OfferNextWaitlistEntryActivity.Input(input.EventId, _offerDurationInMinutes)
            );

            await context.CallActivityAsync(
                nameof(UpdateOrderStatusActivity),
                new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Failed, "Payment was declined."));
        }
    }

    private async Task OrchestratePayLaterProcess(TaskOrchestrationContext context, PlaceOrderInput input)
    {
        var orderId = context.InstanceId;

        await context.CallActivityAsync(
            nameof(UpdateOrderStatusActivity),
            new UpdateOrderStatusActivity.Input(
                orderId,
                OrderStatus.Pending,
                "Awaiting payment — customer chose to pay later."
            )
        );

        var eventDetails = await context.CallActivityAsync<GetEventActivity.Result?>(
            nameof(GetEventActivity),
            input.EventId
        );

        if (eventDetails is null)
        {
            await context.CallActivityAsync(
                nameof(UpdateOrderStatusActivity),
                new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Failed, "Event not found.")
            );
            return;
        }

        await context.CreateTimer(
            context.CurrentUtcDateTime.AddSeconds(eventDetails.ReservationExpirationInSeconds),
            CancellationToken.None
        );

        var paymentCompleted = await context.CallActivityAsync<bool>(
            nameof(CheckPaymentStatusActivity),
            new CheckPaymentStatusActivity.Input(orderId, input.SimulatePaymentSuccess)
        );

        if (paymentCompleted)
        {
            await context.CallActivityAsync(
                nameof(UpdateOrderStatusActivity),
                new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Confirmed)
            );

            return;
        }

        await context.CallActivityAsync(
            nameof(ReleaseTicketActivity),
            new ReleaseTicketActivity.Input(orderId, input.EventId)
        );

        await context.CallActivityAsync<OfferNextWaitlistEntryActivity.Result?>(
            nameof(OfferNextWaitlistEntryActivity),
            new OfferNextWaitlistEntryActivity.Input(input.EventId, _offerDurationInMinutes)
        );

        await context.CallActivityAsync(
            nameof(UpdateOrderStatusActivity),
            new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Failed,
                "Payment was not completed within the allowed time.")
        );
    }
}
