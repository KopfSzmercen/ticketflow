using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Activities;

/// <summary>
///     Decrements <c>AvailableTickets</c> on the event by 1.
///     Uses EF Core Cosmos optimistic concurrency (<c>_etag</c>) — if another
///     concurrent purchase wins the race a <see cref="DbUpdateConcurrencyException" />
///     is thrown, which Durable will retry according to the host retry policy.
///     Returns <c>true</c> when the ticket was reserved, <c>false</c> when the event
///     has no remaining availability.
/// </summary>
public sealed class ReserveTicketActivity(TicketFlowDbContext dbContext)
{
    [Function(nameof(ReserveTicketActivity))]
    public async Task<bool> RunActivity(
        [ActivityTrigger] Input input,
        FunctionContext executionContext)
    {
        var ticketEvent = await dbContext.Events.FindAsync(input.EventId);

        if (ticketEvent is null)
            throw new InvalidOperationException($"Event '{input.EventId}' not found.");

        if (ticketEvent.AvailableTickets <= 0)
            return false;

        ticketEvent.AvailableTickets--;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public sealed record Input(string OrderId, string EventId);
}
