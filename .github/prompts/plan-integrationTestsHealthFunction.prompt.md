## Plan: Integration Tests for Health Function

The integration tests will verify the health function against a **real CosmosDB emulator** managed by Testcontainers (no need for pre-running docker-compose). Rather than fighting Azure Functions isolated worker with `WebApplicationFactory` (complex, brittle), we'll instantiate `HealthFunction` directly from a real `IServiceProvider` — the "integration" is in the real database, real EF Core context, and real DI wiring that matches production.

**Steps**

1. **Add packages to [Directory.Packages.props](Directory.Packages.props)**
   - `Testcontainers.CosmosDb` (latest, 4.10.0) — Testcontainers module that wraps the CosmosDB Linux emulator image
   - `Microsoft.Extensions.Hosting` (10.0.x) — to build an `IHost` in tests (may already be transitive)

2. **Update [tests/TicketFlow.Integration.Tests/TicketFlow.Integration.Tests.csproj](tests/TicketFlow.Integration.Tests/TicketFlow.Integration.Tests.csproj)**
   - Add `<PackageReference>` for: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `Shouldly`, `Testcontainers.CosmosDb`
   - Add `<IsPackable>false</IsPackable>` and ensure `<Nullable>enable</Nullable>` / `<ImplicitUsings>enable</ImplicitUsings>` (likely inherited from [Directory.Build.props](Directory.Build.props))

3. **Create `tests/TicketFlow.Integration.Tests/Fixtures/CosmosDbContainerFixture.cs`**
   - Implements `IAsyncLifetime`
   - Starts a `CosmosDbContainer` using Testcontainers (uses the same `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator` image)
   - Builds an `IServiceProvider` using `AddCosmosDbModule()` with config pointing to the running container's connection string (overriding `CosmosDb__AuthMode=Emulator` and `CosmosDb__ConnectionString`)
   - Calls `EnsureCosmosDbInitializedAsync()` (or directly `dbContext.Database.EnsureCreatedAsync()`) to create the container/database
   - Exposes the `IServiceProvider` for tests

4. **Create `tests/TicketFlow.Integration.Tests/Fixtures/IntegrationTestCollection.cs`**
   - An xunit `[CollectionDefinition]` + `ICollectionFixture<CosmosDbContainerFixture>` so the container is shared across all integration test classes (avoids restarting it per class)

5. **Create `tests/TicketFlow.Integration.Tests/Http/HealthFunctionTests.cs`**
   - `[Collection("IntegrationTests")]` + `[Trait("Category", "Integration")]`
   - Constructor receives `CosmosDbContainerFixture` — resolves `HealthFunction` and `ILogger<HealthFunction>` from the fixture's service provider
   - Test: `GET /api/health` when CosmosDB is reachable → response has `status = "Healthy"` (asserted with Shouldly)
   - Calls `Run()` with a `DefaultHttpContext().Request` (from `Microsoft.AspNetCore.Http`, already available via transitive refs)

6. **Update [.github/workflows/ci.yml](.github/workflows/ci.yml)**
   - Add new `integration-tests` job:
     - `needs: build-and-test`
     - `runs-on: ubuntu-latest` (Docker daemon available by default on GitHub-hosted runners — Testcontainers uses it directly, no `services:` block needed)
     - Steps: checkout → setup .NET → restore → build → `dotnet test --filter "Category=Integration" --configuration Release`
   - Change `deploy` job to `needs: [build-and-test, integration-tests]`

**Verification**

- Locally: `dotnet test tests/TicketFlow.Integration.Tests --filter "Category=Integration"` (requires Docker running)
- CI: push a PR and confirm the new `integration-tests` job runs and the `deploy` job waits for it

**Decisions**

- Chose direct `HealthFunction` instantiation over `WebApplicationFactory`: Azure Functions isolated worker host requires the real Functions runtime infrastructure to route HTTP — not worth the complexity for a function whose integration value is in the DB interaction, not the HTTP layer
- Chose shared `ICollectionFixture` over `IClassFixture`: container startup (~30s for CosmosDB emulator) is expensive; sharing across test classes keeps CI fast as more tests are added
- Testcontainers manages the container lifecycle — no pre-running `docker-compose` required in CI
