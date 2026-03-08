namespace TicketFlow.Core.Models;

public class TicketEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
}
