using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TicketFlow.Functions.DTO;

namespace TicketFlow.Functions.Triggers;

public sealed class EmailWorkerTrigger(ILogger<EmailWorkerTrigger> logger)
{
    [Function(nameof(EmailWorkerTrigger))]
    public void Run(
        [ServiceBusTrigger(
            "%ServiceBus:TopicName%",
            "%ServiceBus:EmailSubscriptionName%",
            Connection = "ServiceBusTriggerConnection"
        )]
        ServiceBusReceivedMessage message,
        FunctionContext context
    )
    {
        try
        {
            var bodyRaw = message.Body.ToString();
            var orderEvent = JsonSerializer.Deserialize<OrderCompletedEvent>(bodyRaw);

            if (orderEvent is null)
            {
                logger.LogWarning("Received empty message body for message ID: {Id}", message.MessageId);
                return;
            }

            logger.LogInformation(
                "Simulating email sent for order {OrderId} to client {Email} for event {EventName}.",
                orderEvent.OrderId,
                orderEvent.CustomerEmail,
                orderEvent.EventName
            );
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize Service Bus message for message ID: {Id}", message.MessageId);
        }
    }
}
