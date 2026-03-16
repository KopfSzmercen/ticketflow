using TicketFlow.Core.Models;

namespace TicketFlow.Functions.Activities;

public interface IOrderCompletedEventPublisher
{
    Task PublishAsync(Order order, CancellationToken cancellationToken = default);
}