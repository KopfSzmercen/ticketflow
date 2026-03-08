using Microsoft.EntityFrameworkCore;
using TicketFlow.Core.Models;

namespace TicketFlow.Infrastructure.CosmosDb;

public class TicketFlowDbContext(DbContextOptions<TicketFlowDbContext> options) : DbContext(options)
{
    public DbSet<TicketEvent> Events => Set<TicketEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TicketEvent>(entity =>
        {
            entity.ToContainer("events");
            entity.HasKey(e => e.Id);
            entity.HasPartitionKey(e => e.Id);
        });
    }
}
