using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TicketFlow.Core.Models;

namespace TicketFlow.Infrastructure.CosmosDb;

public class TicketFlowDbContext(DbContextOptions<TicketFlowDbContext> options) : DbContext(options)
{
    public DbSet<TicketEvent> Events => Set<TicketEvent>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TicketEvent>(entity =>
        {
            entity.ToContainer("events");
            entity.HasKey(e => e.Id);
            entity.HasPartitionKey(e => e.Id);

            entity.ComplexProperty(e => e.TicketPrice,
                priceBuilder => { priceBuilder.Property(p => p.Currency).HasConversion<string>(); });

            modelBuilder.Entity<TicketEvent>()
                .Property(e => e.ETag)
                .IsETagConcurrency();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToContainer("orders");
            entity.HasKey(o => o.Id);
            entity.HasPartitionKey(o => o.Id);

            entity.ComplexProperty(o => o.TicketPrice,
                priceBuilder => { priceBuilder.Property(p => p.Currency).HasConversion<string>(); });

            entity.Property(o => o.Status)
                .HasConversion<string>();

            entity.Property(o => o.ETag)
                .IsETagConcurrency();
        });

        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.ToContainer("waitlist");
            entity.HasKey(w => w.Id);
            entity.HasPartitionKey(w => w.EventId);

            entity.Property(w => w.OfferedTicketPrice)
                .HasConversion(
                    value => value == null
                        ? null
                        : JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    value => value == null || value == string.Empty
                        ? null
                        : JsonSerializer.Deserialize<Money>(value, (JsonSerializerOptions?)null));

            entity.Property(w => w.Status)
                .HasConversion<string>();

            entity.Property(w => w.ETag)
                .IsETagConcurrency();
        });
    }
}
