using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Functions.Activities;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Infrastructure.ServiceBus;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.AddCosmosDbModule();
builder.Services.AddServiceBusModule();
builder.Services.AddSingleton<IOrderCompletedEventPublisher, ServiceBusOrderCompletedEventPublisher>();

var app = builder.Build();

await app.EnsureCosmosDbInitializedAsync();
await app.RunAsync();
