using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace TicketFlow.Infrastructure.ServiceBus;

public interface IServiceBusClientFactory
{
    ServiceBusClient CreateClient();

    ServiceBusAdministrationClient CreateAdministrationClient();
}
