using Microsoft.Extensions.Options;

namespace TicketFlow.Infrastructure.ServiceBus;

public sealed class ServiceBusOptionsValidator : IValidateOptions<ServiceBusOptions>
{
    public ValidateOptionsResult Validate(string? name, ServiceBusOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TopicName))
            return ValidateOptionsResult.Fail("ServiceBus:TopicName is required.");

        if (string.IsNullOrWhiteSpace(options.EmailSubscriptionName))
            return ValidateOptionsResult.Fail("ServiceBus:EmailSubscriptionName is required.");

        if (string.IsNullOrWhiteSpace(options.AnalyticsSubscriptionName))
            return ValidateOptionsResult.Fail("ServiceBus:AnalyticsSubscriptionName is required.");

        if (string.IsNullOrWhiteSpace(options.QrSubscriptionName))
            return ValidateOptionsResult.Fail("ServiceBus:QrSubscriptionName is required.");

        if (options.AuthMode == ServiceBusAuthMode.Emulator)
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                return ValidateOptionsResult.Fail(
                    "ServiceBus:ConnectionString is required when AuthMode is Emulator.");

            if (!ContainsUseDevelopmentEmulator(options.ConnectionString))
                return ValidateOptionsResult.Fail(
                    "ServiceBus:ConnectionString must include UseDevelopmentEmulator=true in Emulator mode.");

            if (!string.IsNullOrWhiteSpace(options.AdministrationConnectionString))
            {
                if (!ContainsUseDevelopmentEmulator(options.AdministrationConnectionString))
                    return ValidateOptionsResult.Fail(
                        "ServiceBus:AdministrationConnectionString must include UseDevelopmentEmulator=true in Emulator mode.");

                if (!ContainsEmulatorManagementPort(options.AdministrationConnectionString))
                    return ValidateOptionsResult.Fail(
                        "ServiceBus:AdministrationConnectionString must target emulator management port 5300.");
            }

            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace))
            return ValidateOptionsResult.Fail(
                $"ServiceBus:FullyQualifiedNamespace is required when AuthMode is {options.AuthMode}.");

        if (!options.FullyQualifiedNamespace.Contains('.', StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Fail(
                "ServiceBus:FullyQualifiedNamespace must be a valid Service Bus namespace FQDN.");

        return ValidateOptionsResult.Success;
    }

    private static bool ContainsUseDevelopmentEmulator(string connectionString)
    {
        return connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsEmulatorManagementPort(string connectionString)
    {
        return connectionString.Contains("Endpoint=sb://localhost:5300", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains("Endpoint=sb://127.0.0.1:5300", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains(":5300;", StringComparison.OrdinalIgnoreCase);
    }
}
