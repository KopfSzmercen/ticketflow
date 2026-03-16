## Plan: Phase 4 Service Bus Readiness (Emulator + Azure)

Updated DRAFT — Include a Cosmos-style `Emulator` auth mode for Service Bus and run the official local emulator stack from Microsoft docs (Service Bus emulator + required SQL Edge sidecar). Keep scope minimal: infra/module setup, app configuration and DI seams, and integration harness prep only. No business-flow wiring in orchestrator yet. Standardize naming on `analytics-worker`, and keep tests split so local runs can validate contracts/invocability without requiring a full broker roundtrip assertion.

**Steps**

1. Align phase text and naming in [building-phases/4.md](building-phases/4.md) and add an ADR in [adr](adr) documenting: `analytics-worker`, `ServiceBusAuthMode` (`Emulator`, `AzureCli`, `ManagedIdentity`, `DefaultAzureCredential`), and prep-only scope.
2. Add emulator stack to [docker-compose.yml](docker-compose.yml) following Azure docs/installer guidance: Service Bus emulator container plus SQL Edge dependency; include local ports and startup notes in [README.md](README.md).
3. Extend local settings templates in [src/TicketFlow.Functions/local.settings.example.json](src/TicketFlow.Functions/local.settings.example.json) and [src/TicketFlow.Functions/local.settings.azurecli.example.json](src/TicketFlow.Functions/local.settings.azurecli.example.json) with Service Bus config keys for emulator and cloud-local modes.
4. Create Service Bus options/auth model in [src/TicketFlow.Infrastructure/ServiceBus](src/TicketFlow.Infrastructure/ServiceBus) mirroring Cosmos module structure (`SectionName`, options validator, auth-mode switch, client factory).
5. Register Service Bus module in [src/TicketFlow.Functions/Program.cs](src/TicketFlow.Functions/Program.cs) and add required packages in [Directory.Packages.props](Directory.Packages.props), [src/TicketFlow.Infrastructure/TicketFlow.Infrastructure.csproj](src/TicketFlow.Infrastructure/TicketFlow.Infrastructure.csproj), and [src/TicketFlow.Functions/TicketFlow.Functions.csproj](src/TicketFlow.Functions/TicketFlow.Functions.csproj).
6. Add Azure Service Bus IaC as a new module under [infra/modules](infra/modules) (namespace, topic `order-events`, subscriptions `email-worker` + `analytics-worker`), wire through [infra/main.bicep](infra/main.bicep), and parameterize in [infra/parameters/dev.bicepparam](infra/parameters/dev.bicepparam).
7. Extend [infra/modules/functions.bicep](infra/modules/functions.bicep) with Service Bus app settings and [infra/modules/roleAssignments.bicep](infra/modules/roleAssignments.bicep) with MI RBAC for send/receive permissions.
8. Add minimal producer/consumer seams (interfaces + no-op/test doubles) in [src/TicketFlow.Infrastructure/ServiceBus](src/TicketFlow.Infrastructure/ServiceBus) and test harness scaffolding in [tests/TicketFlow.Integration.Tests](tests/TicketFlow.Integration.Tests) to verify publish-contract invocation and consumer invocability without asserting end-to-end broker delivery.

**Verification**

- IaC: `az bicep build --file infra/main.bicep` and `az deployment sub what-if --location polandcentral --template-file infra/main.bicep --parameters infra/parameters/dev.bicepparam`.
- Local emulator readiness: `docker compose up -d` starts Azurite, Cosmos emulator, Service Bus emulator, and SQL Edge.
- Build/tests: `dotnet build TicketFlow.sln` and targeted integration tests for Service Bus harness category.
- Config sanity: emulator connection string includes `UseDevelopmentEmulator=true`; admin-management path uses emulator management port where required.

**Decisions**

- Subscriber naming: `analytics-worker`.
- Local mode: include Service Bus emulator stack (per official docs) plus `Emulator` auth mode.
- Durable pattern: future publish path remains activity/interface seam, not direct orchestrator-side client calls.
