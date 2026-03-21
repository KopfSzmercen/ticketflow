## Plan: Low-Cost Application Insights for Functions

Add workspace-based Application Insights to the existing Azure Functions deployment with strict cost controls (minimal telemetry depth, adaptive sampling, daily cap, 30-day retention) and environment-specific defaults (dev/stage/prod). Keep current managed-identity architecture unchanged; only App Insights ingestion uses connection string app settings.

**Steps**

1. Phase 1 - Observability design and parameter model.
2. Define observability defaults per environment in parameter files (dev/stage/prod) with explicit values for retention days, daily cap, and sampling profile. _Blocks Phase 2 because module inputs must be settled first._
3. Decide naming conventions for monitoring resources aligned with existing `appName`/`environment` pattern (`appi-...`, `log-...`) and keep region consistent with `location`. _Parallel with step 2._
4. Phase 2 - Infrastructure wiring in Bicep.
5. Add a new monitoring module at `/home/komp/Learning/TicketFlow/infra/modules/monitoring.bicep` that provisions: (a) Log Analytics workspace and (b) workspace-based Application Insights component linked to that workspace.
6. Extend `/home/komp/Learning/TicketFlow/infra/main.bicep` to invoke the monitoring module and pass outputs into the Functions module.
7. Extend `/home/komp/Learning/TicketFlow/infra/modules/functions.bicep` parameters and app settings to include Application Insights connection string, plus optional telemetry toggles needed for minimal mode. _Depends on step 6._
8. Update parameter files (starting with `/home/komp/Learning/TicketFlow/infra/parameters/dev.bicepparam`, then stage/prod equivalents when present) to carry monitoring/cost-control inputs. _Depends on step 2._
9. Phase 3 - Functions host telemetry shaping (minimal-cost mode).
10. Update `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/host.json` logging section to keep invocation visibility while suppressing noisy categories and enabling aggressive adaptive sampling.
11. Update `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/Program.cs` only if needed for isolated-worker AI integration path selected by current package/runtime model; prefer minimum required change to avoid duplicative telemetry pipelines.
12. If package additions are required, add central package version in `/home/komp/Learning/TicketFlow/Directory.Packages.props` and reference from `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/TicketFlow.Functions.csproj`. _Depends on step 11 decision._
13. Phase 4 - Documentation and architecture record.
14. Add a new ADR in `/home/komp/Learning/TicketFlow/adr/` documenting why workspace-based AI + minimal telemetry profile was chosen, expected monthly cost behavior, and trade-offs (sampling data loss, daily cap truncation).
15. Update `/home/komp/Learning/TicketFlow/README.md` with where to view invocation logs, key KQL queries for invocations/errors, and how to tune sampling/cap per environment.
16. Phase 5 - Validation and rollout checks.
17. Validate template integrity via `az bicep build --file infra/main.bicep`.
18. Validate planned infra impact via `az deployment sub what-if --location polandcentral --template-file infra/main.bicep --parameters infra/parameters/dev.bicepparam`.
19. Deploy to dev, invoke health + one orchestrated flow, and confirm traces/requests appear in Application Insights while telemetry volume remains below cap.
20. Verify failure-path logs (warning/error) are retained and discoverable with queries, then promote same pattern to stage/prod parameter files.

**Relevant files**

- `/home/komp/Learning/TicketFlow/infra/main.bicep` - add monitoring module invocation and pass outputs downstream.
- `/home/komp/Learning/TicketFlow/infra/modules/functions.bicep` - add App Insights connection string app setting and telemetry controls.
- `/home/komp/Learning/TicketFlow/infra/modules/monitoring.bicep` - new module for Log Analytics + Application Insights resources.
- `/home/komp/Learning/TicketFlow/infra/parameters/dev.bicepparam` - environment defaults for low-cost settings.
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/host.json` - sampling + log-level shaping for minimal telemetry.
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/Program.cs` - optional isolated worker AI service registration based on package approach.
- `/home/komp/Learning/TicketFlow/Directory.Packages.props` - central package versioning if AI package is added.
- `/home/komp/Learning/TicketFlow/src/TicketFlow.Functions/TicketFlow.Functions.csproj` - package reference if required.
- `/home/komp/Learning/TicketFlow/adr/` - new ADR for observability and cost rationale.
- `/home/komp/Learning/TicketFlow/README.md` - operational guidance and query snippets.

**Verification**

1. Run `az bicep build --file infra/main.bicep` to validate syntax.
2. Run subscription-level what-if against dev params and confirm only expected monitoring + app setting deltas.
3. Trigger at least one HTTP function and one background trigger/orchestrated path and confirm invocation logs in Application Insights `requests`/`traces`.
4. Check that informational noise is reduced (Azure SDK categories at warning+), while warnings/errors remain visible.
5. Confirm daily ingestion estimate in Azure Monitor Cost Analysis remains within target for dev usage.

**Decisions**

- Include all environments with per-environment settings.
- Retention target is 30 days.
- Telemetry mode is minimal: invocation outcomes plus warnings/errors, with aggressive sampling.
- Keep managed identity pattern for service access; accept Application Insights connection string app setting for ingestion.

**Further Considerations**

1. Sampling floor choice: Option A 5% (lowest cost) / Option B 10% (safer visibility, recommended default).
2. Daily cap baseline: Option A 0.5 GB/day (strict dev budget) / Option B 1 GB/day (fewer dropped logs during load spikes, recommended default).
3. Stage/prod posture: Option A keep minimal everywhere / Option B balanced telemetry in prod only (recommended if debugging production incidents becomes harder).
