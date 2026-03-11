using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using TicketFlow.Core.Models;
using TicketFlow.Functions.Activities;

namespace TicketFlow.Functions.Orchestrators;

public sealed record PlaceOrderInput(string EventId, bool SimulatePaymentSuccess);

/// <summary>
///     Durable orchestrator that coordinates the single-ticket purchase flow.
///     The Durable instance id is always equal to the <c>orderId</c> stored in Cosmos,
///     so <see cref="TaskOrchestrationContext.InstanceId" /> can be used as the order
///     identifier in every activity call.
/// </summary>
public static class PlaceOrderOrchestrator
{
    [Function(nameof(PlaceOrderOrchestrator))]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context
    )
    {
        var orderId = context.InstanceId;
        var input = context.GetInput<PlaceOrderInput>()
                    ?? throw new InvalidOperationException("Orchestrator input is required.");

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

            await context.CallActivityAsync(
                nameof(UpdateOrderStatusActivity),
                new UpdateOrderStatusActivity.Input(orderId, OrderStatus.Failed, "Payment was declined."));
        }
    }
}
