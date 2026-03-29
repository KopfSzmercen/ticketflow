using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Core.Models;
using TicketFlow.Functions.Orders;
using TicketFlow.Functions.Orchestrators;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Activities;

public sealed class CreateOrderFromWaitlistOfferActivity(
    TicketFlowDbContext dbContext,
    IOrderCreationService orderCreationService)
{
    [Function(nameof(CreateOrderFromWaitlistOfferActivity))]
    public async Task<Result> RunActivity(
        [ActivityTrigger] Input input,
        [DurableClient] DurableTaskClient durableTaskClient,
        FunctionContext executionContext)
    {
        var waitlistEntry = await dbContext.WaitlistEntries
            .WithPartitionKey(input.EventId)
            .FirstOrDefaultAsync(w => w.Id == input.WaitlistEntryId);

        if (waitlistEntry is null)
        {
            throw new InvalidOperationException(
                $"Waitlist entry '{input.WaitlistEntryId}' was not found for event '{input.EventId}'.");
        }

        if (waitlistEntry.Status != WaitlistStatus.Claimed)
        {
            throw new InvalidOperationException(
                $"Waitlist entry '{input.WaitlistEntryId}' is in '{waitlistEntry.Status}' status and cannot be converted into an order.");
        }

        if (string.IsNullOrWhiteSpace(waitlistEntry.AttendeeName))
        {
            throw new InvalidOperationException(
                $"Waitlist entry '{input.WaitlistEntryId}' is missing attendee name.");
        }

        var ticketPrice = await ResolveTicketPriceAsync(waitlistEntry, input.EventId);

        var order = await orderCreationService.CreateOrderAsync(
            new CreateOrderRequest(
                input.EventId,
                waitlistEntry.AttendeeName,
                waitlistEntry.AttendeeContact,
                ticketPrice,
                input.SimulatePaymentSuccess,
                input.OfferInstanceId
            ));

        var orchestration = await durableTaskClient.GetInstanceAsync(order.Id, false);
        if (orchestration is null)
        {
            await durableTaskClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(PlaceOrderOrchestrator),
                new PlaceOrderInput(
                    input.EventId,
                    input.SimulatePaymentSuccess,
                    input.PayLater,
                    ticketPrice),
                new StartOrchestrationOptions { InstanceId = order.Id }
            );
        }

        return new Result(order.Id);
    }

    private async Task<Money> ResolveTicketPriceAsync(WaitlistEntry waitlistEntry, string eventId)
    {
        if (waitlistEntry.OfferedTicketPrice is not null)
            return waitlistEntry.OfferedTicketPrice;

        var ticketEvent = await dbContext.Events
            .AsNoTracking()
            .WithPartitionKey(eventId)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (ticketEvent is null)
        {
            throw new InvalidOperationException(
                $"Event '{eventId}' was not found while creating order from waitlist offer.");
        }

        return new Money(ticketEvent.TicketPrice.Amount, ticketEvent.TicketPrice.Currency);
    }

    public sealed record Input(
        string EventId,
        string WaitlistEntryId,
        string OfferInstanceId,
        bool SimulatePaymentSuccess = true,
        bool PayLater = false);

    public sealed record Result(string OrderId);
}