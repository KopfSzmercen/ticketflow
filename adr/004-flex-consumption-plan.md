# ADR-004: Use Flex Consumption Plan for Azure Functions

**Date:** 2026-03-07  
**Status:** Accepted

---

## Context

Azure Functions offers several hosting plan options, each with different cost models, scaling behaviours, and cold-start characteristics:

- **Consumption (Y1/Dynamic)** — the original serverless plan. Scales to zero; billing is per-execution. Microsoft has signalled that this SKU is on a deprecation path in favour of Flex Consumption and will not receive new feature investment.

- **Flex Consumption (FC1)** — the successor serverless plan introduced in 2024. Retains the scale-to-zero / pay-per-use model of Consumption while adding per-instance memory configuration, faster scale-out, and virtual network integration. Requires `functionAppConfig` on site creation and a dedicated blob container for deployment packages.

- **Premium (EP1–EP3)** — no cold starts (pre-warmed instances), VNet integration, and unlimited execution duration. Significantly more expensive; instances are always billed.

- **Dedicated (App Service Plan)** — full VM, always on, predictable cost. Overkill for any event-driven or low-traffic workload.

TicketFlow is a learning project. The Functions workload consists of HTTP triggers and Durable orchestrations exercised via manual test traffic. There is no SLA requirement, and the cost must be as low as possible.

---

## Decision

Use the **Flex Consumption (FC1)** plan for all Azure Functions deployments in TicketFlow.

---

## Reasons

### 1. Consumption (Y1/Dynamic) is being superseded

Microsoft has publicly indicated that the original Dynamic SKU will not receive future platform improvements and is on a long-term deprecation path. Building on the successor plan avoids a forced migration later and aligns with the direction of the platform.

### 2. Lowest available cost for sporadically used functions

Flex Consumption scales to zero when idle and bills only for actual execution time and memory consumed, making it the cheapest option for a project that sees only occasional manual invocations. No minimum monthly charge applies when the app is not in use.

### 3. Cold starts are acceptable

There is no user-facing SLA or latency requirement for this project. The occasional cold start on the first request after a period of inactivity is a known and accepted trade-off in exchange for zero idle cost.

### 4. Future-proof feature set included at no extra cost

Flex Consumption provides VNet integration and configurable per-instance memory — features previously requiring a Premium plan. These are not needed today but come at no additional cost, leaving the option open without a plan migration.

---

## Consequences

- The Bicep `Microsoft.Web/serverfarms` resource must use `sku.name: 'FC1'` and `sku.tier: 'FlexConsumption'`.
- A `functionAppConfig` block is mandatory on `Microsoft.Web/sites` at creation time; omitting it causes a `BadRequest` error from the ARM API.
- `functionAppConfig.deployment.storage` must reference a blob container URL; the app authenticates to this container via its system-assigned managed identity (`SystemAssignedIdentity` auth type).
- `functionAppConfig.runtime` replaces `siteConfig.linuxFxVersion` for specifying the language runtime and version.
- `WEBSITE_RUN_FROM_PACKAGE` is not applicable to Flex Consumption and must be omitted.
- A `deploymentpackages` blob container is provisioned in the same storage account used for `AzureWebJobsStorage`; the `Storage Blob Data Owner` role on the storage account covers access to this container.

---

## Alternatives Considered

| Option                       | Rejected because                                                             |
| ---------------------------- | ---------------------------------------------------------------------------- |
| Consumption (Y1/Dynamic)     | Being superseded by Flex Consumption; will not receive new platform features |
| Premium (EP1)                | Always-on billing is disproportionate for a low-traffic learning project     |
| Dedicated (App Service Plan) | Full VM cost with no benefit for an event-driven, low-traffic workload       |
