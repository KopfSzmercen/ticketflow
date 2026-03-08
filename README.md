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

# Integration tests (requires Docker)
dotnet test tests/TicketFlow.Integration.Tests --filter "Category=Integration"
```

## Integration Tests

Integration tests spin up the **Cosmos DB Emulator** automatically via Testcontainers — no need to run `docker compose` first. The test host wires up the real `CosmosDbModule` DI registration and calls `EnsureCreatedAsync()` before any test runs, giving full coverage of the EF Core / Cosmos path.

### Testcontainers + Cosmos Emulator — key decisions

- **Generic `ContainerBuilder`, not `CosmosDbBuilder`** — the Testcontainers Cosmos module maps ports randomly, which breaks the emulator's internal redirect logic. Using the low-level `ContainerBuilder` with fixed port bindings (8081 → 8081, 10250-10255 → 10250-10255) keeps the endpoint stable at `localhost:8081`. Also it significantly improved start time of the Cosmos Emulator.
- **HTTP connection string, not HTTPS** — avoids SSL handshake failures against the emulator's self-signed certificate from inside the test process.
- **`HttpClientFactory` with `DangerousAcceptAnyServerCertificateValidator`** — still required in the `CosmosClientOptions` because the Cosmos SDK internally verifies the endpoint even over HTTP in gateway mode.
- **`HealthFunction` instantiated directly** — Azure Functions isolated-worker HTTP routing requires the real Functions runtime, which is too heavy to bring up in tests. Since the integration value is in the DB round-trip, `HealthFunction` is constructed from `IServiceProvider` and `Run()` is called directly with a `DefaultHttpContext`.
- **Shared `ICollectionFixture`** — the emulator takes ~30 s to start; sharing it across all test classes via `[CollectionDefinition]` avoids restarting it per class.

## Local Development

### Running against local emulators

1. **Start emulators** (Azurite + Cosmos Emulator):

   ```bash
   docker compose up -d
   # Wait for Cosmos Emulator to be healthy — takes ~60s on first run
   docker compose ps
   ```

   **Cosmos DB Emulator Notes:**
   - **SSL Validation:** For local development, SSL validation is disabled to simplify connectivity. //https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-develop-emulator?tabs=docker-linux%2Ccsharp&pivots=api-nosql#connect-to-the-emulator-from-the-sdk
   - **Connection Mode:** Uses `Gateway` mode (HTTPS) as the emulator doesn't support Direct/TCP mode.
   - **Endpoint Discovery:** Restricted to the local endpoint (`LimitToEndpoint = true`) to prevent timeouts from attempting to discover other regions.

2. **Ensure `local.settings.json` is in emulator mode** (this is the default):

   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "CosmosDb__AuthMode": "Emulator",
       "CosmosDb__ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
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
   ```

   > The storage account name is auto-generated. Find it with:
   > `az storage account list -g rg-ticketflow-dev --query "[0].name" -o tsv`

4. Start the Functions host and verify as above.

### Settings files

| File                                                    | Purpose                           | Gitignored |
| ------------------------------------------------------- | --------------------------------- | ---------- |
| `src/TicketFlow.Functions/local.settings.json`          | Active settings (never committed) | ✅         |
| `src/TicketFlow.Functions/local.settings.azurecli.json` | Template for cloud-local runs     | ✅         |

### Running from Rider / IntelliJ

The Rider-bundled func CLI (v4.8.0) has two known issues with net10.0:

1. **`local.settings.json` values are not forwarded to the isolated worker.** Workaround: set the required env vars directly in the Rider run configuration (**Run → Edit Configurations → Environment variables**).

2. **`AzureCliCredential` cannot find `az`.** Rider launches processes with a sanitised PATH. Fix: add `PATH=/usr/bin:/usr/local/bin` (or the full output of `echo $PATH`) to the same environment variables section.

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
