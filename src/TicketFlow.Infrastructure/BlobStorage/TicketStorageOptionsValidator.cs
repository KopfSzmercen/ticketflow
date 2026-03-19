using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TicketFlow.Infrastructure.BlobStorage;

public sealed partial class TicketStorageOptionsValidator : IValidateOptions<TicketStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, TicketStorageOptions options)
    {
        if (!options.Containers.ContainsKey(BlobContainerAliases.Tickets))
            return ValidateOptionsResult.Fail(
                $"TicketStorage:Containers:{BlobContainerAliases.Tickets} is required.");

        foreach (var (alias, containerName) in options.Containers)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return ValidateOptionsResult.Fail("TicketStorage:Containers aliases must be non-empty.");

            if (!ContainerAliasRegex().IsMatch(alias))
                return ValidateOptionsResult.Fail(
                    "TicketStorage:Containers aliases must be lowercase and contain only letters, numbers, and single hyphens.");

            if (string.IsNullOrWhiteSpace(containerName) || !ContainerNameRegex().IsMatch(containerName))
                return ValidateOptionsResult.Fail(
                    "TicketStorage:Containers values must be 3-63 characters, lowercase, and use only letters, numbers, and single hyphens.");
        }

        if (options.AuthMode == TicketStorageAuthMode.Emulator)
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                return ValidateOptionsResult.Fail(
                    "TicketStorage:ConnectionString is required when AuthMode is Emulator.");

            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.AccountName))
            return ValidateOptionsResult.Fail(
                $"TicketStorage:AccountName is required when AuthMode is {options.AuthMode}.");

        if (!StorageAccountNameRegex().IsMatch(options.AccountName))
            return ValidateOptionsResult.Fail(
                "TicketStorage:AccountName must be 3-24 lowercase alphanumeric characters.");

        return ValidateOptionsResult.Success;
    }

    [GeneratedRegex("^(?=.{3,63}$)[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ContainerNameRegex();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ContainerAliasRegex();

    [GeneratedRegex("^[a-z0-9]{3,24}$")]
    private static partial Regex StorageAccountNameRegex();
}
