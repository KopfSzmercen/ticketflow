using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Infrastructure.ServiceBus;

namespace TicketFlow.Functions.Activities;

public sealed class ServiceBusOrderCompletedEventPublisher(
    IServiceBusClientFactory sbClientFactory,
    IOptions<ServiceBusOptions> sbOptions
) : IOrderCompletedEventPublisher
{
    public async Task PublishAsync(Order order, CancellationToken cancellationToken = default)
    {
        var orderEvent = new OrderCompletedEvent(
            order.Id,
            order.AttendeeEmail,
            order.EventId,
            1,
            order.TicketPrice.Amount,
            order.TicketPrice.Currency
        );

        var messageJson = JsonSerializer.Serialize(orderEvent);
        var message = new ServiceBusMessage(messageJson);

        await using var client = sbClientFactory.CreateClient();
        await using var sender = client.CreateSender(sbOptions.Value.TopicName);
        await sender.SendMessageAsync(message, cancellationToken);
    }
}
