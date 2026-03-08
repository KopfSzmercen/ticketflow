using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using TicketFlow.Functions.Http;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Http;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class HealthFunctionTests
{
    private readonly CosmosDbContainerFixture _fixture;

    public HealthFunctionTests(CosmosDbContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Run_WhenCosmosDbIsReachable_ReturnsHealthy()
    {
        // Arrange
        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<HealthFunction>>();
        var healthFunction = new HealthFunction(dbContext, logger);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response =
            {
                Body = new MemoryStream()
            }
        };

        // Act
        var result = await healthFunction.Run(httpContext.Request);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
    }
}
