# ADR 010 — Money Serialization Strategy for Cosmos DB

**Date:** 2026-03-29  
**Status:** Accepted

## Context

The domain uses the `Money` value object for ticket pricing in `TicketEvent`, `Order`, and the optional `WaitlistEntry.OfferedTicketPrice`.

When mapped as a complex property in EF Core Cosmos, integration tests showed constructor materialization failures for `Money` during queries and cleanup. The Cosmos provider attempted to hydrate `Money` through a path that could not reliably supply both constructor arguments.

Observed constraints:

- `Money` is an immutable record with constructor validation.
- The Cosmos provider's complex-property support is still incomplete and has known gaps around binding and materialization.
- The issue was reproducible in integration tests that query `TicketEvent`, `Order`, and `WaitlistEntry`.

## Decision

1. Keep `Money` as a record-based value object for domain value semantics.
2. Keep `TicketEvent.TicketPrice`, `Order.TicketPrice`, and `WaitlistEntry.OfferedTicketPrice` as `Money`-typed properties in the domain model.
3. Map all Cosmos-backed `Money` properties with explicit JSON conversion in `TicketFlowDbContext`.
4. Avoid Cosmos complex-property mapping for `Money` until provider support is mature enough to round-trip immutable value objects reliably.
5. Preserve integration tests that validate money round-trip behavior across event creation, order creation, and waitlist offers.

## Why this option

- It fixes the observed Cosmos materialization issue for all `Money` properties used by the app.
- It avoids introducing primitive amount/currency fields back into the domain model.
- It keeps the implementation simpler than a larger custom comparer/converter stack.
- It uses one consistent persistence strategy rather than mixing complex-property and JSON conversions.

## Trade-offs

- Cosmos persistence for `Money` now uses JSON conversion everywhere, which is less idiomatic than native complex-property mapping.
- Future EF Core Cosmos improvements may allow revisiting this decision and restoring complex-property mapping.

## Consequences

- Ticket prices and waitlist offered prices now round-trip correctly in integration tests.
- Domain code continues to use `Money` instead of primitive price fields.
- `TicketFlowDbContext` contains shared conversion rules for Cosmos-backed `Money` properties.
- A future cleanup can remove these conversions if EF Core Cosmos fixes immutable complex-type materialization for this case.
