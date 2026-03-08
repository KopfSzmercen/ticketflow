namespace TicketFlow.Infrastructure.CosmosDb;

public enum CosmosDbAuthMode
{
    DefaultAzureCredential,
    ManagedIdentity,
    AzureCli,
    Emulator
}
