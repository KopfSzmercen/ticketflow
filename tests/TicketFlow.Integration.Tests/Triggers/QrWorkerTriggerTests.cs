using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Qr;
using TicketFlow.Functions.Triggers;
using TicketFlow.Infrastructure.BlobStorage;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Triggers;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class QrWorkerTriggerTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Run_ShouldWritePngQrBlob()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<QrWorkerTrigger>>();
        var qrCodeGenerator = new QrCodeGenerator();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var blobServiceClient = scope.ServiceProvider.GetRequiredService<BlobServiceClient>();
        var resolver = scope.ServiceProvider.GetRequiredService<IBlobContainerNameResolver>();
        var trigger = new QrWorkerTrigger(logger, qrCodeGenerator, fileStorage);

        var orderEvent = new OrderCompletedEvent(
            Guid.NewGuid().ToString("N"),
            "qr-customer@ticketflow.dev",
            "event-qr-123",
            1,
            149.00m,
            "USD");

        var messageBody = JsonSerializer.Serialize(orderEvent);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(messageBody),
            messageId: Guid.NewGuid().ToString("N"));

        // Act
        await trigger.Run(message, null!);

        // Assert
        var blobPath = QrWorkerTrigger.BuildBlobPath(orderEvent.OrderId);

        await using var readStream = await fileStorage.OpenReadAsync(BlobContainerAliases.Tickets, blobPath);
        await using var memory = new MemoryStream();
        await readStream.CopyToAsync(memory);
        var bytes = memory.ToArray();

        bytes.Length.ShouldBeGreaterThan(8);
        bytes[0].ShouldBe((byte)0x89);
        bytes[1].ShouldBe((byte)0x50);
        bytes[2].ShouldBe((byte)0x4E);
        bytes[3].ShouldBe((byte)0x47);
        bytes[4].ShouldBe((byte)0x0D);
        bytes[5].ShouldBe((byte)0x0A);
        bytes[6].ShouldBe((byte)0x1A);
        bytes[7].ShouldBe((byte)0x0A);

        var containerName = resolver.Resolve(BlobContainerAliases.Tickets);
        var blobClient = blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobPath);
        var properties = await blobClient.GetPropertiesAsync();

        properties.Value.ContentType.ShouldBe("image/png");
    }
}