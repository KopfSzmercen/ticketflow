using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Triggers;
using TicketFlow.Infrastructure.BlobStorage;

namespace TicketFlow.Functions.Http;

public sealed class GetOrderTicketFunction(IFileStorage fileStorage)
{
    private static readonly TimeSpan ReadSasTtl = TimeSpan.FromMinutes(15);

    [Function("GetOrderTicket")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{orderId}/ticket")]
        HttpRequest httpRequest,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var blobPath = QrWorkerTrigger.BuildBlobPath(orderId);

        try
        {
            await using var contentStream = await fileStorage.OpenReadAsync(
                BlobContainerAliases.Tickets,
                blobPath,
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
        {
            return Results.NotFound();
        }

        var readSasUri = await fileStorage.GetReadSasUriAsync(
            BlobContainerAliases.Tickets,
            blobPath,
            ReadSasTtl,
            cancellationToken);

        return Results.Ok(new TicketUrlResponse(orderId, readSasUri, (int)ReadSasTtl.TotalSeconds));
    }
}