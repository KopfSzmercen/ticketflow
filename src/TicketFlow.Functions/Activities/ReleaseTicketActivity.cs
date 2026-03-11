using Microsoft.Azure.Functions.Worker;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Activities;

public sealed class ReleaseTicketActivity(TicketFlowDbContext dbContext)
{
    [Function(nameof(ReleaseTicketActivity))]
    public async Task RunActivity(
        [ActivityTrigger] Input input,
        FunctionContext executionContext)
    {
        var ticketEvent = await dbContext.Events.FindAsync(input.EventId);

        if (ticketEvent is null)
            throw new InvalidOperationException($"Event '{input.EventId}' not found during ticket release.");

        ticketEvent.AvailableTickets++;
        await dbContext.SaveChangesAsync();
    }

    public sealed record Input(string OrderId, string EventId);
}
