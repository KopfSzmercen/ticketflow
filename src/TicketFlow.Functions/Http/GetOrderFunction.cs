using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Functions.DTO;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class GetOrderFunction(TicketFlowDbContext dbContext)
{
    [Function("GetOrder")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{orderId}")]
        HttpRequest httpRequest,
        string orderId,
        [DurableClient] DurableTaskClient durableClient
    )
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .WithPartitionKey(orderId)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
            return Results.NotFound();

        var orchestrationMetadata = await TryGetSupplementalOrchestrationMetadataAsync(durableClient, orderId);

        return Results.Ok(OrderResponse.FromOrder(order, orchestrationMetadata));
    }

    private static Task<OrchestrationMetadata?> TryGetSupplementalOrchestrationMetadataAsync(
        DurableTaskClient durableClient,
        string instanceId
    )
    {
        try
        {
            return durableClient.GetInstanceAsync(instanceId);
        }
        catch
        {
            // Orchestration metadata is supplemental; never fail the business read.
            return Task.FromResult<OrchestrationMetadata?>(null);
        }
    }
}
