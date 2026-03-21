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
        await ClearDatabaseAsync();
        await Fixture.ClearTicketsContainerAsync();
    }

    private async Task ClearDatabaseAsync()
    {
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var allTicketEvents = await dbContext.Events.ToListAsync();
        dbContext.Events.RemoveRange(allTicketEvents);

        var allOrders = await dbContext.Orders.ToListAsync();
        dbContext.Orders.RemoveRange(allOrders);

        await dbContext.SaveChangesAsync();
    }
}
