using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Core.Models;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class TicketEventConcurrencyTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task TicketEventEtag_ShouldProtect_FromConcurrentUpdates()
    {
        var ticketEventId = Guid.NewGuid().ToString();

        //Arrange
        using (var scope = Fixture.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

            var ticketEvent = new TicketEvent
            {
                Id = ticketEventId,
                Name = "Concurrent Event",
                Venue = "Test Venue",
                TicketPrice = new Money(50, "USD"),
                TotalCapacity = 100,
                Date = new DateTimeOffset(2024, 12, 31, 20, 0, 0, TimeSpan.Zero),
                AvailableTickets = 100
            };

            await dbContext.Events.AddAsync(ticketEvent);
            await dbContext.SaveChangesAsync();
        }

        //Act 1
        var mainScope = Fixture.Services.CreateScope();

        var mainDbContext = mainScope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var event1 = await mainDbContext.Events.FirstAsync(e => e.Id == ticketEventId);

        //Act 2
        using (var scope2 = Fixture.Services.CreateScope())
        {
            var dbContext2 = scope2.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            var event2 = await dbContext2.Events.FirstAsync(e => e.Id == ticketEventId);
            event2.Name = "Updated Event Name 2";
            await dbContext2.SaveChangesAsync();
        }

        //Act 3 - attempt to save changes from first context, should throw DbUpdateConcurrencyException
        event1.Name = "Updated Event Name 1";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await mainDbContext.SaveChangesAsync());
    }
}
