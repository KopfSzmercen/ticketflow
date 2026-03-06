# TicketFlow

Event ticketing platform built on Azure — .NET 10 Azure Functions (isolated worker), Cosmos DB, Service Bus, and Blob Storage.

## Solution Layout

```
src/
  TicketFlow.Core/           Domain models & interfaces (no Azure SDK references)
  TicketFlow.Infrastructure/ Azure SDK implementations (Cosmos DB, Service Bus, Blob)
  TicketFlow.Functions/      Azure Functions host (HTTP, Durable, triggers)
tests/
  TicketFlow.Unit.Tests/     Fast unit tests, no external dependencies
  TicketFlow.Integration.Tests/ Tests against emulators (Azurite, Cosmos Emulator)
infra/                       Bicep IaC
frontend/                    React app (placeholder)
```

## Quick Start

```bash
# Build
dotnet build TicketFlow.sln

# Unit tests only
dotnet test --filter "Category!=Integration"

# Integration tests (requires emulators)
dotnet test --filter "Category=Integration"
```
