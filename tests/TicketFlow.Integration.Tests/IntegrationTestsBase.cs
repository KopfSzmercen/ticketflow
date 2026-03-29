using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests;

[Collection("IntegrationTests")]
public class IntegrationTestsBase : IAsyncLifetime
{
    protected readonly CosmosDbContainerFixture Fixture;

    protected IntegrationTestsBase(CosmosDbContainerFixture fixture)
    {
        Fixture = fixture;
    }

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        await Fixture.ClearDatabaseAsync();
        await Fixture.ClearTicketsContainerAsync();
    }
}
