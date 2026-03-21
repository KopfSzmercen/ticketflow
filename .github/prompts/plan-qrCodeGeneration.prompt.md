## Plan: Phase 5 QR Ticket Generation

Implement a minimal, event-driven QR ticket flow by adding a dedicated `qr-worker` Service Bus subscription and trigger that consumes `OrderCompletedEvent`, generates a PNG QR code via `QRCoder` with payload `OrderId + EventId + AttendeeEmail`, and writes it to Blob Storage at `tickets/{orderId}.png`. Keep the trigger invocable directly from tests (no broker mock, no host-level Service Bus publish), and validate with an integration happy path that asserts blob existence/content in real Azurite-backed storage.

**Steps**
1. Phase A: Event contract and infrastructure alignment
2. Add `QrSubscriptionName` to Service Bus options/configuration surface so functions can bind to a dedicated subscription. *blocks step 3*
3. Extend IaC and environment wiring for `qr-worker` subscription in `order-events` topic (Bicep outputs, app settings, local settings templates, durable fixture env vars). *depends on 2; parallel with 4 where possible*
4. Confirm/standardize `OrderCompletedEvent` payload semantics for QR v1 (`OrderId`, `EventName` as event identifier, `CustomerEmail`) and document that `EventName` currently carries event id. *parallel with 3; blocks 5 if contract changes*

5. Phase B: QR generation flow implementation
6. Add `QRCoder` package to `src/TicketFlow.Functions/TicketFlow.Functions.csproj` and introduce a small QR generation service abstraction in Functions layer (or trigger-local helper) that returns PNG bytes from payload text. *blocks 7*
7. Add new Service Bus trigger `QrWorkerTrigger` under `src/TicketFlow.Functions/Triggers/` bound to `%ServiceBus:TopicName%` + `%ServiceBus:QrSubscriptionName%`. In `Run(...)`, deserialize `OrderCompletedEvent`, build deterministic payload string from agreed fields, generate PNG, and upload with `IFileStorage.UploadAsync(BlobContainerAliases.Tickets, "tickets/{orderId}.png", ..., "image/png")`. Keep logic in methods that can be called directly from integration tests. *depends on 6 and 3*
8. Add defensive behavior: log and return on invalid/empty payload, preserve exception flow for transient failures so Service Bus retry/dead-letter semantics remain effective. *depends on 7*

9. Phase C: Integration happy path (direct trigger invocation)
10. Create/extend integration fixture to include Azurite alongside Cosmos for non-host direct-invocation tests (recommended: augment `CosmosDbContainerFixture` or add sibling fixture that shares current `IntegrationTests` style but injects `TicketStorage` emulator settings). *blocks 11*
11. Add `QrWorkerTriggerTests` in `tests/TicketFlow.Integration.Tests/Triggers/` using direct invocation pattern: seed any required state, build a real `ServiceBusReceivedMessage` carrying serialized `OrderCompletedEvent`, instantiate trigger from DI scope, invoke `Run(...)` directly, then verify blob write at `tickets/{orderId}.png` using `IFileStorage.OpenReadAsync(...)` or Azure SDK container client. *depends on 10 and 7*
12. Assert happy-path specifics: file exists, non-empty bytes, PNG signature/content-type expectation, and stable blob naming for the provided order id. *depends on 11*

13. Phase D: Optional Phase 5 endpoint completion
14. Implement `GET /orders/{orderId}/ticket` returning read SAS URL via `IFileStorage.GetReadSasUriAsync(...)` for `tickets/{orderId}.png` with bounded TTL and 404 semantics when blob missing. *depends on 7; can proceed in parallel with 10/11 if team split*
15. Add integration test for ticket URL endpoint (direct function invocation style used in current HTTP tests), validating status code and returned URI shape. *depends on 14*

16. Phase E: Validation and documentation
17. Run targeted unit/integration tests and verify no regression in existing Service Bus consumers (`email-worker`, `analytics-worker`). *depends on 8, 12, 15*
18. Update `building-phases/completed-phases.md` and add/update ADR note if contract naming (`EventName` as event id) remains intentionally temporary. *depends on 17*

