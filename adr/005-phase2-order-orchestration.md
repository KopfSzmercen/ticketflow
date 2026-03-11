# ADR 005 — Phase 2 Order Orchestration

**Date:** 2026-03-10  
**Status:** Accepted

## Context

Phase 2 introduces the purchase flow: a user submits a single-ticket order, the system
reserves inventory, simulates payment, and either confirms or cancels the order and
releases the reserved ticket. The flow is inherently long-running (network calls,
potential retries, compensation) so it is coordinated via Azure Durable Functions.

Before implementing the workflow two design decisions had to be resolved:

1. **When does the `Order` resource first exist?**  
   Option A — create Order after the orchestration starts (orchestration-first).  
   Option B — create Order before the orchestration starts (persistence-first).

2. **How is the resource identifier related to the Durable instance id?**

## Decisions

### D1 — Create `Order` before starting the workflow (persistence-first)

`POST /orders` persists the `Order` document in Cosmos DB with status `Pending`
_before_ calling `DurableTaskClient.ScheduleNewOrchestrationInstanceAsync`.

**Rationale:**

- The resource exists immediately; `GET /orders/{orderId}` is valid the instant the
  `202 Accepted` is returned, with no race window.
- The Cosmos document is the authoritative business record at all times—event if the
  Durable host restarts or history is purged the Order state is not lost.
- Simplifies polling: the client always reads from Cosmos, not from the Durable
  status endpoint.
- Matches REST semantics: `202 Accepted` implies "the resource has been created; its
  processing is ongoing".

### D2 — Use the same value for `orderId` and Durable instance id

`Guid.NewGuid().ToString()` is generated once in the HTTP function; it is used as
`order.Id` (partition key in Cosmos) and passed as `instanceId` to
`ScheduleNewOrchestrationInstanceAsync`.

**Rationale:**

- Eliminates any mapping table; a single identifier traces through Cosmos, Durable
  history, and logs.
- Makes the supplemental orchestration metadata on `GET /orders/{orderId}` trivial
  to retrieve: `DurableTaskClient.GetInstanceAsync(orderId)`.

### D3 — Cosmos is the business source of truth

`Order.Status` in Cosmos drives the business domain state. The Durable orchestration
updates this field via activities (`UpdateOrderStatusActivity`). `GET /orders/{orderId}`
reads Cosmos first; orchestration metadata (runtime status, timestamps) is appended as
supplemental, non-required debug information.

### D4 — `POST /orders` returns `202 Accepted`

The HTTP response includes:

- `orderId` — the new identifier.
- `Location` header pointing to `GET /orders/{orderId}`.

The response body carries the initial `OrderResponse` so clients do not need an extra
GET to know the order was created.

### D5 — Single-ticket purchases only in Phase 2

Each order covers exactly one ticket. Multi-ticket orders, waitlists, group bookings,
and quotas are deferred.

### D6 — Simulated payment result is request-controlled

`POST /orders` body accepts `simulatePaymentSuccess: bool`. This controls the outcome
of `ProcessPaymentActivity`, enabling deterministic integration and scenario tests
without an external payment gateway.

### D7 — Inventory field naming

`TicketEvent.TotalCapacity` is the total number of tickets created for the event.  
`TicketEvent.AvailableTickets` is the current remaining inventory, decremented by
`ReserveTicketActivity` and restored by `ReleaseTicketActivity`.

The redundant `Capacity` field has been removed; it duplicated `TotalCapacity` without
adding semantic value.

### D8 — `/id` as the `orders` partition key

Consistent with the existing `events` container. Each order lives in its own logical
partition. No cross-partition queries in Phase 2.

### D9 — Endpoints remain anonymous

`POST /orders` and `GET /orders/{orderId}` use `AuthorizationLevel.Anonymous`,
matching the existing events endpoints. Authentication/authorization is deferred.

### D10 — Release-on-failure included in Phase 2

When payment fails (or reservation fails), `ReleaseTicketActivity` increments
`AvailableTickets` back to its pre-reservation value, and the order is marked `Failed`.
This minimal compensation is implemented now because it is required for correctness.
Broader compensation and saga patterns are deferred to later phases.

### D11 — Durable workflow isolated-worker style

The Durable orchestrator and activities follow the .NET isolated worker programming
model. The DurableTask worker is registered via `ConfigureDurableTask` in `Program.cs`.
Orchestrators go in `Orchestrators/`, activities in `Activities/`.

## Workflow summary

```
POST /orders
  │
  ├─ Create Order (Pending) → Cosmos
  ├─ ScheduleNewOrchestrationInstance(orderId)
  └─ 202 Accepted {orderId, Location}

PlaceOrderOrchestrator (instanceId = orderId)
  │
  ├─ UpdateOrderStatus (Reserving)
  ├─ ReserveTicketActivity          → load event, decrement AvailableTickets (_etag)
  │   ├─ success → continue
  │   └─ failure → UpdateOrderStatus (Failed), end
  ├─ UpdateOrderStatus (Paying)
  ├─ ProcessPaymentActivity         → simulated outcome from input
  │   ├─ success → UpdateOrderStatus (Confirmed)
  │   └─ failure → ReleaseTicketActivity, UpdateOrderStatus (Failed)
  └─ end

GET /orders/{orderId}
  └─ Read Order from Cosmos (primary)
  └─ Read orchestration metadata (supplemental, optional)
```

## Features deferred to later phases

- Waitlist and oversell prevention beyond \_etag
- Timer-based reservation expiry
- Blob PDF generation
- Service Bus downstream dispatch
- Fraud detection fan-out
- Full runtime-level Durable integration test coverage
