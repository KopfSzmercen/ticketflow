namespace TicketFlow.Functions.DTO;

public sealed record OrderCompletedEvent(
    string OrderId,
    string CustomerEmail,
    string EventName,
    int TicketQuantity,
    decimal TotalPrice,
    string Currency
);
