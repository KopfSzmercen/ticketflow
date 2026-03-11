using TicketFlow.Core.Models;

namespace TicketFlow.Functions.DTO;

public sealed record TicketEventResponse(
    string Id,
    string Name,
    string Venue,
    Money TicketPrice,
    int TotalCapacity,
    int AvailableTickets,
    DateTimeOffset Date
)
{
    public static TicketEventResponse FromTicketEvent(TicketEvent ticketEvent)
    {
        return new TicketEventResponse(
            ticketEvent.Id,
            ticketEvent.Name,
            ticketEvent.Venue,
            ticketEvent.TicketPrice,
            ticketEvent.TotalCapacity,
            ticketEvent.AvailableTickets,
            ticketEvent.Date
        );
    }
}
