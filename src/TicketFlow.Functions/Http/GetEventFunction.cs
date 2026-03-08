using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Functions.DTO;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class GetEventFunction(TicketFlowDbContext dbContext)
{
    [Function("GetEvent")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}")]
        HttpRequest req,
        string eventId
    )
    {
        var ticketEvent = await dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId);

        if (ticketEvent is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(TicketEventResponse.FromTicketEvent(ticketEvent));
    }
}
