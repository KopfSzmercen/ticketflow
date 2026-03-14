namespace TicketFlow.Core.Models;

public class TicketEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public required Money TicketPrice { get; init; }

    public required string Name { get; set; }

    /// <summary>Total number of tickets originally created for the event.</summary>
    public required int TotalCapacity { get; init; }

    public required string Venue { get; init; }

    /// <summary>Remaining tickets that have not yet been reserved.</summary>
    public required int AvailableTickets { get; set; }

    public required DateTimeOffset Date { get; init; }

    public string? ETag { get; private set; }

    public required int ReservationExpirationInSeconds { get; set; };
}
