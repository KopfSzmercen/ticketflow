using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Infrastructure.BlobStorage;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Infrastructure.ServiceBus;
using Xunit;

namespace TicketFlow.Integration.Tests.Fixtures;

public sealed class DurableFunctionsHostFixture : IAsyncLifetime
{
    private const string CosmosEmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDjAz4=";
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

    private readonly IContainer _cosmosContainer =
        new ContainerBuilder("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
            .WithEnvironment("AZURE_COSMOS_EMULATOR_PARTITION_COUNT", "1")
            .WithEnvironment("AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE", "127.0.0.1")
            .WithPortBinding(8081, true)
            .WithPortBinding(10250, true)
            .WithPortBinding(10251, true)
            .WithPortBinding(10252, true)
            .WithPortBinding(10253, true)
            .WithPortBinding(10254, true)
            .WithPortBinding(10255, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(8081)
            )
            .Build();

    private readonly StringBuilder _functionsHostLogs = new();
    private Process? _functionsHostProcess;
    private IHost _host = null!;

    public IServiceProvider Services => _host.Services;

    public HttpClient HttpClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _cosmosContainer.StartAsync();
        await _azuriteContainer.StartAsync();

        var cosmosEndpoint = $"http://localhost:{_cosmosContainer.GetMappedPublicPort(8081)}";
        var connectionString =
            $"AccountEndpoint={cosmosEndpoint};AccountKey={CosmosEmulatorKey};DisableServerCertificateValidation=true";

        _host = new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{CosmosDbOptions.SectionName}:AuthMode"] = nameof(CosmosDbAuthMode.Emulator),
                    [$"{CosmosDbOptions.SectionName}:ConnectionString"] = connectionString,
                    ["ServiceBus"] =
                        "Endpoint=sb://127.0.0.1:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                    ["ServiceBusTriggerConnection"] =
                        "Endpoint=sb://127.0.0.1:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                    [$"{ServiceBusOptions.SectionName}:AuthMode"] = nameof(ServiceBusAuthMode.Emulator),
                    [$"{ServiceBusOptions.SectionName}:ConnectionString"] =
                        "Endpoint=sb://127.0.0.1:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                    [$"{ServiceBusOptions.SectionName}:AdministrationConnectionString"] =
                        "Endpoint=sb://127.0.0.1:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                    [$"{ServiceBusOptions.SectionName}:TopicName"] = "order-events",
                    [$"{ServiceBusOptions.SectionName}:EmailSubscriptionName"] = "email-worker",
                    [$"{ServiceBusOptions.SectionName}:AnalyticsSubscriptionName"] = "analytics-worker",
                    [$"{ServiceBusOptions.SectionName}:QrSubscriptionName"] = "qr-worker"
                });
            })
            .ConfigureServices((_, services) => services.AddCosmosDbModule())
            .Build();

        await _host.EnsureCosmosDbInitializedAsync();

        var httpPort = GetRandomPort();
        await StartFunctionsHostAsync(httpPort, connectionString);

        HttpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{httpPort}")
        };

        await WaitForHealthAsync();
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();

        if (_functionsHostProcess is { HasExited: false })
        {
            _functionsHostProcess.Kill(true);
            await _functionsHostProcess.WaitForExitAsync();
            _functionsHostProcess.Dispose();
        }

        _host?.Dispose();
        await _azuriteContainer.DisposeAsync();
        await _cosmosContainer.DisposeAsync();
    }

    public async Task ClearDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var allTicketEvents = await dbContext.Events.ToListAsync();
        dbContext.Events.RemoveRange(allTicketEvents);

        var allOrders = await dbContext.Orders.ToListAsync();
        dbContext.Orders.RemoveRange(allOrders);

        var allWaitlistEntries = await dbContext.WaitlistEntries.ToListAsync();
        dbContext.WaitlistEntries.RemoveRange(allWaitlistEntries);

        await dbContext.SaveChangesAsync();
    }

    private async Task StartFunctionsHostAsync(int httpPort, string cosmosConnectionString)
    {
        var repositoryRoot = GetRepositoryRoot();
        var azuriteBlobPort = _azuriteContainer.GetMappedPublicPort(10000);
        var azuriteQueuePort = _azuriteContainer.GetMappedPublicPort(10001);
        var azuriteTablePort = _azuriteContainer.GetMappedPublicPort(10002);
        var dotnetRoot = ResolveDotnetRootForFunctionsHost(repositoryRoot);

        var azuriteConnectionString =
            $"DefaultEndpointsProtocol=http;" +
            $"AccountName={AzuriteAccountName};" +
            $"AccountKey={AzuriteAccountKey};" +
            $"BlobEndpoint=http://127.0.0.1:{azuriteBlobPort}/{AzuriteAccountName};" +
            $"QueueEndpoint=http://127.0.0.1:{azuriteQueuePort}/{AzuriteAccountName};" +
            $"TableEndpoint=http://127.0.0.1:{azuriteTablePort}/{AzuriteAccountName};";

        var functionsProjectPath = Path.Combine(repositoryRoot, "src", "TicketFlow.Functions");

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "func",
            Arguments = $"host start --port {httpPort}",
            WorkingDirectory = functionsProjectPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment =
            {
                ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated",
                ["AzureWebJobsStorage"] = azuriteConnectionString,
                ["CosmosDb__AuthMode"] = nameof(CosmosDbAuthMode.Emulator),
                ["CosmosDb__ConnectionString"] = cosmosConnectionString,
                ["TicketStorage__AuthMode"] = nameof(TicketStorageAuthMode.Emulator),
                ["TicketStorage__ConnectionString"] = azuriteConnectionString,
                ["TicketStorage__Containers__tickets"] = "tickets",
                ["AzureFunctionsJobHost__extensions__durableTask__hubName"] = $"tf{Guid.NewGuid().ToString("N")[..20]}",
                ["ServiceBus"] =
                    "Endpoint=sb://127.0.0.1:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                ["ServiceBusTriggerConnection"] =
                    "Endpoint=sb://127.0.0.1:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                [$"{ServiceBusOptions.SectionName}:AuthMode"] = nameof(ServiceBusAuthMode.Emulator),
                [$"{ServiceBusOptions.SectionName}:ConnectionString"] =
                    "Endpoint=sb://127.0.0.1:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                [$"{ServiceBusOptions.SectionName}:AdministrationConnectionString"] =
                    "Endpoint=sb://127.0.0.1:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                [$"{ServiceBusOptions.SectionName}:TopicName"] = "order-events",
                [$"{ServiceBusOptions.SectionName}:EmailSubscriptionName"] = "email-worker",
                [$"{ServiceBusOptions.SectionName}:AnalyticsSubscriptionName"] = "analytics-worker",
                [$"{ServiceBusOptions.SectionName}:QrSubscriptionName"] = "qr-worker"
            }
        };

        if (dotnetRoot is not null)
        {
            var existingPath = processStartInfo.Environment.TryGetValue("PATH", out var currentPath)
                ? currentPath
                : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            processStartInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
            processStartInfo.Environment["PATH"] = $"{dotnetRoot}:{existingPath}";
        }

        _functionsHostProcess = Process.Start(processStartInfo);

        if (_functionsHostProcess is null)
            throw new InvalidOperationException("Failed to start Azure Functions host process.");

        _functionsHostProcess.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                _functionsHostLogs.AppendLine(eventArgs.Data);
        };

        _functionsHostProcess.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                _functionsHostLogs.AppendLine(eventArgs.Data);
        };

        _functionsHostProcess.BeginOutputReadLine();
        _functionsHostProcess.BeginErrorReadLine();

        await Task.Delay(TimeSpan.FromSeconds(1));

        if (_functionsHostProcess.HasExited)
            throw new InvalidOperationException(
                $"Azure Functions host exited early. Logs:{Environment.NewLine}{_functionsHostLogs}");
    }

    private async Task WaitForHealthAsync()
    {
        using var healthClient = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            if (_functionsHostProcess is { HasExited: true })
                throw new InvalidOperationException(
                    $"Azure Functions host exited before becoming healthy. Logs:{Environment.NewLine}{_functionsHostLogs}");

            try
            {
                var response = await healthClient.GetAsync(new Uri(HttpClient.BaseAddress!, "/api/health"));

                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadAsStringAsync();
                    if (payload.Contains("Healthy", StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }
            catch
            {
                // Host may not be listening yet.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Timed out waiting for Azure Functions host readiness. Logs:{Environment.NewLine}{_functionsHostLogs}");
    }

    private static int GetRandomPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TicketFlow.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find TicketFlow.sln from test execution directory.");
    }

    private static string? ResolveDotnetRootForFunctionsHost(string repositoryRoot)
    {
        var requestedSdkVersion = TryReadRequestedSdkVersion(repositoryRoot);
        if (requestedSdkVersion is null)
            return null;

        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
            "/usr/share/dotnet",
            "/usr/lib/dotnet"
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var sdkPath = Path.Combine(candidate, "sdk", requestedSdkVersion);
            if (Directory.Exists(sdkPath))
                return candidate;
        }

        return null;
    }

    private static string? TryReadRequestedSdkVersion(string repositoryRoot)
    {
        try
        {
            var globalJsonPath = Path.Combine(repositoryRoot, "global.json");
            if (!File.Exists(globalJsonPath))
                return null;

            using var document = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
            if (!document.RootElement.TryGetProperty("sdk", out var sdkElement))
                return null;

            if (!sdkElement.TryGetProperty("version", out var versionElement))
                return null;

            return versionElement.GetString();
        }
        catch
        {
            return null;
        }
    }
}
