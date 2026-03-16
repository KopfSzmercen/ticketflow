# ADR 007 — Phase 4 Service Bus Readiness (Prep-only)

**Date:** 2026-03-14  
**Status:** Accepted

## Context

Phase 4 introduces Azure Service Bus into the architecture. We need local and cloud-local developer readiness before wiring business flow publishing/consuming into the durable process.

Without an explicit prep phase, teams risk coupling business logic changes with infrastructure/connectivity issues, making failures harder to isolate.

## Decision

For Phase 4, implement **readiness only**:

1. Standardize subscriber naming on `analytics-worker`.
2. Support `ServiceBusAuthMode` values:
   - `Emulator`
   - `AzureCli`
   - `ManagedIdentity`
   - `DefaultAzureCredential`
3. Run the official local emulator stack shape:
   - Service Bus emulator container
   - SQL Edge sidecar dependency
4. Add IaC and app-setting wiring for namespace/topic/subscriptions and managed-identity RBAC.
5. Add publish/consume seams plus harness tests that validate invocation contracts and invocability, **without** requiring full broker-delivery assertions.

## Scope boundaries

### Included

- Configuration model + DI registration
- Local settings templates
- Docker compose readiness for Service Bus emulator
- Bicep modules and role assignments
- Minimal producer/consumer abstractions and no-op/test doubles

### Excluded

- Durable orchestrator-side publish calls
- Worker trigger business implementations (`email-worker`, `analytics-worker`)
- End-to-end broker delivery assertions in integration tests

## Consequences

- Local runs can validate setup and contracts quickly.
- Azure deployments include Service Bus and least-privilege runtime permissions.
- Later phases can wire business flow with reduced risk because auth, configuration, and infrastructure are already verified.
