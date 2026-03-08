using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TicketFlow.Infrastructure.CosmosDb;

public static class CosmosDbModule
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCosmosDbModule()
        {
            services.AddOptions<CosmosDbOptions>()
                .BindConfiguration(CosmosDbOptions.SectionName)
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>();

            services.AddDbContext<TicketFlowDbContext>((sp, options) =>
            {
                var cosmosOptions = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

                switch (cosmosOptions.AuthMode)
                {
                    case CosmosDbAuthMode.Emulator:
                        options.UseCosmos(cosmosOptions.ConnectionString!, "ticketflow", opt =>
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

                    case CosmosDbAuthMode.ManagedIdentity:
                        options.UseCosmos(cosmosOptions.AccountEndpoint!,
                            new ManagedIdentityCredential(new ManagedIdentityCredentialOptions()), "ticketflow");
                        break;

                    case CosmosDbAuthMode.AzureCli:
                        options.UseCosmos(cosmosOptions.AccountEndpoint!, new AzureCliCredential(), "ticketflow");
                        break;

                    default:
                        options.UseCosmos(cosmosOptions.AccountEndpoint!, new DefaultAzureCredential(), "ticketflow");
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
