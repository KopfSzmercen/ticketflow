using Microsoft.Extensions.Options;

namespace TicketFlow.Infrastructure.CosmosDb;

public sealed class CosmosDbOptionsValidator : IValidateOptions<CosmosDbOptions>
{
    public ValidateOptionsResult Validate(string? name, CosmosDbOptions options)
    {
        if (options.AuthMode == CosmosDbAuthMode.Emulator)
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                return ValidateOptionsResult.Fail(
                    "CosmosDb:ConnectionString is required when AuthMode is Emulator.");

            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.AccountEndpoint))
            return ValidateOptionsResult.Fail(
                $"CosmosDb:AccountEndpoint is required when AuthMode is {options.AuthMode}.");

        if (!Uri.TryCreate(options.AccountEndpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            return ValidateOptionsResult.Fail(
                "CosmosDb:AccountEndpoint must be a valid absolute HTTPS URI.");

        return ValidateOptionsResult.Success;
    }
}
