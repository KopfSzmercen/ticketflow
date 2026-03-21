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

# All integration tests (fast + durable E2E, requires Docker + func)
dotnet test tests/TicketFlow.Integration.Tests --filter "Category=Integration"

# Fast integration tests only (Cosmos-backed, no Functions host)
dotnet test tests/TicketFlow.Integration.Tests --filter "Category=Integration&IntegrationType!=DurableE2E"

# Durable end-to-end integration tests only (real Functions host + Azurite + Cosmos)
dotnet test tests/TicketFlow.Integration.Tests --filter "IntegrationType=DurableE2E"

# Service Bus readiness harness tests only (no broker roundtrip assertions)
dotnet test tests/TicketFlow.Integration.Tests --filter "IntegrationType=ServiceBusHarness"
```

## Integration Tests

Integration tests use **two collection fixtures**:

- `IntegrationTests` (`CosmosDbContainerFixture`) for fast function-level integration tests with real Cosmos persistence.
- `DurableIntegrationTests` (`DurableFunctionsHostFixture`) for runtime-backed durable end-to-end tests (real `func host`, Azurite, and Cosmos emulator).
- Durable end-to-end tests carry trait `IntegrationType=DurableE2E` so CI and local runs can select them reliably without class-name filters.

This split keeps the main feedback loop fast while still validating real orchestration execution where it matters.

### Fixture strategy and key decisions

- **Two fixtures by design** — lightweight fixture for broad coverage, heavyweight fixture only for durable runtime scenarios.
- **Generic `ContainerBuilder`, not `CosmosDbBuilder`** — the Testcontainers Cosmos module maps ports randomly, which breaks the emulator's internal redirect logic. Using the low-level `ContainerBuilder` keeps emulator connectivity stable in tests.
- **HTTP connection string, not HTTPS** — avoids SSL handshake failures against the emulator's self-signed certificate from inside the test process.
- **`HttpClientFactory` with `DangerousAcceptAnyServerCertificateValidator`** — still required in the `CosmosClientOptions` because the Cosmos SDK internally verifies the endpoint even over HTTP in gateway mode.
- **Direct invocation for fast tests** — non-durable integration tests instantiate functions from `IServiceProvider` and execute with `DefaultHttpContext`.
- **Real host for durable tests** — durable E2E tests launch `func host start`, call HTTP endpoints, then poll order state to assert terminal orchestration outcomes.

Decision record: `ADR 006` in `adr/006-two-integration-test-fixtures.md`.

## Local Development

### Running against local emulators

1. **Start emulators** (Azurite + Cosmos Emulator + Service Bus Emulator + SQL Edge sidecar):

   ```bash
   docker compose up -d
   ```

# Wait for Cosmos Emulator and Service Bus Emulator to be healthy

docker compose ps

````

**Cosmos DB Emulator Notes:**
- **SSL Validation:** For local development, SSL validation is disabled to simplify connectivity. //https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-develop-emulator?tabs=docker-linux%2Ccsharp&pivots=api-nosql#connect-to-the-emulator-from-the-sdk
- **Connection Mode:** Uses `Gateway` mode (HTTPS) as the emulator doesn't support Direct/TCP mode.
- **Endpoint Discovery:** Restricted to the local endpoint (`LimitToEndpoint = true`) to prevent timeouts from attempting to discover other regions.

**Service Bus Emulator Notes:**
- The emulator requires a SQL Edge dependency container (`sqledge`) and starts with static entities from `emulators/servicebus/config.json`.
- Runtime messaging connection string (local app process):
  - `Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`
- Management/Admin operations should use port `5300`:
  - `Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`

2. **Ensure `local.settings.json` is in emulator mode** (this is the default):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDb__AuthMode": "Emulator",
     "CosmosDb__ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
     "ServiceBus__AuthMode": "Emulator",
     "ServiceBus__ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
     "ServiceBus__AdministrationConnectionString": "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
     "ServiceBus__TopicName": "order-events",
     "ServiceBus__EmailSubscriptionName": "email-worker",
     "ServiceBus__AnalyticsSubscriptionName": "analytics-worker",
     "TicketStorage__AuthMode": "Emulator",
     "TicketStorage__ConnectionString": "UseDevelopmentStorage=true",
    "TicketStorage__Containers__tickets": "tickets"
  }
}
```

