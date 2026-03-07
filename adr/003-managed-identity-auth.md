# ADR-003: Use Managed Identity for Azure Service Authentication

**Date:** 2026-03-06  
**Status:** Accepted

---

## Context

The Function App needs to authenticate against two Azure services:

- **Azure Storage** — required by the Functions runtime for internal state (blobs, queues, tables) via the `AzureWebJobsStorage` setting.
- **Azure Cosmos DB** — used by application code to read and write events.

There are two approaches:

- **Connection strings / access keys** — a secret string containing the account name and a shared key (or the full endpoint + key). Stored as an app setting, passed as a parameter, and potentially leaked in deployment logs, state files, or source control.

- **Managed identity** — the Function App is assigned a system-managed Azure AD identity at creation time. RBAC role assignments grant that identity access to specific resources. No secret is ever created, stored, or rotated.

---

## Decision

Use a **system-assigned managed identity** on the Function App and grant it the minimum required RBAC roles on Storage and Cosmos DB. No connection strings or access keys are used anywhere in the Bicep templates or application configuration.

---

## Implementation

### Function App identity

`identity: { type: 'SystemAssigned' }` is declared in `modules/functions.bicep`. Azure creates the Azure AD service principal on first deployment.

### Azure Storage

The Functions runtime supports managed-identity storage connections via two app settings (no connection string):

```
AzureWebJobsStorage__accountName  = <storage account name>
AzureWebJobsStorage__credential   = managedidentity
```

The identity is granted three RBAC roles on the Storage Account (all three are required by the runtime):

| Role                           | Purpose                                         |
| ------------------------------ | ----------------------------------------------- |
| Storage Blob Data Owner        | Checkpoint blobs, Durable Functions lease blobs |
| Storage Queue Data Contributor | Trigger/output queue messages                   |
| Storage Table Data Contributor | Durable Functions history table                 |

### Cosmos DB

The application code must construct `CosmosClient` with `DefaultAzureCredential`:

```csharp
new CosmosClient(endpoint, new DefaultAzureCredential())
```

The endpoint is injected via:

```
CosmosDb__AccountEndpoint = https://<account>.documents.azure.com:443/
```

The identity is granted the **Cosmos DB Built-in Data Contributor** role (`00000000-0000-0000-0000-000000000002`). This is a **data-plane** role assigned through `Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments` — it is distinct from ARM-level roles and must be assigned separately. It allows full read/write access to items but no control-plane operations (no ability to create/delete databases or containers).

### Role assignments

All role assignments are in `modules/roleAssignments.bicep`. Each assignment name uses `guid()` seeded from the resource ID, principal ID, and role ID — making re-deployments fully idempotent.

### Local development

`DefaultAzureCredential` in the Azure SDK falls back through a credential chain. Running `az login` locally is sufficient; no environment variables or local secrets are needed. The Cosmos Emulator can also be used with its default endpoint.

---

## Reasons

### 1. No secrets to manage

Connection strings are long-lived shared keys. Managed identity eliminates the need to create, distribute, rotate, or revoke them. There is no secret that can be accidentally committed to source control or exposed in deployment logs.

### 2. Principle of least privilege

RBAC roles are scoped to specific resources and grant only the permissions required. Connection strings grant full access to the entire account with no fine-grained control.

### 3. Azure best practice

Microsoft's published guidance for production Functions workloads recommends managed-identity connections for both `AzureWebJobsStorage` and Cosmos DB. The isolated worker model's `DefaultAzureCredential` support makes this straightforward to implement.

### 4. No code change required on identity switch

If the system-assigned identity is ever replaced with a user-assigned identity, only the Bicep changes — application code using `DefaultAzureCredential` continues to work without modification.

---

## Consequences

- The `CosmosClient` in `TicketFlow.Infrastructure` must be constructed with `DefaultAzureCredential`, not a connection string.
- Local development requires `az login` (or the Azure CLI credential picked up by `DefaultAzureCredential`).
- The Cosmos DB Built-in Data Contributor role assignment takes effect within seconds but may lag slightly on first deployment; a retry in application startup handles this gracefully.
- If a future phase adds Service Bus or Blob Storage (for QR PDFs), the same pattern applies: add the relevant RBAC roles to `roleAssignments.bicep` and use `DefaultAzureCredential` in code.
