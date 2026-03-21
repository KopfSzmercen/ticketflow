# ADR 009 — Workspace-based Application Insights with Minimal-Cost Telemetry

**Date:** 2026-03-21  
**Status:** Accepted

## Context

The Function App currently has no centralized Azure Monitor telemetry sink in infrastructure.
We need enough operational visibility for invocations and failures while keeping cost predictable in a learning project environment.

Existing architecture constraints:

- Managed identity remains the default authentication model for service access (Storage, Cosmos DB, Service Bus).
- Telemetry ingestion can use app settings (connection string) without introducing customer-managed secrets.
- Environment-specific behavior must remain configurable through Bicep parameters.

## Decision

1. Add a dedicated monitoring module that provisions:
   - Log Analytics workspace (`log-<app>-<env>`) with configurable retention.
   - Workspace-based Application Insights (`appi-<app>-<env>`) linked to that workspace.
2. Configure Application Insights with strict cost controls:
   - 30-day retention target.
   - Daily cap configured per environment.
   - Host-level adaptive sampling enabled by default.
3. Pass the Application Insights connection string into the Function App via app settings:
   - `APPLICATIONINSIGHTS_CONNECTION_STRING`
4. Keep managed identity architecture unchanged for business dependencies.
5. Introduce parameterized telemetry profile controls:
   - `minimal` (default for dev): warning+ noise floor, dependency tracking off, aggressive sampling bounds.
   - `balanced` (optional for stage/prod): broader informational capture with less aggressive bounds.

## Why this option

- Workspace-based Application Insights is the recommended modern integration path and consolidates analytics in Log Analytics.
- Daily cap plus adaptive sampling creates bounded ingestion spend during noisy bursts.
- A profile switch allows better incident diagnostics in higher environments without changing code.

## Trade-offs

- Adaptive sampling can drop low-severity/duplicate events, reducing forensic fidelity.
- Daily cap can truncate telemetry late in the day during spikes.
- Minimal profile may hide useful informational traces when troubleshooting complex orchestration behavior.

## Cost behavior expectations

- Dev baseline targets low and predictable ingestion by combining:
  - 10% initial adaptive sampling (min 5%, max 20%).
  - Warning-level defaults for broad SDK categories.
  - Dependency tracking disabled in minimal mode.
  - 1 GB/day default cap in dev parameters.
- Stage/prod can choose between strict minimal mode and balanced visibility by environment parameter only.

## Consequences

- New monitoring resources are created in each environment deployment.
- Function App gets AI ingestion without introducing credential sprawl.
- Operations can tune telemetry spend by modifying Bicep parameters, not by code changes.
