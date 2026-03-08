using Shouldly;
using TicketFlow.Infrastructure.CosmosDb;
using Xunit;

namespace TicketFlow.Unit.Tests.Infrastructure.CosmosDb;

public sealed class CosmosDbOptionsValidatorTests
{
    private readonly CosmosDbOptionsValidator _sut = new();

    [Fact]
    public void Emulator_WithValidConnectionString_ShouldSucceed()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.Emulator,
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6=="
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Emulator_WithNullConnectionString_ShouldFail()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.Emulator,
            ConnectionString = null
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionString");
    }

    [Fact]
    public void Emulator_WithEmptyConnectionString_ShouldFail()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.Emulator,
            ConnectionString = "   "
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionString");
    }


    [Fact]
    public void DefaultAzureCredential_WithValidHttpsEndpoint_ShouldSucceed()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.DefaultAzureCredential,
            AccountEndpoint = "https://my-account.documents.azure.com:443/"
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void DefaultAzureCredential_WithNullEndpoint_ShouldFail()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.DefaultAzureCredential,
            AccountEndpoint = null
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AccountEndpoint");
    }

    [Fact]
    public void DefaultAzureCredential_WithNonHttpsEndpoint_ShouldFail()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.DefaultAzureCredential,
            AccountEndpoint = "http://my-account.documents.azure.com:443/"
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("HTTPS");
    }


    [Fact]
    public void ManagedIdentity_WithValidHttpsEndpoint_ShouldSucceed()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.ManagedIdentity,
            AccountEndpoint = "https://my-account.documents.azure.com:443/"
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void ManagedIdentity_WithNullEndpoint_ShouldFail()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.ManagedIdentity,
            AccountEndpoint = null
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AccountEndpoint");
    }

    [Fact]
    public void AzureCli_WithValidHttpsEndpoint_ShouldSucceed()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.AzureCli,
            AccountEndpoint = "https://my-account.documents.azure.com:443/"
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void AzureCli_WithNullEndpoint_ShouldFail()
    {
        var options = new CosmosDbOptions
        {
            AuthMode = CosmosDbAuthMode.AzureCli,
            AccountEndpoint = null
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AccountEndpoint");
    }
}
