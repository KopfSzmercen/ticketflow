using TicketFlow.Core.Models;

namespace TicketFlow.Functions.DTO;

public sealed record WaitlistEntryResponse(
    string Id,
    string EventId,
    string AttendeeId,
    string AttendeeName,
    string AttendeeContact,
    string Status,
    DateTimeOffset EnqueuedAt,
    string? OfferInstanceId,
    DateTimeOffset? OfferedAt,
    DateTimeOffset? OfferExpiresAt,
    Money? OfferedTicketPrice,
    DateTimeOffset? ClaimedAt
)
{
    public static WaitlistEntryResponse FromWaitlistEntry(WaitlistEntry entry)
    {
        return new WaitlistEntryResponse(
            entry.Id,
            entry.EventId,
            entry.AttendeeId,
            entry.AttendeeName,
            entry.AttendeeContact,
            entry.Status.ToString(),
            entry.EnqueuedAt,
            entry.OfferInstanceId,
            entry.OfferedAt,
            entry.OfferExpiresAt,
            entry.OfferedTicketPrice,
            entry.ClaimedAt
        );
    }
}