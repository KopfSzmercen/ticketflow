## Phase 1: Event Management + IaC & CI/CD Baseline

**Step 1: Creating IaC + deployment**: Bicep templates created, resources deployed via azure CLI locally and everything works. Added ADRs for cosmosdb, hosting plans and managed identity auth.

**Step 2: CI/CD pipeline**: GitHub Actions workflow configured to build, test, and deploy on every push to `main`. Infrastructure is kept in sync via an idempotent Bicep deployment on each run, supporting dev, stage, and prod environments each with their own region and secrets. An Azure AD app registration with a system-assigned service principal was created, granted Owner at subscription scope (required for RBAC role assignments in Bicep), and wired to GitHub Actions via OIDC federated credentials — one per environment — so no client secrets are stored anywhere.
