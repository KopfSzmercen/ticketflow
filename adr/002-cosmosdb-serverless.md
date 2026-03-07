# ADR-002: Use Cosmos DB Serverless Throughput Mode

**Date:** 2026-03-06  
**Status:** Accepted

---

## Context

Cosmos DB supports two capacity modes for SQL API accounts:

- **Provisioned throughput** — RU/s are reserved upfront per container or database. You pay for the reserved capacity regardless of actual traffic. Can autoscale within a configured range,. Suitable for predictable, high-volume workloads.

- **Serverless** — RU/s are consumed on demand, billed per request. No minimum cost when idle. Subject to a single-region limit and a maximum throughput ceiling (~5,000 RU/s per container as of 2026). Suitable for unpredictable or low-traffic workloads.

TicketFlow is a learning project. The `events` container will receive only manual test traffic.

---

## Decision

Use **Serverless** capacity mode for the Cosmos DB account in all environments.

---

## Reasons

### 1. Zero idle cost

A provisioned account billed at 400 RU/s costs roughly $23/month whether or not a single request is made. Serverless has no baseline charge — the dev environment costs nothing when not actively used.

### 2. Traffic profile matches serverless

Serverless is designed for sporadic, low-volume workloads. Manual API calls via Postman/curl during development are exactly that pattern.

### 3. Throughput ceiling is not a constraint here

The 5,000 RU/s per container ceiling of serverless is irrelevant for a toy project. If TicketFlow were ever production-bound, switching to provisioned (or autoscale provisioned) is a one-line Bicep change.

### 4. Simplicity

Serverless accounts do not require per-container throughput configuration. The `events` container requires no `options.throughput` property, reducing Bicep boilerplate.

---

## Consequences

- If a future phase stress-tests the purchase saga at high concurrency, capacity mode can be changed to provisioned autoscale by removing the `EnableServerless` capability and adding `autoscaleSettings` to the container.
- Serverless Cosmos DB does not support multi-region writes; the account is single-region. Acceptable for a learning project.
