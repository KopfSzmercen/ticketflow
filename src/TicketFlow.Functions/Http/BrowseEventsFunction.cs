using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Functions.DTO;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class BrowseEventsFunction(TicketFlowDbContext dbContext)
{
    [Function("BrowseEvents")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")]
        HttpRequest req
    )
    {
        var events = await dbContext.Events.ToListAsync();
        var response = events.Select(TicketEventResponse.FromTicketEvent);
        return Results.Ok(response);
    }
}
