# Plan: Azure Functions HTTP Setup for Event Creation (EF Core 10)

**TL;DR:** The same 3-package, auth-mode-switch, health-endpoint structure as the original, but `Microsoft.Azure.Cosmos` is replaced by `Microsoft.EntityFrameworkCore.Cosmos` (EF Core 10 Cosmos provider). Instead of registering a `CosmosClient` singleton, a `TicketFlowDbContext` is registered via `AddDbContext` with a `UseCosmos(...)` factory that honours the same `CosmosDb__AuthMode` key. A minimal `TicketEvent` entity is added to `TicketFlow.Core/Models` so EF Core can build its model; the `DbContext` itself lives in `TicketFlow.Infrastructure/CosmosDb`. No migrations are needed — the container (`events`, partition key `/id`) is already provisioned by the Bicep module.

---

## Packages

| Package                                                       | Target project | Why                                                                         |
| ------------------------------------------------------------- | -------------- | --------------------------------------------------------------------------- |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | Functions      | ASP.NET Core `HttpRequest`/`IActionResult` HTTP model for isolated worker   |
| `Microsoft.EntityFrameworkCore.Cosmos`                        | Infrastructure | EF Core 10 Cosmos DB provider (bundles the raw SDK internally)              |
| `Azure.Identity`                                              | Functions      | `ManagedIdentityCredential`, `AzureCliCredential`, `DefaultAzureCredential` |

`Microsoft.EntityFrameworkCore` does **not** need to be added explicitly to Functions — it arrives transitively via the existing `ProjectReference` to `TicketFlow.Infrastructure`.

---

## Steps

### 1. Update `Directory.Packages.props`

Add `PackageVersion` entries for all 3 packages. Use `10.0.0` (or latest `10.0.x` patch) for `Microsoft.EntityFrameworkCore.Cosmos`; match the stable GA versions for the other two.

### 2. Update `src/TicketFlow.Infrastructure/TicketFlow.Infrastructure.csproj`

Add `<PackageReference Include="Microsoft.EntityFrameworkCore.Cosmos" />`.

### 3. Update `src/TicketFlow.Functions/TicketFlow.Functions.csproj`

Three additions:

- `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- `<PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" />`
- `<PackageReference Include="Azure.Identity" />`

### 4. Create `src/TicketFlow.Core/Models/TicketEvent.cs`

A minimal POCO (at minimum `string Id`) so EF Core can build a non-empty model. Properties will grow in later phases; the important thing now is that the entity exists and maps to the `events` container.

### 5. Create `src/TicketFlow.Infrastructure/CosmosDb/TicketFlowDbContext.cs`

A `DbContext` subclass with:

- `DbSet<TicketEvent> Events`
- `OnModelCreating` calling `entity.ToContainer("events")`, `entity.HasKey(e => e.Id)`, `entity.HasPartitionKey(e => e.Id)` — matching the partition key already defined in the Bicep Cosmos module.

### 6. Rewrite `src/TicketFlow.Functions/Program.cs`

Switch to `ConfigureFunctionsWebApplication()` and register `TicketFlowDbContext` via `AddDbContext<TicketFlowDbContext>((sp, options) => { ... })` with the same four-branch auth switch, but calling EF Core's `UseCosmos` instead of constructing a `CosmosClient`:

```
"Emulator"          → options.UseCosmos(connectionString, databaseName: "ticketflow")
"ManagedIdentity"   → options.UseCosmos(endpoint, new ManagedIdentityCredential(), databaseName: "ticketflow")
"AzureCli"          → options.UseCosmos(endpoint, new AzureCliCredential(), databaseName: "ticketflow")
(default)           → options.UseCosmos(endpoint, new DefaultAzureCredential(), databaseName: "ticketflow")
```

The `TokenCredential` overload of `UseCosmos` was added in EF Core 9 and is fully supported in EF Core 10.

### 7. Create `src/TicketFlow.Functions/local.settings.json`

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDb__AuthMode": "Emulator",
    "CosmosDb__ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMcZcLU/D4bAe7ea=="
  }
}
```

The emulator key is the well-known public default; it's safe to commit if desired, but `local.settings.json` is typically in `.gitignore` anyway.

### 8. Update `infra/modules/functions.bicep`

Add `CosmosDb__AuthMode = "ManagedIdentity"` to the `appSettings` array. The existing `CosmosDb__AccountEndpoint` entry is already present — no change there.

### 9. Add the minimal `GET /health` function

Create `src/TicketFlow.Functions/Http/HealthFunction.cs` — `[Function("Health")]` + `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]` returning `Results.Ok(new { status = "healthy" })`. No `TicketFlowDbContext` injection — pure routing smoke test. A Cosmos connectivity check can be added in a later phase when a real repository operation exists.

---

## Verification

**Locally:**

1. Start Azurite (VS Code sidebar or `azurite` in terminal)
2. Start the Cosmos emulator (Docker: `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator`)
3. `cd src/TicketFlow.Functions && func start`
4. `curl http://localhost:7071/api/health` → `{"status":"healthy"}`

**On cloud:**

1. Push to `main` (or `workflow_dispatch` → dev)
2. `curl https://func-ticketflow-dev.azurewebsites.net/api/health` → `{"status":"healthy"}`

**Quick cloud connection locally (when needed):**
Change `local.settings.json` to `"CosmosDb__AuthMode": "AzureCli"` + add `"CosmosDb__AccountEndpoint": "https://cosmos-ticketflow-dev.documents.azure.com:443/"`, run `az login`, done.

---

## Decisions

- **`Microsoft.EntityFrameworkCore.Cosmos` over `Microsoft.Azure.Cosmos` directly**: EF Core 10 Cosmos provider wraps the raw SDK and adds LINQ querying, change tracking, and a value-object story that will be needed in later phases (ticket aggregates, domain events). The raw SDK offers no advantage at this stage.
- **`TicketFlowDbContext` in `TicketFlow.Infrastructure`, not `TicketFlow.Functions`**: keeps persistence concerns out of the entry-point project; Functions only knows about `AddDbContext<>` registration, not Cosmos internals.
- **No `EnsureCreatedAsync()` call**: containers are already provisioned by Bicep. For the local emulator the DbContext will create the database/container lazily on first operation, which the health endpoint never triggers — this is intentional so the health check passes even when the emulator is not running.
- **Manual `AddDbContext` DI over `Microsoft.Extensions.Azure`**: `AddAzureClients` doesn't natively support emulator connection strings (it's credential-based), so a custom factory is cleaner and keeps all 3 auth modes in one readable `switch`.
- **Explicit `CosmosDb__AuthMode` key over environment detection**: avoids magic (e.g. checking `AZURE_FUNCTIONS_ENVIRONMENT == "Development"`) and makes the active auth mode auditable in logs at startup.
- **Emulator in Docker over the Windows GUI installer**: cross-platform, no install required, consistent across developer machines.
