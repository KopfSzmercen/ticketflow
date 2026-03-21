using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace TicketFlow.Infrastructure.BlobStorage;

public sealed class AzureBlobFileStorage(
    BlobServiceClient blobServiceClient,
    IBlobContainerNameResolver containerNameResolver) : IFileStorage
{
    private static readonly TimeSpan SasClockSkew = TimeSpan.FromMinutes(5);

    public async Task UploadAsync(
        string containerAlias,
        string blobName,
        Stream content,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentNullException.ThrowIfNull(content);

        var blobClient = GetBlobClient(containerAlias, blobName);

        var uploadOptions = new BlobUploadOptions();

        if (!string.IsNullOrWhiteSpace(contentType))
            uploadOptions.HttpHeaders = new BlobHttpHeaders { ContentType = contentType };

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(
        string containerAlias,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var blobClient = GetBlobClient(containerAlias, blobName);
        return blobClient.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteIfExistsAsync(
        string containerAlias,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var blobClient = GetBlobClient(containerAlias, blobName);
        return await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public Task<Uri> GetReadSasUriAsync(
        string containerAlias,
        string blobName,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var sasBuilder = CreateBlobSasBuilder(containerAlias, blobName, ttl);
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return BuildSasUriAsync(containerAlias, blobName, sasBuilder, cancellationToken);
    }

    public Task<Uri> GetWriteSasUriAsync(
        string containerAlias,
        string blobName,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var sasBuilder = CreateBlobSasBuilder(containerAlias, blobName, ttl);
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        return BuildSasUriAsync(containerAlias, blobName, sasBuilder, cancellationToken);
    }

    private async Task<Uri> BuildSasUriAsync(
        string containerAlias,
        string blobName,
        BlobSasBuilder sasBuilder,
        CancellationToken cancellationToken)
    {
        var blobClient = GetBlobClient(containerAlias, blobName);

        if (blobClient.CanGenerateSasUri)
            return blobClient.GenerateSasUri(sasBuilder);

        var delegationKey = await blobServiceClient.GetUserDelegationKeyAsync(
            sasBuilder.StartsOn,
            sasBuilder.ExpiresOn,
            cancellationToken);

        var queryParameters = sasBuilder.ToSasQueryParameters(delegationKey.Value, blobServiceClient.AccountName);

        var uriBuilder = new BlobUriBuilder(blobClient.Uri)
        {
            Sas = queryParameters
        };

        return uriBuilder.ToUri();
    }

    private BlobSasBuilder CreateBlobSasBuilder(string containerAlias, string blobName, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "SAS token TTL must be greater than zero.");

        var blobClient = GetBlobClient(containerAlias, blobName);

        var now = DateTimeOffset.UtcNow;
        var startsOn = now.Subtract(SasClockSkew);
        var expiresOn = now.Add(ttl);

        return new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            StartsOn = startsOn,
            ExpiresOn = expiresOn
        };
    }

    private BlobClient GetBlobClient(string containerAlias, string blobName)
    {
        var containerName = containerNameResolver.Resolve(containerAlias);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        return containerClient.GetBlobClient(blobName);
    }
}
