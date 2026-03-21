using System.IO;

namespace TicketFlow.Infrastructure.BlobStorage;

public interface IFileStorage
{
    Task UploadAsync(
        string containerAlias,
        string blobName,
        Stream content,
        string? contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string containerAlias,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string containerAlias,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteIfExistsAsync(
        string containerAlias,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<Uri> GetReadSasUriAsync(
        string containerAlias,
        string blobName,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task<Uri> GetWriteSasUriAsync(
        string containerAlias,
        string blobName,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
