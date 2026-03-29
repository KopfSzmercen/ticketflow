# ADR 010 — Waitlist Money Serialization Strategy for Cosmos DB

**Date:** 2026-03-29  
**Status:** Accepted

## Context

The waitlist flow now stores offered ticket price as the `Money` value object on `WaitlistEntry`.

When mapped as a nullable complex property in EF Core Cosmos, integration tests showed incorrect round-trip behavior for `Money.Amount` in waitlist entries (persisted value `49` materialized as `0`).

Observed constraints:

- `TicketEvent.TicketPrice` and `Order.TicketPrice` complex mappings continue to work as expected.
- `WaitlistEntry.OfferedTicketPrice` is optional and frequently null.
- The issue was reproducible in integration tests that exercise waitlist offer persistence and retrieval.

## Decision

1. Keep `Money` as a record-based value object for domain value semantics.
2. Keep `WaitlistEntry.OfferedTicketPrice` as `Money?` in the domain model.
3. Map only `WaitlistEntry.OfferedTicketPrice` with explicit JSON conversion in `TicketFlowDbContext`.
4. Keep existing complex-property mappings for `TicketEvent.TicketPrice` and `Order.TicketPrice` unchanged.
5. Preserve integration tests that validate offered waitlist price round-trip correctness.

## Why this option

- It fixes the observed Cosmos round-trip issue for the nullable waitlist money property.
- It avoids introducing primitive amount/currency fields back into the domain model.
- It limits the workaround to one problematic property instead of changing all money mappings.
- It keeps the implementation simpler than a larger custom comparer/converter stack.

## Trade-offs

- Waitlist money persistence uses a different EF mapping style than event/order money.
- JSON conversion on a single property adds a small amount of mapping inconsistency.
- Future EF Core Cosmos improvements may allow revisiting this decision.

## Consequences

- Waitlist offered price now round-trips correctly in integration tests.
- Domain code continues to use `Money` instead of primitive price fields.
- `TicketFlowDbContext` contains a targeted conversion rule for `WaitlistEntry.OfferedTicketPrice`.
- A future cleanup can remove this conversion if EF Core Cosmos fixes nullable complex-property materialization for this case.
