using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TicketFlow.Infrastructure.CosmosDb;
using Xunit;

namespace TicketFlow.Integration.Tests.Fixtures;

public sealed class CosmosDbContainerFixture : IAsyncLifetime
{
    private readonly IContainer _container =
        new ContainerBuilder("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
            .WithEnvironment("AZURE_COSMOS_EMULATOR_PARTITION_COUNT", "1")
            .WithEnvironment("AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE", "127.0.0.1")
            .WithPortBinding(8081, 8081)
            .WithPortBinding(10250, 10250)
            .WithPortBinding(10251, 10251)
            .WithPortBinding(10252, 10252)
            .WithPortBinding(10253, 10253)
            .WithPortBinding(10254, 10254)
            .WithPortBinding(10255, 10255)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(8081)
            )
            .Build();


    private IHost _host = null!;

    public IServiceProvider Services => _host.Services;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var endpoint = $"http://localhost:{_container.GetMappedPublicPort(8081)}";
        // This is the well-known Azure Cosmos DB emulator key, safe to use for local testing only.
        const string key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDjAz4=";

        _host = new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{CosmosDbOptions.SectionName}:AuthMode"] = nameof(CosmosDbAuthMode.Emulator),
                    [$"{CosmosDbOptions.SectionName}:ConnectionString"] =
                        $"AccountEndpoint={endpoint};AccountKey={key};DisableServerCertificateValidation=true"
                });
            })
            .ConfigureServices((_, services) => { services.AddCosmosDbModule(); })
            .Build();

        await _host.EnsureCosmosDbInitializedAsync();
    }

    public async Task DisposeAsync()
    {
        _host.Dispose();
        await _container.DisposeAsync();
    }
}