3. **Start the Functions host:**

   ```bash
   dotnet run --project src/TicketFlow.Functions
   ```

4. **Verify** using `requests/health.http` (REST Client extension) or:
   ```bash
   curl http://localhost:7071/api/health
   # Expected: {"status":"Healthy"}
   ```

### Running against live Azure (cloud-local)

1. Copy the cloud settings template over the active settings file:

   ```bash
   cp src/TicketFlow.Functions/local.settings.azurecli.json src/TicketFlow.Functions/local.settings.json
   ```

2. Log in with the Azure CLI:

   ```bash
   az login
   ```

3. Ensure your personal account has the required RBAC roles on the dev resources:

   ```bash
   # Cosmos DB
   az cosmosdb sql role assignment create \
     --account-name cosmos-ticketflow-dev \
     --resource-group rg-ticketflow-dev \
     --role-definition-name "Cosmos DB Built-in Data Contributor" \
     --principal-id $(az ad signed-in-user show --query id -o tsv) \
     --scope "/"

   # Storage
   az role assignment create \
     --role "Storage Blob Data Contributor" \
     --assignee $(az ad signed-in-user show --query id -o tsv) \
     --scope $(az storage account show -n <storage-account-name> -g rg-ticketflow-dev --query id -o tsv)

   # Service Bus
   az role assignment create \
     --role "Azure Service Bus Data Sender" \
     --assignee $(az ad signed-in-user show --query id -o tsv) \
     --scope $(az servicebus namespace show -n sb-ticketflow-dev -g rg-ticketflow-dev --query id -o tsv)

   az role assignment create \
     --role "Azure Service Bus Data Receiver" \
     --assignee $(az ad signed-in-user show --query id -o tsv) \
     --scope $(az servicebus namespace show -n sb-ticketflow-dev -g rg-ticketflow-dev --query id -o tsv)
   ```

   > The storage account name is auto-generated. Find it with:
   > `az storage account list -g rg-ticketflow-dev --query "[0].name" -o tsv`

5. Ensure `local.settings.json` contains Ticket Storage cloud-local settings:

   ```json
   {
     "Values": {
       "TicketStorage__AuthMode": "AzureCli",
       "TicketStorage__AccountName": "<storage-account-name>",
       "TicketStorage__Containers__tickets": "tickets"
     }
   }
   ```

6. Start the Functions host and verify as above.

### Settings files

| File                                                    | Purpose                           | Gitignored |
| ------------------------------------------------------- | --------------------------------- | ---------- |
| `src/TicketFlow.Functions/local.settings.json`          | Active settings (never committed) | ✅         |
| `src/TicketFlow.Functions/local.settings.azurecli.json` | Template for cloud-local runs     | ✅         |

### Running from Rider / IntelliJ

The Rider-bundled func CLI (v4.8.0) has known issues with net10.0:

1. **`local.settings.json` values are not forwarded to the isolated worker.** Workaround: set the required env vars directly in the Rider run configuration (**Run → Edit Configurations → Environment variables**).

2. **`AzureCliCredential` cannot find `az`.** Rider launches processes with a sanitised PATH. Fix: add `PATH=/usr/bin:/usr/local/bin` (or the full output of `echo $PATH`) to the same environment variables section.

3. **Durable integration tests fail with "A compatible .NET SDK was not found"** when Rider launches `func` with only `/usr/lib/dotnet` (for example, .NET 9 only) while the repository `global.json` pins .NET 10 (`10.0.101`).
   - Symptom: fixture throws `Azure Functions host exited early` and host logs show SDK resolution failure.
   - Fix: in the Rider test run configuration, set environment variables so `func` resolves the correct SDK:
     - `DOTNET_ROOT=/home/<your-user>/.dotnet`
     - `PATH=/home/<your-user>/.dotnet:/home/<your-user>/.dotnet/tools:/usr/local/bin:/usr/bin:$PATH`
   - Alternative: install the pinned SDK globally in `/usr/lib/dotnet`, or update `global.json` to a version present in Rider's environment.

The simplest alternative is to use `dotnet run --project src/TicketFlow.Functions` from the terminal — the SDK-bundled func CLI handles net10.0 correctly without any workarounds.

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
````
