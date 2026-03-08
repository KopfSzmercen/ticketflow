using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketFlow.Infrastructure.CosmosDb;

namespace TicketFlow.Functions.Http;

public class HealthFunction(TicketFlowDbContext dbContext, ILogger<HealthFunction> logger)
{
    [Function("Health")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequest req
    )
    {
        try
        {
            await dbContext.Events.ToListAsync();
            logger.LogInformation("Health check successful. Database connection is healthy.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed. Database connection is unhealthy.");
            return Results.Ok(new { status = "Unhealthy" });
        }

        return Results.Ok(new { status = "Healthy" });
    }
}
