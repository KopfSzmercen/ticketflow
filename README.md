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

## Infrastructure

All Azure resources are provisioned via Bicep. No manual portal clicks.

### Structure

```
infra/
  main.bicep              # Subscription-scoped entry point — creates the Resource Group
  modules/
    storage.bicep         # Storage Account (required by Functions runtime)
    cosmos.bicep          # Cosmos DB account + database + events container
    functions.bicep       # Consumption App Service Plan + Function App
  parameters/
    dev.bicepparam        # Dev environment values
```

To add a new environment (e.g. prod), copy `dev.bicepparam`, rename it `prod.bicepparam`, and change the values. The modules are environment-agnostic.

### Prerequisites

```bash
az extension add --name bicep   # or: az bicep install
az login
az account set --subscription "<your-subscription-id>"
```

### Validate (compile Bicep locally)

```bash
az bicep build --file infra/main.bicep
```

### What-if (dry run — no changes made)

```bash
az deployment sub what-if \
  --location polandcentral \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam
```

### Deploy

```bash
az deployment sub create \
  --location polandcentral \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam
```

### Tear down

```bash
az group delete --name rg-ticketflow-dev --yes --no-wait
```
