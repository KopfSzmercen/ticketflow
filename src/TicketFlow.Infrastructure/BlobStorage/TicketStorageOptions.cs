namespace TicketFlow.Infrastructure.BlobStorage;

public sealed class TicketStorageOptions
{
    public const string SectionName = "TicketStorage";

    public TicketStorageAuthMode AuthMode { get; init; } = TicketStorageAuthMode.DefaultAzureCredential;

    /// <summary>Required when <see cref="AuthMode" /> is <see cref="TicketStorageAuthMode.Emulator" />.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Required when <see cref="AuthMode" /> is not <see cref="TicketStorageAuthMode.Emulator" />.</summary>
    public string? AccountName { get; init; }

    /// <summary>
    /// Optional map of logical container aliases to concrete container names.
    /// Example: TicketStorage:Containers:tickets = tickets
    /// </summary>
    public Dictionary<string, string> Containers { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [BlobContainerAliases.Tickets] = "tickets"
    };
}
