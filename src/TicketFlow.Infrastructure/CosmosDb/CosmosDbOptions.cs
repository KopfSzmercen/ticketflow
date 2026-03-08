namespace TicketFlow.Infrastructure.CosmosDb;

public sealed class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public CosmosDbAuthMode AuthMode { get; init; } = CosmosDbAuthMode.DefaultAzureCredential;

    /// <summary>Required when <see cref="AuthMode"/> is <see cref="CosmosDbAuthMode.Emulator"/>.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Required when <see cref="AuthMode"/> is anything other than <see cref="CosmosDbAuthMode.Emulator"/>.</summary>
    public string? AccountEndpoint { get; init; }
}
