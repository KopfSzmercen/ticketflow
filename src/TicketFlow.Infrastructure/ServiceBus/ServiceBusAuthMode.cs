namespace TicketFlow.Infrastructure.ServiceBus;

public enum ServiceBusAuthMode
{
    DefaultAzureCredential,
    ManagedIdentity,
    AzureCli,
    Emulator
}
