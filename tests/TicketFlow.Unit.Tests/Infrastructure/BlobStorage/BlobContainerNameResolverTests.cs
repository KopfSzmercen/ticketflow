using Microsoft.Extensions.Options;
using Shouldly;
using TicketFlow.Infrastructure.BlobStorage;
using Xunit;

namespace TicketFlow.Unit.Tests.Infrastructure.BlobStorage;

public sealed class BlobContainerNameResolverTests
{
    [Fact]
    public void Resolve_WithTicketsAlias_ReturnsTicketsContainerName()
    {
        var options = CreateOptions();
        var sut = new BlobContainerNameResolver(Options.Create(options));

        var containerName = sut.Resolve(BlobContainerAliases.Tickets);

        containerName.ShouldBe("tickets");
    }

    [Fact]
    public void Resolve_WithConfiguredAlias_IsCaseInsensitive()
    {
        var options = CreateOptions();
        var sut = new BlobContainerNameResolver(Options.Create(options));

        var containerName = sut.Resolve("ATTACHMENTS");

        containerName.ShouldBe("event-attachments");
    }

    [Fact]
    public void Resolve_WithUnknownAlias_ThrowsKeyNotFoundException()
    {
        var options = CreateOptions();
        var sut = new BlobContainerNameResolver(Options.Create(options));

        Should.Throw<KeyNotFoundException>(() => sut.Resolve("missing"));
    }

    private static TicketStorageOptions CreateOptions()
    {
        return new TicketStorageOptions
        {
            Containers = new Dictionary<string, string>
            {
                [BlobContainerAliases.Tickets] = "tickets",
                ["attachments"] = "event-attachments"
            }
        };
    }
}
