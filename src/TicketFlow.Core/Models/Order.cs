namespace TicketFlow.Core.Models;

public class Order
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public required string EventId { get; set; }

    public required string AttendeeName { get; set; }

    public required string AttendeeEmail { get; set; }

    public required Money TicketPrice { get; set; }

    public string? WaitlistOfferInstanceId { get; set; }

    /// <summary>
    /// When <c>true</c> the payment activity simulates a successful payment;
    /// when <c>false</c> it simulates a declined payment.
    /// Controlled by the caller to enable deterministic integration testing.
    /// </summary>
    public bool SimulatePaymentSuccess { get; init; } = true;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>Populated when <see cref="Status"/> is <see cref="OrderStatus.Failed"/>.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? ETag { get; private set; }
}
