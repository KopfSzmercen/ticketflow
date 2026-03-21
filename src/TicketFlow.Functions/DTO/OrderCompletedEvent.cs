namespace TicketFlow.Functions.DTO;

/// <summary>
/// Integration event consumed by worker subscriptions.
/// EventName currently carries the EventId value for backward compatibility.
/// </summary>
public sealed record OrderCompletedEvent(
    string OrderId,
    string CustomerEmail,
    string EventName,
    int TicketQuantity,
    decimal TotalPrice,
    string Currency
);
