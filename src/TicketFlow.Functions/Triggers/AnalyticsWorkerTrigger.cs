using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TicketFlow.Functions.DTO;

namespace TicketFlow.Functions.Triggers;

public sealed class AnalyticsWorkerTrigger(ILogger<AnalyticsWorkerTrigger> logger)
{
    [Function(nameof(AnalyticsWorkerTrigger))]
    public void Run(
        [ServiceBusTrigger(
            "%ServiceBus:TopicName%",
            "%ServiceBus:AnalyticsSubscriptionName%",
            Connection = "ServiceBusTriggerConnection"
        )]
        string messageBody,
        FunctionContext context
    )
    {
        try
        {
            var orderEvent = JsonSerializer.Deserialize<OrderCompletedEvent>(messageBody);

            if (orderEvent is null)
            {
                logger.LogWarning("Received empty message body for AnalyticsWorkerTrigger.");
                return;
            }

            logger.LogInformation(
                "Simulating analytics processing for order {OrderId} for event {EventName}.",
                orderEvent.OrderId,
                orderEvent.EventName
            );
        }

        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize Service Bus message for AnalyticsWorkerTrigger.");
        }
    }
}
