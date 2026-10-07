# Deployment status & next steps

This documents where Azure deployment of Open Work stands, so work can resume
without re-discovering context.

## Target environment (confirmed by user)

- Tenant: `16b3c013-d300-468d-ac64-7eda0820b6d3`
- Subscription: `2b4c73d5-769b-4b2f-9f5f-420c55fdee99` (`MCAPS-Hybrid-REQ-146787-2026-jamesgould`)
- Resource group: **`rg-open-work`** in `uksouth` — must be a brand-new, isolated
  group (user explicitly asked it not share any existing infra). The
  subscription already has ~17 unrelated resource groups (`McapsGovernance`,
  `rg-ahm-*`, `jgould-dev-testing`, etc.) — do not reuse or touch these.

## Done so far

- Full app (backend `.NET 10` + frontend `React 19`) built, verified to
  compile and run correctly (see commit `3eddee2`, already on local `main`,
  **not yet pushed to GitHub**).
- Looked up the Azure best-practices guidance for deployment: use Bicep IaC
  under `infra/`, validate with `whatif_deployment` before `create_deployment`,
  check policy assignments first (policy-list call returned `403
  AuthorizationFailed` for this identity — revisit if it blocks later steps).
- Confirmed Docker Desktop *is* installed locally
  (`%LOCALAPPDATA%\Programs\DockerDesktop`) but wasn't running; started it and
  confirmed the engine responds to `docker info` (Server Version 29.8.2).
- Attempted to create the `rg-open-work` resource group via the `azure-mcp-arm`
  `create_or_update_resource_group` command — **failed twice with an opaque
  "unexpected error" from the ARM MCP server** (trace IDs
  `17d1e78a5c678a1b5900e4c5a7b3a6f9` and `af30bd2bf480028902a539b6d1392405`,
  reportable at `https://aka.ms/ARMMCPIssue`). Not yet retried a third time or
  diagnosed further.
- Started a local `docker build -t open-work:local .` to verify the full
  multi-stage image — was **stopped mid-build** (not completed/verified) so
  the laptop could be shut down cleanly.

## Not yet done — pick up here

1. **Create the resource group.** Retry
   `azure-mcp-arm create_or_update_resource_group` (location `uksouth`, name
   `rg-open-work`) — if it keeps failing, create it directly in the Azure
   Portal or via `az group create -n rg-open-work -l uksouth --subscription 2b4c73d5-769b-4b2f-9f5f-420c55fdee99`
   once `az` CLI is available (it is **not** currently installed in this
   environment — only the `azure-mcp-*` tool surface was used, which has no
   "create a user/service principal" capability — see point 4).
2. **Author `infra/*.bicep`** (not yet written) for, all scoped to
   `rg-open-work` only:
   - Log Analytics workspace
   - Container Apps Environment
   - Azure Container Registry (Basic SKU)
   - Storage account + Azure Files share, mounted as a Container Apps
     Environment storage + volume at `/data` (this is where the SQLite file
     and generated VAPID keys persist across restarts/redeploys — see
     `VapidKeyProvider.cs` / `ConnectionStrings:Default` in `appsettings.json`)
   - Container App: single container, **target port 8080** (matches
     `Dockerfile`'s `ASPNETCORE_URLS=http://+:8080`), external ingress,
     `minReplicas: 1` (the `NudgeBackgroundService` needs an always-on
     instance to send push nudges; also SQLite-on-a-volume doesn't support
     >1 replica), 0.5 vCPU / 1 Gi.
   - Validate with `whatif_deployment` before `create_deployment`.
3. **Finish verifying `docker build -t open-work:local .`** end-to-end (the
   multi-stage Dockerfile itself has never fully completed a build in this
   session — only the underlying `npm run build` and `dotnet build` steps
   were separately verified). Then push the image into the new ACR.
4. **Wire up GitHub Actions → Azure auth.** The repo's CI
   (`.github/workflows/build-and-publish.yml`) already has a `deploy` job
   scaffolded but disabled (`vars.ENABLE_AZURE_DEPLOY`), expecting a
   `secrets.AZURE_CREDENTIALS` service-principal JSON plus
   `vars.AZURE_RESOURCE_GROUP` / `AZURE_CONTAINER_APP_NAME` /
   `AZURE_CONTAINER_APP_ENVIRONMENT`. **This environment has no `az` CLI and
   no Entra/Graph MCP tool**, so a service principal / federated credential
   could not be created automatically. Options once resumed:
   - Run `az ad sp create-for-rbac --role Contributor --scopes /subscriptions/2b4c73d5-769b-4b2f-9f5f-420c55fdee99/resourceGroups/rg-open-work --sdk-auth` yourself (or from an environment with `az` installed) and paste the JSON into the `AZURE_CREDENTIALS` GitHub secret, **or**
   - Set up OIDC federated credentials (no secret, more setup) per
     `azure/login@v2` docs, **or**
   - Simpler short-term option: enable the ACR admin user and use its
     username/password as `docker/login-action` + `az containerapp update`
     credentials directly (less best-practice but no Entra app registration
     needed) — update the CI `docker`/`deploy` jobs to match if chosen.
5. **Push the local commit to GitHub** (currently only committed on local
   `main`, not pushed) once the above is ready, then enable
   `ENABLE_AZURE_DEPLOY` and set the GitHub repo variables/secrets so the
   existing `deploy` job in `build-and-publish.yml` goes live.
6. Switch the Container App's registry from GHCR references (if CI still
   targets GHCR) to the new ACR, or decide to keep GHCR for the image and use
   Azure auth only for the `containerapp update` deploy step — pick one and
   keep `Dockerfile`/CI/`infra/` consistent.
7. After first successful deploy: hit the Container App's FQDN
   `/healthz`, confirm it returns `{"status":"ok"}`, and give the user the
   live URL + a link to `rg-open-work` in the Azure portal.

## Known loose ends unrelated to deployment

- `backend/.../OpenWork.Api.csproj` has 2 NU1903 advisory warnings
  (`Microsoft.OpenApi 2.0.0`, `SQLitePCLRaw.lib.e_sqlite3 2.1.11`) — transitive,
  not fixed, see README.
- Azure Policy assignment list call returned `403` for the signed-in identity
  on this subscription — worth re-checking once resuming in case it blocks
  Bicep deployment via policy denial (not yet confirmed either way).
