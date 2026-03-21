# ADR 008 — Phase 5 Ticket Blob Storage Foundation

**Date:** 2026-03-17  
**Status:** Accepted

## Context

Phase 5 requires storing generated ticket artifacts in Blob Storage after order confirmation.
This session focuses only on infrastructure and connectivity groundwork, not on ticket business logic.

The project already uses:

- a shared Storage Account provisioned by Bicep
- system-assigned managed identity for the Function App
- local emulator-driven development with Azurite

## Decision

1. Add a dedicated `tickets` container to the existing Storage Account in IaC.
2. Keep storage RBAC unchanged because Function App identity already receives `Storage Blob Data Owner` at account scope.
3. Introduce a dedicated `TicketStorage` configuration section with explicit auth modes:
   - `Emulator`
   - `AzureCli`
   - `ManagedIdentity`
   - `DefaultAzureCredential`
4. Validate `TicketStorage` configuration on startup to fail fast for invalid local/cloud settings.
5. Provide cloud app settings in Bicep for managed identity:
   - `TicketStorage__AuthMode=ManagedIdentity`
   - `TicketStorage__AccountName=<storage account>`
   - `TicketStorage__Containers__tickets=tickets`
6. Provide local/test settings for emulator/Azure CLI modes in `local.settings*` and integration fixture environment.

## Scope boundaries

### Included

- `tickets` container IaC
- app configuration and credential wiring
- local emulator and cloud-local configuration coverage
- startup validation and unit tests for `TicketStorage` settings

### Excluded

- ticket generation business workflow
- event trigger handling for order-completed
- SAS endpoint and retrieval HTTP API

## Consequences

- Deployments create the required storage container up front.
- Cloud runtime can access ticket storage without secrets via managed identity.
- Local and integration environments have explicit, validated Blob configuration paths.
- Phase 5 business work can now focus on ticket artifact behavior rather than connectivity setup.
