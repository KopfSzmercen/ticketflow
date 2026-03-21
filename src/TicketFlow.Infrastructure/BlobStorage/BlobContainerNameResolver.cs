using Microsoft.Extensions.Options;

namespace TicketFlow.Infrastructure.BlobStorage;

public sealed class BlobContainerNameResolver(IOptions<TicketStorageOptions> optionsAccessor) : IBlobContainerNameResolver
{
    private readonly Dictionary<string, string> _containerNames = new(optionsAccessor.Value.Containers, StringComparer.OrdinalIgnoreCase);

    public string Resolve(string containerAlias)
    {
        if (string.IsNullOrWhiteSpace(containerAlias))
            throw new ArgumentException("Container alias must be provided.", nameof(containerAlias));

        if (_containerNames.TryGetValue(containerAlias, out var containerName))
            return containerName;

        throw new KeyNotFoundException($"Container alias '{containerAlias}' is not configured.");
    }
}
