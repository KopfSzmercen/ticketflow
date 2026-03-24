using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Functions.Activities;
using TicketFlow.Functions.Qr;
using TicketFlow.Functions.Waitlist;
using TicketFlow.Infrastructure.BlobStorage;
using TicketFlow.Infrastructure.CosmosDb;
using TicketFlow.Infrastructure.ServiceBus;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.AddCosmosDbModule();
builder.Services.AddServiceBusModule();
builder.Services.AddBlobStorageModule();
builder.Services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
builder.Services.AddSingleton<IOrderCompletedEventPublisher, ServiceBusOrderCompletedEventPublisher>();
builder.Services.AddScoped<IWaitlistOfferCoordinator, WaitlistOfferCoordinator>();

var app = builder.Build();

await app.EnsureCosmosDbInitializedAsync();
await app.EnsureBlobContainersInitializedAsync();
await app.RunAsync();
