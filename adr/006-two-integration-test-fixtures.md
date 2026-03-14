# ADR 006 — Two Integration Test Collection Fixtures

**Date:** 2026-03-14  
**Status:** Accepted

## Context

Integration testing in TicketFlow now covers two distinct needs:

1. **Fast function-level integration tests** that validate HTTP function behavior and
   Cosmos persistence while keeping Durable runtime out of process.
2. **End-to-end Durable workflow tests** that validate real orchestration execution
   (`func host`, Durable storage backend, activities, timers, and HTTP polling).

Trying to force both needs into one fixture created trade-offs:

- A single heavyweight fixture slows every integration test.
- A single lightweight fixture cannot validate real Durable runtime behavior.
- Runtime-backed tests require additional infrastructure (Functions host process,
  Azurite, Durable hub isolation, readiness/polling logic) that is unnecessary for
  fast contract tests.

## Decision

Use **two separate xUnit collection fixtures**:

- `IntegrationTests` → `CosmosDbContainerFixture`
- `DurableIntegrationTests` → `DurableFunctionsHostFixture`

The fixture split is intentional and permanent unless a future architecture change
eliminates the need for either fast or runtime-backed coverage.

## Reasons

### 1. Preserve fast feedback loop

Most tests only need Cosmos + DI and can run quickly with direct function invocation.
Keeping them on `CosmosDbContainerFixture` avoids unnecessary host startup cost.

### 2. Add true orchestration confidence

Durable-specific behavior (scheduler wiring, activity dispatch, timers, runtime state
transitions, host configuration) is only validated when the Functions host and Durable
backend run for real. `DurableFunctionsHostFixture` provides that environment.

### 3. Keep infrastructure concerns isolated

Runtime-backed tests have dedicated concerns (process lifecycle, health checks,
connection strings, hub naming, container ports). Isolating them in a separate fixture
contains complexity and reduces blast radius for standard integration tests.

### 4. Support deterministic timer-path testing

Durable tests use short expiration windows and polling to assert terminal order state,
instead of broad `Task.Delay` usage in lightweight tests.

## Consequences

- Integration suite now has two categories of tests with different execution cost.
- Developers can run fast tests by default and durable end-to-end tests when needed.
- CI can run both categories for full confidence, while still preserving local speed.
- Fixture-level container/runtime setup is no longer duplicated per test class.

## Alternatives considered

| Option                                      | Rejected because                                                       |
| ------------------------------------------- | ---------------------------------------------------------------------- |
| Single lightweight fixture only             | Cannot verify real Durable orchestration behavior.                     |
| Single heavyweight fixture for all tests    | Makes all integration tests slower and more brittle.                   |
| No integration coverage for Durable runtime | Misses regressions in host/runtime wiring and orchestration execution. |
