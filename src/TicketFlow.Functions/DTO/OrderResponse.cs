using Microsoft.DurableTask.Client;
using TicketFlow.Core.Models;

namespace TicketFlow.Functions.DTO;

/// <summary>
/// Response returned by <c>GET /orders/{orderId}</c> and as the initial body of
/// <c>POST /orders 202 Accepted</c>.
///
/// The Cosmos <see cref="Order"/> is the primary, authoritative source of all fields.
/// Orchestration metadata fields are supplemental: they aid debugging and operational
/// visibility but are not required for business correctness.  Clients must not rely
/// on orchestration fields being present.
/// </summary>
public sealed record OrderResponse(
    string Id,
    string EventId,
    string AttendeeName,
    string AttendeeEmail,
    Money TicketPrice,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? FailureReason,
    // ── Supplemental orchestration / debug metadata ───────────────────────────
    string? OrchestrationStatus,
    DateTimeOffset? OrchestrationCreatedAt,
    DateTimeOffset? OrchestrationLastUpdatedAt
)
{
    public static OrderResponse FromOrder(Order order, OrchestrationMetadata? orchestration = null)
    {
        return new OrderResponse(
            order.Id,
            order.EventId,
            order.AttendeeName,
            order.AttendeeEmail,
            order.TicketPrice,
            order.Status.ToString(),
            order.CreatedAt,
            order.UpdatedAt,
            order.FailureReason,
            orchestration?.RuntimeStatus.ToString(),
            orchestration?.CreatedAt,
            orchestration?.LastUpdatedAt
        );
    }
}
