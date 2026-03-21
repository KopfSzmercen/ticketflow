using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Qr;
using TicketFlow.Infrastructure.BlobStorage;

namespace TicketFlow.Functions.Triggers;

public sealed class QrWorkerTrigger(
    ILogger<QrWorkerTrigger> logger,
    IQrCodeGenerator qrCodeGenerator,
    IFileStorage fileStorage)
{
    [Function(nameof(QrWorkerTrigger))]
    public async Task Run(
        [ServiceBusTrigger(
            "%ServiceBus:TopicName%",
            "%ServiceBus:QrSubscriptionName%",
            Connection = "ServiceBusTriggerConnection"
        )]
        ServiceBusReceivedMessage message,
        FunctionContext context,
        CancellationToken cancellationToken = default)
    {
        var bodyRaw = message.Body.ToString();

        if (string.IsNullOrWhiteSpace(bodyRaw))
        {
            logger.LogWarning("Received empty message body for message ID: {Id}", message.MessageId);
            return;
        }

        OrderCompletedEvent? orderEvent;

        try
        {
            orderEvent = JsonSerializer.Deserialize<OrderCompletedEvent>(bodyRaw);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid QR worker payload for message ID: {Id}", message.MessageId);
            return;
        }

        if (!IsValid(orderEvent))
        {
            logger.LogWarning("QR worker payload missing required fields for message ID: {Id}", message.MessageId);
            return;
        }

        await ProcessOrderCompletedEventAsync(orderEvent!, cancellationToken);
    }

    public async Task ProcessOrderCompletedEventAsync(OrderCompletedEvent orderEvent, CancellationToken cancellationToken = default)
    {
        var qrPayload = BuildPayload(orderEvent);
        var pngBytes = qrCodeGenerator.GeneratePng(qrPayload);
        await using var contentStream = new MemoryStream(pngBytes);

        var blobPath = BuildBlobPath(orderEvent.OrderId);
        await fileStorage.UploadAsync(
            BlobContainerAliases.Tickets,
            blobPath,
            contentStream,
            "image/png",
            cancellationToken);

        logger.LogInformation("Generated QR ticket blob at {BlobPath} for order {OrderId}", blobPath, orderEvent.OrderId);
    }

    public static string BuildPayload(OrderCompletedEvent orderEvent)
    {
        return $"{orderEvent.OrderId}|{orderEvent.EventName}|{orderEvent.CustomerEmail}";
    }

    public static string BuildBlobPath(string orderId)
    {
        return $"tickets/{orderId}.png";
    }

    private static bool IsValid(OrderCompletedEvent? orderEvent)
    {
        return orderEvent is not null
               && !string.IsNullOrWhiteSpace(orderEvent.OrderId)
               && !string.IsNullOrWhiteSpace(orderEvent.EventName)
               && !string.IsNullOrWhiteSpace(orderEvent.CustomerEmail);
    }
}