using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TicketFlow.Functions.DTO;
using TicketFlow.Functions.Http;
using TicketFlow.Functions.Triggers;
using TicketFlow.Infrastructure.BlobStorage;
using TicketFlow.Integration.Tests.Fixtures;
using Xunit;

namespace TicketFlow.Integration.Tests.Http;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class GetOrderTicketFunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Run_ShouldReturnSasUrl_WhenTicketExists()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var function = new GetOrderTicketFunction(fileStorage);

        var orderId = Guid.NewGuid().ToString("N");
        var blobPath = QrWorkerTrigger.BuildBlobPath(orderId);
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        };

        await using (var uploadStream = new MemoryStream(pngBytes))
        {
            await fileStorage.UploadAsync(BlobContainerAliases.Tickets, blobPath, uploadStream, "image/png");
        }

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(httpContext.Request, orderId);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<TicketUrlResponse>(json, JsonOptions);

        response.ShouldNotBeNull();
        response.OrderId.ShouldBe(orderId);
        response.ExpiresInSeconds.ShouldBe(900);
        response.TicketUrl.IsAbsoluteUri.ShouldBeTrue();
        response.TicketUrl.AbsolutePath.ShouldContain($"/tickets/{blobPath}");
        response.TicketUrl.Query.ShouldContain("sig=");
    }

    [Fact]
    public async Task Run_ShouldReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var function = new GetOrderTicketFunction(fileStorage);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };

        // Act
        var result = await function.Run(httpContext.Request, Guid.NewGuid().ToString("N"));
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }
}