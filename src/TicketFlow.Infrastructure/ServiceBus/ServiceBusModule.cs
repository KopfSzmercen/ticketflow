using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TicketFlow.Infrastructure.ServiceBus;

public static class ServiceBusModule
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddServiceBusModule()
        {
            services.AddOptions<ServiceBusOptions>()
                .BindConfiguration(ServiceBusOptions.SectionName)
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<ServiceBusOptions>, ServiceBusOptionsValidator>();
            services.AddSingleton<IServiceBusClientFactory, ServiceBusClientFactory>();

            return services;
        }
    }
}
