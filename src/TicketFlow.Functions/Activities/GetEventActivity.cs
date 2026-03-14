using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Activities;

public sealed class GetEventActivity(TicketFlowDbContext dbContext)
{
    [Function(nameof(GetEventActivity))]
    public async Task<Result?> RunActivity(
        [ActivityTrigger] string eventId,
        FunctionContext executionContext
    )
    {
        var ticketEvent = await dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId);

        return ticketEvent is null
            ? null
            : new Result(
                ticketEvent.Id,
                ticketEvent.Name,
                ticketEvent.TotalCapacity,
                ticketEvent.AvailableTickets,
                ticketEvent.ReservationExpirationInSeconds
            );
    }

    public sealed record Result(
        string Id,
        string Name,
        int TotalTickets,
        int AvailableTickets,
        int ReservationExpirationInSeconds
    );
}
