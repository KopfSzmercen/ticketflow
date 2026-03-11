using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Core.Models;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Activities;

public sealed class UpdateOrderStatusActivity(TicketFlowDbContext dbContext)
{
    [Function(nameof(UpdateOrderStatusActivity))]
    public async Task RunActivity(
        [ActivityTrigger] Input input,
        FunctionContext executionContext
    )
    {
        var order = await dbContext.Orders
            .WithPartitionKey(input.OrderId)
            .FirstOrDefaultAsync(o => o.Id == input.OrderId);

        if (order is null)
            throw new InvalidOperationException($"Order '{input.OrderId}' not found.");

        order.Status = input.NewStatus;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        if (input.FailureReason is not null)
            order.FailureReason = input.FailureReason;

        await dbContext.SaveChangesAsync();
    }

    public sealed record Input(
        string OrderId,
        OrderStatus NewStatus,
        string? FailureReason = null
    );
}
