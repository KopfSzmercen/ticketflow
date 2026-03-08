# Plan: Test Local and Cloud Dev Environments Manually

The goal is to go from "zero running services" to a verified working `GET /health` response — first against local emulators, then against deployed Azure resources. Since only `HealthFunction` exists (it calls `dbContext.Database.CanConnectAsync()`), that single endpoint is enough to validate the full stack (Functions runtime → EF Core → CosmosDB).

## Part 1 — Local environment

1. **Create `docker-compose.yml` at the workspace root** with two services:
   - **Azurite** (`mcr.microsoft.com/azure-storage/azurite`) — expose ports `10000` (blob), `10001` (queue), `10002` (table); this satisfies `AzureWebJobsStorage=UseDevelopmentStorage=true`
   - **Cosmos Emulator** (`mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator`) — expose port `8081`; use the well-known emulator key already in `local.settings.json`; add a healthcheck so the emulator is fully ready before the Functions host tries to connect

2. **Verify `local.settings.json`** has the right values for emulator mode — `CosmosDb__AuthMode=Emulator` and the connection string pointing to `https://localhost:8081/` — which already looks correct; no change expected

3. **Check `Program.cs` for `EnsureCreatedAsync`** — EF Core Cosmos provider needs the database + container to exist; if it's not already called at startup, a one-liner `app.Services.GetRequiredService<TicketFlowDbContext>().Database.EnsureCreatedAsync()` needs to be added to the app startup, otherwise `CanConnectAsync()` may throw

4. **Start emulators**: `docker compose up -d` then wait for the Cosmos Emulator healthcheck to pass (can take ~60s on first run)

5. **Start the Functions host**: `dotnet run --project src/TicketFlow.Functions` or `func start` from `src/TicketFlow.Functions/`

6. **Create `requests/health.http`** — a simple `.http` file with `GET http://localhost:7071/api/health` to use from VS Code (REST Client extension) or as a curl reference; verify the response is `{ "status": "Healthy" }`

## Part 2 — Cloud environment (from local, no deployment)

7. **Create `local.settings.azurecli.json`** as a sibling to `local.settings.json` with:
   - `CosmosDb__AuthMode=AzureCli`
   - `CosmosDb__AccountEndpoint` set to the deployed account endpoint (e.g. `https://cosmos-ticketflow-dev.documents.azure.com:443/`)
   - `AzureWebJobsStorage` still using `UseDevelopmentStorage=true` (Azurite handles the Functions runtime storage — no need to hit Azure Storage for a simple health test)
   - This file is `.gitignore`-able; add it to `.gitignore` as a safety measure

8. **Run `az login`** to authenticate your local CLI session — `AzureCliCredential` picks this up automatically

9. **Swap the settings**: rename/copy `local.settings.azurecli.json` → `local.settings.json` (or manually patch the two values in the existing file)

10. **Confirm RBAC role** — your personal Azure account needs `Cosmos DB Built-in Data Contributor` on the CosmosDB account; the role assignments in `infra/modules/roleAssignments.bicep` target the Function App's managed identity; verify your personal object ID has the same role (or add it via `az cosmosdb sql role assignment create`)

11. **Start the Functions host** again with the updated settings; hit `GET http://localhost:7071/api/health` — expect `{ "status": "Healthy" }` against live Azure CosmosDB

## Verification

- Local: `docker compose ps` shows both containers healthy → `func start` boots without errors → `GET /api/health` returns `200 { "status": "Healthy" }`
- Cloud: `az account show` confirms logged-in identity → Functions host starts → `GET /api/health` returns `200 { "status": "Healthy" }` (any connection error would be `Unhealthy` + an exception log)

## Decisions

- `AzureCliCredential` chosen over `DefaultAzureCredential` for cloud-local testing — avoids credential chain ambiguity and makes auth explicit
- `AzureWebJobsStorage` stays on Azurite even in the cloud-local scenario — avoids needing Azure Storage RBAC on your personal account just for a health check
- `.http` file preferred over a test project — keeps things lightweight until CRUD endpoints exist
