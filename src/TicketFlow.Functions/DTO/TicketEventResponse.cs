using TicketFlow.Core.Models;

namespace TicketFlow.Functions.DTO;

public sealed record TicketEventResponse(
    string Id,
    string Name,
    int Capacity,
    string Venue,
    Money TicketPrice,
    int AvailableTickets,
    int TotalCapacity,
    DateTimeOffset Date
)
{
    public static TicketEventResponse FromTicketEvent(TicketEvent ticketEvent)
    {
        return new TicketEventResponse(
            ticketEvent.Id,
            ticketEvent.Name,
            ticketEvent.Capacity,
            ticketEvent.Venue,
            ticketEvent.TicketPrice,
            ticketEvent.AvailableTickets,
            ticketEvent.TotalCapacity,
            ticketEvent.Date
        );
    }
}
