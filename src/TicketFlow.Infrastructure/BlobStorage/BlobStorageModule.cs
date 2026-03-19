using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TicketFlow.Infrastructure.BlobStorage;

public static class BlobStorageModule
{
    private static TokenCredential CreateCredential(TicketStorageAuthMode authMode)
    {
        return authMode switch
        {
            TicketStorageAuthMode.ManagedIdentity =>
                new ManagedIdentityCredential(new ManagedIdentityCredentialOptions()),
            TicketStorageAuthMode.AzureCli => new AzureCliCredential(),
            _ => new DefaultAzureCredential()
        };
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddBlobStorageModule()
        {
            services.AddOptions<TicketStorageOptions>()
                .BindConfiguration(TicketStorageOptions.SectionName)
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<TicketStorageOptions>, TicketStorageOptionsValidator>();

            services.AddSingleton<BlobServiceClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<TicketStorageOptions>>().Value;

                if (options.AuthMode == TicketStorageAuthMode.Emulator)
                    return new BlobServiceClient(options.ConnectionString!);

                var serviceUri = new Uri($"https://{options.AccountName}.blob.core.windows.net");
                return new BlobServiceClient(serviceUri, CreateCredential(options.AuthMode));
            });

            services.AddSingleton<IBlobContainerNameResolver, BlobContainerNameResolver>();
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();

            return services;
        }
    }
}
