using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Orchestrators;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class CreateOrderFunction(TicketFlowDbContext dbContext)
{
    [Function("CreateOrder")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")]
        Request request,
        [DurableClient] DurableTaskClient durableClient
    )
    {
        var orderId = Guid.NewGuid().ToString();

        var order = new Order
        {
            Id = orderId,
            EventId = request.EventId,
            AttendeeName = request.AttendeeName,
            AttendeeEmail = request.AttendeeEmail,
            TicketPrice = request.TicketPrice,
            SimulatePaymentSuccess = request.SimulatePaymentSuccess,
            Status = OrderStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(PlaceOrderOrchestrator),
            new PlaceOrderInput(request.EventId, request.SimulatePaymentSuccess),
            new StartOrchestrationOptions { InstanceId = orderId }
        );

        var response = OrderResponse.FromOrder(order);

        return Results.Accepted(
            $"/orders/{orderId}",
            response
        );
    }

    public sealed record Request(
        string EventId,
        string AttendeeName,
        string AttendeeEmail,
        Money TicketPrice,
        //Used to deterministically simulate payment success or failure in the ProcessPaymentActivity, for demo purposes only
        bool SimulatePaymentSuccess = true
    );
}
