## Plan: QR ticket blob storage

Build phase 5 as an event-driven ticket pipeline. When an order reaches Confirmed, the existing `OrderCompletedEvent` should trigger a separate worker that generates a QR-backed ticket artifact, uploads it to the existing Storage Account in a dedicated `tickets` container, and exposes retrieval via SAS URL as the second half of the phase. This keeps the order saga clean and matches the project’s Service Bus worker pattern.

**Steps**

1. Confirm the ticket contract and storage shape: one blob per confirmed order, with a predictable name and enough metadata or persisted linkage to resolve it later. _Depends on the confirmed-order event already being published._
2. Add the `tickets` container to [infra/modules/storage.bicep](infra/modules/storage.bicep) and verify no extra RBAC is needed because blob data owner access is already assigned in [infra/modules/roleAssignments.bicep](infra/modules/roleAssignments.bicep).
3. Add a Service Bus-driven ticket worker under [src/TicketFlow.Functions/Triggers/](src/TicketFlow.Functions/Triggers/) that subscribes to order-confirmed events and invokes the ticket-generation logic.
4. Add the ticket-generation implementation in [src/TicketFlow.Functions/Activities/](src/TicketFlow.Functions/Activities/) or a dedicated helper/service used by the worker. It should create the QR payload, render the ticket artifact, and upload it to Blob Storage.
5. Add ticket lookup/retrieval support as the second half of phase 5: an HTTP endpoint that resolves the order’s ticket blob and returns a SAS download URL. If this is intentionally deferred, keep the upload logic isolated so the retrieval endpoint can be added without reworking the artifact model.
6. Add tests in [tests/TicketFlow.Integration.Tests/](tests/TicketFlow.Integration.Tests/) that prove the event triggers blob creation for confirmed orders and, if included in scope, that the SAS URL can be generated and used against Azurite.

**Relevant files**

- [building-phases/5.md](building-phases/5.md) — phase 5 acceptance criteria and scope
- [infra/modules/storage.bicep](infra/modules/storage.bicep) — add the `tickets` container
- [infra/modules/roleAssignments.bicep](infra/modules/roleAssignments.bicep) — confirm existing blob permissions are sufficient
- [src/TicketFlow.Functions/Activities/UpdateOrderStatusActivity.cs](src/TicketFlow.Functions/Activities/UpdateOrderStatusActivity.cs) — confirms when the order-completed event is published
- [src/TicketFlow.Functions/Triggers/](src/TicketFlow.Functions/Triggers/) — place the Service Bus ticket worker here
- [src/TicketFlow.Functions/Activities/](src/TicketFlow.Functions/Activities/) — place the QR/PDF generation and upload logic
- [src/TicketFlow.Core/Models/Order.cs](src/TicketFlow.Core/Models/Order.cs) — likely place to persist ticket/blob linkage, unless a separate ticket record is preferred
- [tests/TicketFlow.Integration.Tests/](tests/TicketFlow.Integration.Tests/) — blob upload and retrieval coverage

**Verification**

1. Confirm the `tickets` container exists in deployed or emulated storage.
2. Run the confirmed-order event flow and verify a blob is created only after the order reaches Confirmed.
3. If SAS retrieval is in scope, verify the generated URL can download the uploaded ticket from Azurite and from the local Azure-hosted setup.
4. Run the existing integration suite plus any new blob-focused tests to ensure the new worker does not break the order saga.

**Decisions**

- Keep this feature narrowly focused on ticket artifact generation and storage; do not add UI, auth, or seating logic.
- Use the existing Service Bus event flow instead of coupling ticket generation to `PlaceOrderOrchestrator`.
- Reuse the existing Storage Account and managed identity pattern already established for the project.
- Favor a single ticket artifact per confirmed order rather than splitting QR generation and storage into unrelated flows.
- If SAS retrieval is deferred, document that as an explicit follow-up rather than leaving the blob unreachable.

**Further Considerations**

1. Should the ticket artifact be a PDF, a QR image, or both? The phase file currently points to PDF, but your note suggests splitting the work.
2. Should the order record store the blob name, a ticket id, or both? Pick one source of truth before implementation.
3. Should the SAS URL be generated on demand only, or also cached/stored with the order? On-demand is usually simpler and safer.
