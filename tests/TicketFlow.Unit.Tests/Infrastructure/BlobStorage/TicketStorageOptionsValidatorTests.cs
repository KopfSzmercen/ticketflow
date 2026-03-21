using Shouldly;
using TicketFlow.Infrastructure.BlobStorage;
using Xunit;

namespace TicketFlow.Unit.Tests.Infrastructure.BlobStorage;

public sealed class TicketStorageOptionsValidatorTests
{
    private readonly TicketStorageOptionsValidator _sut = new();

    [Fact]
    public void Emulator_WithValidConnectionStringAndContainer_ShouldSucceed()
    {
        var options = new TicketStorageOptions
        {
            AuthMode = TicketStorageAuthMode.Emulator,
            ConnectionString = "UseDevelopmentStorage=true",
            Containers = new Dictionary<string, string>
            {
                [BlobContainerAliases.Tickets] = "tickets"
            }
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Emulator_WithMissingConnectionString_ShouldFail()
    {
        var options = new TicketStorageOptions
        {
            AuthMode = TicketStorageAuthMode.Emulator,
            ConnectionString = null,
            Containers = new Dictionary<string, string>
            {
                [BlobContainerAliases.Tickets] = "tickets"
            }
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionString");
    }

    [Fact]
    public void ManagedIdentity_WithValidAccountName_ShouldSucceed()
    {
        var options = new TicketStorageOptions
        {
            AuthMode = TicketStorageAuthMode.ManagedIdentity,
            AccountName = "stticketflowdev123",
            Containers = new Dictionary<string, string>
            {
                [BlobContainerAliases.Tickets] = "tickets"
            }
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void AzureCli_WithMissingAccountName_ShouldFail()
    {
        var options = new TicketStorageOptions
        {
            AuthMode = TicketStorageAuthMode.AzureCli,
            AccountName = null,
            Containers = new Dictionary<string, string>
            {
                [BlobContainerAliases.Tickets] = "tickets"
            }
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AccountName");
    }

    [Fact]
    public void DefaultAzureCredential_WithInvalidTicketsContainerName_ShouldFail()
    {
        var options = new TicketStorageOptions
        {
            AuthMode = TicketStorageAuthMode.DefaultAzureCredential,
            AccountName = "stticketflowdev123",
            Containers = new Dictionary<string, string>
            {
                [BlobContainerAliases.Tickets] = "Tickets"
            }
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TicketStorage:Containers values");
    }

    [Fact]
    public void AdditionalContainers_WithValidAliasAndName_ShouldSucceed()
    {
        var options = new TicketStorageOptions
        {
            AuthMode = TicketStorageAuthMode.AzureCli,
            AccountName = "stticketflowdev123",
            Containers = new Dictionary<string, string>
            {
                [BlobContainerAliases.Tickets] = "tickets",
                ["attachments"] = "event-attachments"
            }
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void AdditionalContainers_WithoutTicketsAlias_ShouldFail()
    {
        var options = new TicketStorageOptions
        {
            AuthMode = TicketStorageAuthMode.AzureCli,
            AccountName = "stticketflowdev123",
            Containers = new Dictionary<string, string>
            {
                ["attachments"] = "event-attachments"
            }
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Containers:tickets is required");
    }

    [Fact]
    public void AdditionalContainers_WithInvalidContainerName_ShouldFail()
    {
        var options = new TicketStorageOptions
        {
            AuthMode = TicketStorageAuthMode.AzureCli,
            AccountName = "stticketflowdev123",
            Containers = new Dictionary<string, string>
            {
                [BlobContainerAliases.Tickets] = "tickets",
                ["attachments"] = "Event-Attachments"
            }
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TicketStorage:Containers values");
    }
}
