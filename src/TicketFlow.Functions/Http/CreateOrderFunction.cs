using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Orders;
using TicketFlow.Functions.Orchestrators;
using TicketFlow.Core.Models;

namespace TicketFlow.Functions.Http;

public sealed class CreateOrderFunction(IOrderCreationService orderCreationService)
{
    [Function("CreateOrder")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] [FromBody]
        Request request,
        [DurableClient] DurableTaskClient durableClient
    )
    {
        var order = await orderCreationService.CreateOrderAsync(
            new CreateOrderRequest(
                request.EventId,
                request.AttendeeName,
                request.AttendeeEmail,
                request.TicketPrice,
                request.SimulatePaymentSuccess
            ));

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(PlaceOrderOrchestrator),
            new PlaceOrderInput(
                request.EventId,
                request.SimulatePaymentSuccess,
                request.PayLater,
                request.TicketPrice),
            new StartOrchestrationOptions { InstanceId = order.Id }
        );

        var response = OrderResponse.FromOrder(order);

        return Results.Accepted(
            $"/orders/{order.Id}",
            response
        );
    }

    public sealed record Request(
        string EventId,
        string AttendeeName,
        string AttendeeEmail,
        Money TicketPrice,
        //Used to deterministically simulate payment success or failure in the ProcessPaymentActivity, for demo purposes only
        bool SimulatePaymentSuccess = true,
        bool PayLater = false
    );
}
