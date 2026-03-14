using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using TicketFlow.Core.Models;
using TicketFlow.Functions.DTO;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public sealed class CreateEventFunction(TicketFlowDbContext dbContext)
{
    [Function("CreateEvent")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events")] [FromBody]
        Request newEvent
    )
    {
        var ticketEvent = new TicketEvent
        {
            Id = Guid.NewGuid().ToString(),
            Name = newEvent.Name,
            Venue = newEvent.Venue,
            TicketPrice = newEvent.TicketPrice,
            TotalCapacity = newEvent.TotalCapacity,
            Date = newEvent.Date,
            AvailableTickets = newEvent.TotalCapacity,
            ReservationExpirationInSeconds = newEvent.ReservationExpirationInSeconds
        };

        await dbContext.Events.AddAsync(ticketEvent);
        await dbContext.SaveChangesAsync();

        return Results.Created(
            $"/events/{ticketEvent.Id}",
            TicketEventResponse.FromTicketEvent(ticketEvent)
        );
    }

    public sealed record Request(
        string Name,
        string Venue,
        Money TicketPrice,
        int TotalCapacity,
        DateTimeOffset Date,
        int ReservationExpirationInSeconds
    );
}
