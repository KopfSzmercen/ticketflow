using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class JoinWaitlistFunction(
    TicketFlowDbContext dbContext,
    ILogger<JoinWaitlistFunction> logger
)
{
    [Function("JoinWaitlist")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/waitlist")] [FromBody]
        Request request,
        string eventId
    )
    {
        if (string.IsNullOrWhiteSpace(request.AttendeeId)
            || string.IsNullOrWhiteSpace(request.AttendeeName)
            || string.IsNullOrWhiteSpace(request.AttendeeContact))
        {
            logger.LogWarning(
                "Waitlist join rejected for event {EventId}. Missing attendee information.",
                eventId
            );

            return Results.BadRequest(new
            {
                error = "attendee_info_required",
                message = "attendeeId, attendeeName, and attendeeContact are required."
            });
        }

        var ticketEvent = await dbContext.Events
            .AsNoTracking()
            .WithPartitionKey(eventId)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (ticketEvent is null)
        {
            logger.LogInformation(
                "Waitlist join failed. Event {EventId} was not found.",
                eventId
            );
            return Results.NotFound(new
            {
                error = "event_not_found",
                message = $"Event '{eventId}' was not found."
            });
        }

        if (ticketEvent.AvailableTickets > 0)
        {
            logger.LogInformation(
                "Waitlist join rejected for event {EventId}. Event is not sold out, available tickets: {AvailableTickets}.",
                eventId,
                ticketEvent.AvailableTickets
            );

            return Results.Conflict(new
            {
                error = "event_not_sold_out",
                message = "Waitlist can only be joined when the event is sold out."
            });
        }

        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid().ToString(),
            EventId = eventId,
            AttendeeId = request.AttendeeId,
            AttendeeName = request.AttendeeName,
            AttendeeContact = request.AttendeeContact,
            Status = WaitlistStatus.Waiting,
            EnqueuedAt = DateTimeOffset.UtcNow
        };

        await dbContext.WaitlistEntries.AddAsync(entry);
        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Waitlist join succeeded for event {EventId}. EntryId: {EntryId}, AttendeeId: {AttendeeId}.",
            eventId,
            entry.Id,
            request.AttendeeId
        );

        return Results.Created(
            $"/events/{eventId}/waitlist/{entry.Id}",
            WaitlistEntryResponse.FromWaitlistEntry(entry)
        );
    }

    public sealed record Request(string AttendeeId, string AttendeeName, string AttendeeContact);
}
