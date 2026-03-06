# ADR-001: Use Azure Functions Isolated Worker Model

**Date:** 2026-03-06  
**Status:** Accepted

---

## Context

Azure Functions supports two hosting models for .NET:

- **In-process model** — the function code runs inside the same process as the Functions host. The project assembly must target the exact .NET version that the runtime hosts, and the project takes a hard dependency on the `Microsoft.NET.Sdk.Functions` SDK and internal Functions runtime assemblies. Microsoft announced end-of-support for the in-process model on **10 November 2026**.

- **Isolated worker model** — the function code runs in a separate worker process. The worker communicates with the host over gRPC. The project references the `Microsoft.Azure.Functions.Worker` family of packages, which are versioned independently of the runtime.

This project targets **.NET 10** and is designed to be maintained beyond 2026.

---

## Decision

Use the **isolated worker model** for all Azure Functions in TicketFlow.

---

## Reasons

### 1. .NET 10 support

The in-process model is permanently pinned to the .NET version embedded in the Azure Functions v4 host. The isolated worker model allows the project to target any .NET version — including .NET 10 — independently of the host runtime.

### 2. Microsoft's published roadmap

Microsoft has officially retired the in-process model effective November 10, 2026. Choosing it today would require a forced migration before that date with no meaningful benefit in return.

### 3. No assembly version conflicts

In-process functions share the host's AppDomain, so package versions (e.g. `Azure.Core`, `Newtonsoft.Json`) must align with what the host already loads. The isolated model eliminates these conflicts entirely: the worker process owns its own dependency graph.

### 4. Standard ASP.NET Core middleware and DI

The isolated worker exposes a `HostBuilder`-based startup with full `IServiceCollection` registration parity and support for ASP.NET Core-style middleware on HTTP triggers — patterns the rest of the .NET 10 ecosystem already uses.

### 5. Cleaner architecture boundary

Because the worker process has no runtime coupling to Functions host internals, `TicketFlow.Core` (domain) and `TicketFlow.Infrastructure` (Azure SDK adapters) carry zero Azure Functions dependencies. Only `TicketFlow.Functions` references the worker SDK, keeping the dependency graph strictly layered.

---

## Consequences

- `TicketFlow.Functions.csproj` uses `Microsoft.NET.Sdk` (not `Microsoft.NET.Sdk.Functions`) with `OutputType=Exe` and a `Program.cs` entry point.
- HTTP trigger return types use `HttpResponseData` rather than `IActionResult`.
- Binding extensions (Cosmos DB, Service Bus, Durable) are pulled in as isolated-worker NuGet packages (`Microsoft.Azure.Functions.Worker.Extensions.*`).
- Local development requires `func start` from Azure Functions Core Tools v4.

---

## Alternatives Considered

| Option                        | Rejected because                                                             |
| ----------------------------- | ---------------------------------------------------------------------------- |
| In-process model              | End-of-life November 2026; cannot target net10.0                             |
| ASP.NET Core minimal API only | Loses Durable Functions orchestration and built-in trigger/binding ecosystem |
