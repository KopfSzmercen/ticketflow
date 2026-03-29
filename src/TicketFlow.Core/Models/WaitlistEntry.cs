namespace TicketFlow.Core.Models;

public class WaitlistEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public required string EventId { get; set; }

    public required string AttendeeId { get; set; }

    public required string AttendeeName { get; set; }

    public required string AttendeeEmail { get; set; }

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;

    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? OfferInstanceId { get; set; }

    public DateTimeOffset? OfferedAt { get; set; }

    public DateTimeOffset? OfferExpiresAt { get; set; }

    public Money? OfferedTicketPrice { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? ETag { get; private set; }
}