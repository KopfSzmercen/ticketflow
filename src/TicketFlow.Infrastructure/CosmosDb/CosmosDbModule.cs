using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TicketFlow.Infrastructure.CosmosDb;

public static class CosmosDbModule
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCosmosDbModule(IConfiguration config)
        {
            var authMode = config["CosmosDb:AuthMode"] ?? "DefaultAzureCredential";

            services.AddDbContext<TicketFlowDbContext>((_, options) =>
            {
                switch (authMode)
                {
                    case "Emulator":
                        var connectionString = config["CosmosDb:ConnectionString"]
                                               ?? throw new InvalidOperationException(
                                                   "CosmosDb:ConnectionString is required for Emulator auth mode.");


                        options.UseCosmos(connectionString, "ticketflow", opt =>
                        {
                            //https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-develop-emulator?tabs=docker-linux%2Ccsharp&pivots=api-nosql#connect-to-the-emulator-from-the-sdk
                            opt.HttpClientFactory(() => new HttpClient(new HttpClientHandler
                                {
                                    ServerCertificateCustomValidationCallback =
                                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                                })
                            );

                            opt.ConnectionMode(ConnectionMode.Gateway);
                            opt.LimitToEndpoint();
                        });
                        
                        break;

                    case "ManagedIdentity":
                        var miEndpoint = config["CosmosDb:AccountEndpoint"]
                                         ?? throw new InvalidOperationException(
                                             "CosmosDb:AccountEndpoint is required for ManagedIdentity auth mode.");
                        options.UseCosmos(miEndpoint,
                            new ManagedIdentityCredential(new ManagedIdentityCredentialOptions()), "ticketflow");
                        break;

                    case "AzureCli":
                        var cliEndpoint = config["CosmosDb:AccountEndpoint"]
                                          ?? throw new InvalidOperationException(
                                              "CosmosDb:AccountEndpoint is required for AzureCli auth mode.");
                        options.UseCosmos(cliEndpoint, new AzureCliCredential(), "ticketflow");
                        break;

                    default:

                        var endpoint = config["CosmosDb:AccountEndpoint"]
                                       ?? throw new InvalidOperationException("CosmosDb:AccountEndpoint is required.");
                        options.UseCosmos(endpoint, new DefaultAzureCredential(), "ticketflow");
                        break;
                }
            });

            return services;
        }
    }

    extension(IHost host)
    {
        public async Task<IHost> EnsureCosmosDbInitializedAsync()
        {
            await using var scope = host.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            return host;
        }
    }
}
