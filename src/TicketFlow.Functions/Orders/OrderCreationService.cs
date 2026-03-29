using Microsoft.EntityFrameworkCore;
using TicketFlow.Core.Models;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Orders;

public interface IOrderCreationService
{
    Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
}

public sealed record CreateOrderRequest(
    string EventId,
    string AttendeeName,
    string AttendeeEmail,
    Money TicketPrice,
    bool SimulatePaymentSuccess,
    string? WaitlistOfferInstanceId = null
);

public sealed class OrderCreationService(TicketFlowDbContext dbContext) : IOrderCreationService
{
    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.WaitlistOfferInstanceId))
        {
            var existingOrder = await dbContext.Orders
                .FirstOrDefaultAsync(
                    order => order.WaitlistOfferInstanceId == request.WaitlistOfferInstanceId,
                    cancellationToken);

            if (existingOrder is not null)
                return existingOrder;
        }

        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            EventId = request.EventId,
            AttendeeName = request.AttendeeName,
            AttendeeEmail = request.AttendeeEmail,
            TicketPrice = request.TicketPrice,
            SimulatePaymentSuccess = request.SimulatePaymentSuccess,
            WaitlistOfferInstanceId = request.WaitlistOfferInstanceId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Orders.AddAsync(order, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return order;
    }
}