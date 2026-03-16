using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;

namespace TicketFlow.Infrastructure.ServiceBus;

public sealed class ServiceBusClientFactory(IOptions<ServiceBusOptions> optionsAccessor) : IServiceBusClientFactory
{
    private readonly ServiceBusOptions _options = optionsAccessor.Value;

    public ServiceBusClient CreateClient()
    {
        return _options.AuthMode switch
        {
            ServiceBusAuthMode.Emulator => new ServiceBusClient(_options.ConnectionString!),
            ServiceBusAuthMode.ManagedIdentity => new ServiceBusClient(
                _options.FullyQualifiedNamespace!,
                new ManagedIdentityCredential(new ManagedIdentityCredentialOptions())),
            ServiceBusAuthMode.AzureCli => new ServiceBusClient(
                _options.FullyQualifiedNamespace!,
                new AzureCliCredential()),
            _ => new ServiceBusClient(_options.FullyQualifiedNamespace!, new DefaultAzureCredential())
        };
    }

    public ServiceBusAdministrationClient CreateAdministrationClient()
    {
        return _options.AuthMode switch
        {
            ServiceBusAuthMode.Emulator => new ServiceBusAdministrationClient(
                _options.AdministrationConnectionString ?? _options.ConnectionString!),
            ServiceBusAuthMode.ManagedIdentity => new ServiceBusAdministrationClient(
                _options.FullyQualifiedNamespace!,
                new ManagedIdentityCredential(new ManagedIdentityCredentialOptions())),
            ServiceBusAuthMode.AzureCli => new ServiceBusAdministrationClient(
                _options.FullyQualifiedNamespace!,
                new AzureCliCredential()),
            _ => new ServiceBusAdministrationClient(_options.FullyQualifiedNamespace!, new DefaultAzureCredential())
        };
    }
}
