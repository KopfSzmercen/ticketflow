using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using TicketFlow.Infrastructure.CosmosDb;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.AddCosmosDbModule();

var app = builder.Build();

await app.EnsureCosmosDbInitializedAsync();
await app.RunAsync();
