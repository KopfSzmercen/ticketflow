using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Functions.DTO;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class BrowseOrdersFunction(TicketFlowDbContext dbContext)
{
    [Function("BrowseOrders")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")]
        HttpRequest req
    )
    {
        var orders = await dbContext.Orders.ToListAsync();
        var response = orders.Select(order => OrderResponse.FromOrder(order));
        return Results.Ok(response);
    }
}
