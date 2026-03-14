# DurableFunctionsHostFixture — Setup and Rationale

This document explains why `DurableFunctionsHostFixture` exists, how it is wired, and why specific settings are used.

## Purpose

`DurableFunctionsHostFixture` provides a **real durable end-to-end integration test environment** for scenarios that cannot be validated by direct function invocation alone.

It starts:

- Azure Cosmos DB Emulator (for app data)
- Azurite (for Durable Task storage backend)
- A real Azure Functions host process (`func host start`)

This fixture is used by tests in the `DurableIntegrationTests` collection.

---

## Why this fixture is separate from `CosmosDbContainerFixture`

We intentionally keep two fixtures:

- `CosmosDbContainerFixture` for fast integration tests (direct function invocation, no real Functions host)
- `DurableFunctionsHostFixture` for runtime-backed orchestration validation

This prevents all integration tests from paying the startup cost of Azurite + real `func` host while still giving us high-confidence durable workflow coverage.

---

## Lifecycle overview

### `InitializeAsync()`

1. Start Cosmos emulator container
2. Start Azurite container
3. Build Cosmos connection string from the mapped emulator port
4. Build a DI host with `AddCosmosDbModule()` and call `EnsureCosmosDbInitializedAsync()`
5. Pick a free HTTP port for Functions host
6. Start `func host` with required environment variables
7. Create `HttpClient` pointing to that host
8. Poll `/api/health` until host is healthy

### `DisposeAsync()`

1. Dispose `HttpClient`
2. Kill and dispose `func` process (entire process tree)
3. Dispose DI host
4. Dispose Azurite and Cosmos containers

### `ClearDatabaseAsync()`

Used per-test (via test `DisposeAsync`) to remove all events and orders and keep test isolation without restarting infrastructure.

---

## Why each important setting is used

## Cosmos emulator container

- `AZURE_COSMOS_EMULATOR_PARTITION_COUNT=1`
  - Keeps local emulator footprint low and startup simpler for tests.

- `AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=127.0.0.1`
  - Ensures endpoint behavior works correctly in local Docker networking scenarios.

- `.WithPortBinding(8081, true)` and `.WithPortBinding(10250-10255, true)`
  - Enables random host ports to avoid collisions when multiple fixtures or test collections run in parallel.

## Azurite container

- `azurite --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0`
  - Exposes all required Durable storage surfaces (Blob/Queue/Table) to the test host.

- Random host port bindings for `10000/10001/10002`
  - Avoids local port collisions and supports parallelized test execution.

## Functions host startup environment

- `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
  - Matches app runtime model.

- `AzureWebJobsStorage=<constructed Azurite connection string>`
  - Mandatory for Durable runtime state and messaging.

- `CosmosDb__AuthMode=Emulator` + `CosmosDb__ConnectionString=...`
  - Forces the function app under test to use emulator-backed Cosmos, independent of local developer settings.

- `AzureFunctionsJobHost__extensions__durableTask__hubName=tf<random>`
  - Ensures each fixture run gets a dedicated Durable task hub, preventing cross-test contamination.

---

## <u>Workarounds and issues we hit</u>

1. <u>Durable hub name validation failure</u>

   Durable host startup failed when the hub name contained invalid format/length patterns.

   **Fix used:** hub name is generated as `tf` + 20 chars from `Guid.NewGuid().ToString("N")`.

2. <u>Azurite credential mismatch caused host startup failure</u>

   Startup failed with queue/storage auth errors when using an incorrect account key.

   **Fix used:** use Azurite's known development account and key (`devstoreaccount1`).

3. <u>Port collisions with other integration fixture</u>

   Running full integration suite in parallel caused Docker bind errors (especially around Cosmos ports) when host ports were fixed.

   **Fix used:** random host port mapping for Cosmos and Azurite container ports in this fixture.

4. <u>Host readiness race</u>

   Tests attempted API calls before `func host` was fully ready.

   **Fix used:** explicit health polling (`/api/health`) with timeout and diagnostic logs.

5. <u>Debuggability during startup failures</u>

   Startup failures were hard to diagnose without host output.

   **Fix used:** capture stdout/stderr into `_functionsHostLogs` and include logs in thrown exceptions.

6. <u>Rider GUI test run used incompatible .NET SDK for `func`</u>

In Rider, durable integration tests failed with:

- `Azure Functions host exited early`
- `A compatible .NET SDK was not found`
- repository `global.json` requested `10.0.101`, but Rider-inherited environment exposed only `/usr/lib/dotnet` with .NET 9.

**Fix used:** configure Rider test run environment so `func` resolves .NET 10:

- `DOTNET_ROOT=/home/<user>/.dotnet`
- `PATH=/home/<user>/.dotnet:/home/<user>/.dotnet/tools:/usr/local/bin:/usr/bin:$PATH`

Alternative fixes:

- install .NET 10 SDK in system dotnet location, or
- align `global.json` with available SDKs.

---

## Why we run `func host` as a process (instead of in-process bootstrapping)

Durable orchestrations, triggers, and storage wiring are runtime features. Spawning the real host gives confidence that:

- Durable extension wiring is valid
- Trigger bindings resolve correctly
- Orchestration scheduling and execution happen end-to-end

This is intentionally closer to production behavior than direct method invocation tests.

---

## Test usage pattern

Durable E2E tests typically:

1. Arrange data through API calls or seeded state
2. Trigger flow via HTTP endpoint
3. Poll `GET` endpoint until terminal business state (`Confirmed` or `Failed`)
4. Assert final state and reason

This pattern avoids brittle fixed waits and maps to real client behavior.

---

## Troubleshooting checklist

If a durable integration test fails early, check:

1. Docker daemon is running
2. `func --version` is available
3. No local firewall blocks dynamic localhost ports
4. Exception message contains fixture-captured host logs
5. `/api/health` response from host when started manually
6. `dotnet --list-sdks` includes the SDK pinned in repository `global.json`
7. If running from Rider GUI, ensure `DOTNET_ROOT` and `PATH` point to the same SDK installation used in terminal

Manual host verification command:

```bash
func host start --port 7071
```

---

## Related files

- `tests/TicketFlow.Integration.Tests/Fixtures/DurableFunctionsHostFixture.cs`
- `tests/TicketFlow.Integration.Tests/Fixtures/DurableIntegrationTestCollection.cs`
- `tests/TicketFlow.Integration.Tests/Http/CreateOrderWithPayLaterExpiryTests.cs`
- `adr/006-two-integration-test-fixtures.md`
