using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using TicketFlow.Core.Models;

namespace TicketFlow.Infrastructure.CosmosDb;

public class TicketFlowDbContext(DbContextOptions<TicketFlowDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<Money, string> MoneyConverter = new(
        value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
        value => JsonSerializer.Deserialize<Money>(value, (JsonSerializerOptions?)null)!);

    private static readonly ValueConverter<Money?, string?> NullableMoneyConverter = new(
        value => value == null
            ? null
            : JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
        value => value == null || value == string.Empty
            ? null
            : JsonSerializer.Deserialize<Money>(value, (JsonSerializerOptions?)null));

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

            entity.Property(e => e.TicketPrice)
                .HasConversion(MoneyConverter);

            modelBuilder.Entity<TicketEvent>()
                .Property(e => e.ETag)
                .IsETagConcurrency();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToContainer("orders");
            entity.HasKey(o => o.Id);
            entity.HasPartitionKey(o => o.Id);

            entity.Property(o => o.TicketPrice)
                .HasConversion(MoneyConverter);

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
                .HasConversion(NullableMoneyConverter);

            entity.Property(w => w.Status)
                .HasConversion<string>();

            entity.Property(w => w.ETag)
                .IsETagConcurrency();
        });
    }
}
