using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Infrastructure.BlobStorage;
using TicketFlow.Infrastructure.CosmosDb;
using Xunit;

namespace TicketFlow.Integration.Tests.Fixtures;

public sealed class CosmosDbContainerFixture : IAsyncLifetime
{
    private const string AzuriteAccountName = "devstoreaccount1";

    private const string AzuriteAccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private readonly IContainer _azuriteContainer =
        new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithCommand(
                "azurite",
                "--skipApiVersionCheck",
                "--blobHost", "0.0.0.0",
                "--queueHost", "0.0.0.0",
                "--tableHost", "0.0.0.0")
            .WithPortBinding(10000, true)
            .WithPortBinding(10001, true)
            .WithPortBinding(10002, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(10000))
            .Build();

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
        await _azuriteContainer.StartAsync();
        await _container.StartAsync();

        var endpoint = $"http://localhost:{_container.GetMappedPublicPort(8081)}";
        var azuriteConnectionString = BuildAzuriteConnectionString();
        // This is the well-known Azure Cosmos DB emulator key, safe to use for local testing only.
        const string key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDjAz4=";

        _host = new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{CosmosDbOptions.SectionName}:AuthMode"] = nameof(CosmosDbAuthMode.Emulator),
                    [$"{CosmosDbOptions.SectionName}:ConnectionString"] =
                        $"AccountEndpoint={endpoint};AccountKey={key};DisableServerCertificateValidation=true",
                    [$"{TicketStorageOptions.SectionName}:AuthMode"] = nameof(TicketStorageAuthMode.Emulator),
                    [$"{TicketStorageOptions.SectionName}:ConnectionString"] = azuriteConnectionString,
                    [$"{TicketStorageOptions.SectionName}:Containers:{BlobContainerAliases.Tickets}"] = "tickets"
                });
            })
            .ConfigureServices((_, services) =>
            {
                services.AddCosmosDbModule();
                services.AddBlobStorageModule();
            })
            .Build();

        await _host.EnsureCosmosDbInitializedAsync();
        await EnsureTicketsContainerExistsAsync();
    }

    public async Task DisposeAsync()
    {
        _host.Dispose();
        await _azuriteContainer.DisposeAsync();
        await _container.DisposeAsync();
    }

    public async Task ClearTicketsContainerAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var blobServiceClient = scope.ServiceProvider.GetRequiredService<BlobServiceClient>();
        var resolver = scope.ServiceProvider.GetRequiredService<IBlobContainerNameResolver>();
        var containerName = resolver.Resolve(BlobContainerAliases.Tickets);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        await containerClient.DeleteIfExistsAsync();
        await containerClient.CreateIfNotExistsAsync();
    }

    public async Task ClearDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await _host.EnsureCosmosDbInitializedAsync();
    }

    private async Task EnsureTicketsContainerExistsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var blobServiceClient = scope.ServiceProvider.GetRequiredService<BlobServiceClient>();
        var resolver = scope.ServiceProvider.GetRequiredService<IBlobContainerNameResolver>();
        var containerName = resolver.Resolve(BlobContainerAliases.Tickets);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();
    }

    private string BuildAzuriteConnectionString()
    {
        var blobPort = _azuriteContainer.GetMappedPublicPort(10000);
        var queuePort = _azuriteContainer.GetMappedPublicPort(10001);
        var tablePort = _azuriteContainer.GetMappedPublicPort(10002);

        return
            $"DefaultEndpointsProtocol=http;" +
            $"AccountName={AzuriteAccountName};" +
            $"AccountKey={AzuriteAccountKey};" +
            $"BlobEndpoint=http://127.0.0.1:{blobPort}/{AzuriteAccountName};" +
            $"QueueEndpoint=http://127.0.0.1:{queuePort}/{AzuriteAccountName};" +
            $"TableEndpoint=http://127.0.0.1:{tablePort}/{AzuriteAccountName};";
    }
}