**Relevant files**
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/Triggers/EmailWorkerTrigger.cs` — existing Service Bus trigger binding and deserialization style to mirror
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/Triggers/AnalyticsWorkerTrigger.cs` — alternate trigger signature pattern (`string` payload)
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/DTO/OrderCompletedEvent.cs` — consumed event contract used for QR payload composition
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/Activities/ServiceBusOrderCompletedEventPublisher.cs` — publisher source for message shape and topic routing
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/Program.cs` — DI registrations for trigger dependencies and new QR service if added
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/TicketFlow.Functions.csproj` — add `QRCoder` dependency
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Infrastructure/BlobStorage/IFileStorage.cs` — upload/read APIs used by trigger and tests
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Infrastructure/BlobStorage/BlobContainerAliases.cs` — use `Tickets` alias constant
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Infrastructure/ServiceBus/ServiceBusOptions.cs` — add `QrSubscriptionName` config property
- `/home/komp/Learning/TicketFlow/infra/modules/serviceBus.bicep` — add `qr-worker` subscription resource/output
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/local.settings.example.json` — add local config key for QR subscription
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/local.settings.json` — add local config key for QR subscription
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/local.settings.azurecli.example.json` — keep cloud-local template aligned
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/local.settings.azurecli.json` — keep dev profile aligned
- `/home/komp/Learning/TicketFlow/tests/TicketFlow.Integration.Tests/Fixtures/CosmosDbContainerFixture.cs` — candidate fixture to extend with Azurite for blob assertions
- `/home/komp/Learning/TicketFlow/tests/TicketFlow.Integration.Tests/Fixtures/DurableFunctionsHostFixture.cs` — reference for Azurite env var wiring in tests
- `/home/komp/Learning/TicketFlow/tests/TicketFlow.Integration.Tests/Activities/UpdateOrderStatusActivityTests.cs` — direct invocation + scoped arrange/act/assert pattern to replicate
- `/home/komp/Learning/TicketFlow/tests/TicketFlow.Integration.Tests/Triggers/QrWorkerTriggerTests.cs` — new happy-path direct-trigger integration test
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/Http/GetOrderTicketFunction.cs` — new endpoint for SAS URL retrieval (if included scope)
- `/home/komp/Learning/TicketFlow/tests/TicketFlow.Integration.Tests/Http/GetOrderTicketFunctionTests.cs` — endpoint integration test (if included scope)

**Verification**
1. Build functions project and run full unit test suite to catch package/config regressions.
2. Run integration tests for triggers and blob storage path; ensure `QrWorkerTriggerTests` passes against real Azurite.
3. Run durable/E2E integration subset to ensure existing order completion still publishes and existing subscribers still bind.
4. Manually validate local configuration boot: functions host starts with new `ServiceBus:QrSubscriptionName` and `TicketStorage` settings.
5. Optionally inspect Azurite container contents after test run to confirm `tickets/{orderId}.png` artifact path and MIME behavior.

**Decisions**
- Include scope for both minimal QR generation + blob write and `GET /orders/{orderId}/ticket` endpoint.
- QR payload v1: `OrderId + EventId + AttendeeEmail`.
- Blob path v1: `tickets/{orderId}.png`.
- Use dedicated `qr-worker` Service Bus subscription (fan-out from `order-events`).
- Integration happy path must directly invoke trigger from code with crafted `ServiceBusReceivedMessage` (no broker mock and no broker publish assertion).
- Fixture strategy: extend/add non-host integration fixture to include Azurite so blob writes are real and assertable.

**Further Considerations**
1. `OrderCompletedEvent.EventName` appears to carry `EventId`; decide whether to keep this semantic mismatch for now or rename in a backward-compatible follow-up.
2. Confirm whether QR payload should remain plain text concatenation for learning simplicity or use a versioned JSON payload to allow future scanner evolution.
3. Decide retention/lifecycle policy for `tickets` container artifacts (out of immediate scope but relevant for later phases).
