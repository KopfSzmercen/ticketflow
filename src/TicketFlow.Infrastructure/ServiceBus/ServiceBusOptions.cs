namespace TicketFlow.Infrastructure.ServiceBus;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public ServiceBusAuthMode AuthMode { get; init; } = ServiceBusAuthMode.DefaultAzureCredential;

    /// <summary>Required when <see cref="AuthMode"/> is <see cref="ServiceBusAuthMode.Emulator"/>.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Optional admin/management connection string. For emulator scenarios this should
    /// use management port 5300 when administration operations are needed.
    /// </summary>
    public string? AdministrationConnectionString { get; init; }

    /// <summary>Required when <see cref="AuthMode"/> is not <see cref="ServiceBusAuthMode.Emulator"/>.</summary>
    public string? FullyQualifiedNamespace { get; init; }

    public string TopicName { get; init; } = "order-events";

    public string EmailSubscriptionName { get; init; } = "email-worker";

    public string AnalyticsSubscriptionName { get; init; } = "analytics-worker";
}
