namespace TicketFlow.Infrastructure.BlobStorage;

public enum TicketStorageAuthMode
{
    Emulator,
    ManagedIdentity,
    AzureCli,
    DefaultAzureCredential
}