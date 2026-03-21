namespace TicketFlow.Functions.DTO;

public sealed record TicketUrlResponse(string OrderId, Uri TicketUrl, int ExpiresInSeconds);