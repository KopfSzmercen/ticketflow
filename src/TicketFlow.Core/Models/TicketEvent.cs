namespace TicketFlow.Core.Models;

public class TicketEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public required Money TicketPrice { get; set; }

    public required string Name { get; set; }

    /// <summary>Total number of tickets originally created for the event.</summary>
    public required int TotalCapacity { get; set; }

    public required string Venue { get; set; }

    /// <summary>Remaining tickets that have not yet been reserved.</summary>
    public required int AvailableTickets { get; set; }

    public required DateTimeOffset Date { get; set; }

    public string? ETag { get; private set; }
}
