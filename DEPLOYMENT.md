# Deployment status & next steps

**The app is deployed and live** in Azure Container Apps.

```
https://openwork-app.delightfulpebble-ad1c96d3.uksouth.azurecontainerapps.io
```

`/healthz` returns `{"status":"ok"}`. CI auto-deploys on every push to `main`
via GitHub OIDC (no stored secrets). One known limitation remains: **data is
not yet persistent across restarts** (see below) - that's the next real task.

## Target environment

- Tenant: `16b3c013-d300-468d-ac64-7eda0820b6d3`
- Subscription: `2b4c73d5-769b-4b2f-9f5f-420c55fdee99` (`MCAPS-Hybrid-REQ-146787-2026-jamesgould`)
- Resource group: `rg-open-work` in `uksouth` - isolated, dedicated to this
  app, no shared infra with the subscription's other resource groups.

## What actually blocked this, and how each was resolved

### 1. Identity mismatch (the real root cause of the original "AuthorizationFailed")

The `azure-mcp-*` MCP tools authenticate as a **different Entra object id**
than this machine's real interactive user, even though both resolve to the
same UPN (`jamesgould@microsoft.com`):

- `3b305683-2eab-47e5-ba11-d5f26d653e94` - Reader only (inherited from a
  management-group policy). This is who `azure-mcp-*` tools ran as, which is
  why every read (list subscriptions/RGs/role assignments) worked but every
  write/deploy failed with `AuthorizationFailed`.
- `bec79daa-7e4a-4555-acfb-59f019c529f2` - the real identity, holds `Owner`
  at subscription scope. Confirmed via a fresh interactive `az login` on the
  actual desktop (WAM broker/account picker), then `az ad signed-in-user show`.

**Workaround**: run all write/deploy operations through the `az` CLI after an
interactive `az login`, not through the `azure-mcp-*` tools. Reads via
`azure-mcp-*` are fine.

### 2. GHCR image was private

`james-gould/open-work` is a private repo, so its GHCR package
(`ghcr.io/james-gould/open-work`) inherited private visibility - Container
Apps' anonymous pull got `DENIED`. GitHub's REST API does **not** support
changing a repo-linked container package's visibility (confirmed: `PATCH
/user/packages/container/...` 404s; it's web-UI only), so instead the
Container App was wired with registry credentials:

```powershell
az rest --method patch --url "https://management.azure.com/subscriptions/<sub>/resourceGroups/rg-open-work/providers/Microsoft.App/containerApps/openwork-app?api-version=2024-03-01" --body '{
  "properties": { "configuration": { "secrets": [{ "name": "ghcr-pat", "value": "<gh auth token with read:packages>" }],
  "registries": [{ "server": "ghcr.io", "username": "james-gould", "passwordSecretRef": "ghcr-pat" }] } }
}'
```

(`gh auth refresh -s read:packages` was needed first to get a token with
that scope; the default `gh`/Actions token doesn't have it.)

### 3. CI service principal: tenant policy blocks long-lived secrets

`az ad sp create-for-rbac --sdk-auth` (and `az ad app credential reset`)
both failed with `Credential lifetime exceeds the max value allowed as per
assigned policy` - this tenant enforces short-lived/no app password
credentials. **Fix: switched to GitHub Actions OIDC** (federated identity),
which needs no secret at all:

- App registration `sp-open-work-ci` (appId `2515a71e-12d0-42a9-9435-db1ae787dd08`),
  `Contributor` on `rg-open-work` only.
- Federated credential trusting `repo:james-gould/open-work:ref:refs/heads/main`.
- GitHub repo secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
  `AZURE_SUBSCRIPTION_ID` (no client secret). Variables:
  `ENABLE_AZURE_DEPLOY=true`, `AZURE_RESOURCE_GROUP=rg-open-work`,
  `AZURE_CONTAINER_APP_NAME=openwork-app`.
- `.github/workflows/build-and-publish.yml`'s `deploy` job uses
  `azure/login@v2` with `client-id`/`tenant-id`/`subscription-id` +
  `permissions: id-token: write` instead of a `creds:` secret.
- Two follow-up snags, both now fixed: (a) this repo has GitHub's
  "immutable OIDC subject" setting on, so the subject claim embeds numeric
  owner/repo IDs (`repo:james-gould@<id>/open-work@<id>:ref:refs/heads/main`)
  rather than the plain-name format - the federated credential's `subject`
  had to match that exact string (check via `gh api
  repos/<owner>/<repo>/actions/oidc/customization/sub`); (b) the
  image-update step uses `az rest --method patch` instead of `az
  containerapp update`, because the `containerapp` CLI extension's
  client-side `Microsoft.App` provider-registration check gets stuck
  reporting `"Registering"` indefinitely in this tenant even though the
  provider works fine via ARM directly. **CI now deploys green end-to-end
  on every push to `main`** - confirmed working.

### 4. Azure Files volume mount: policy blocks it entirely (⚠️ open follow-up)

The original Bicep mounted a `/data` Azure Files share into the container so
SQLite + generated VAPID keys would survive restarts. This **cannot work in
this tenant**: the storage account's `allowSharedKeyAccess` and
`publicNetworkAccess` are both forced by policy (`allowSharedKeyAccess:
false`, `publicNetworkAccess: Disabled` - confirmed by attempting
`az storage account update` for both; the call reports success but the
values silently stay unchanged, i.e. policy-enforced, not just defaults).
Azure Container Apps' `AzureFile` volume type **only** supports account-key
SMB auth (no managed-identity/Entra option), and the storage account has no
reachable public endpoint for SMB either way. Mount attempts failed with
`mount error(13): Permission denied`.

**Temporary fix applied**: removed the volume mount; the container now uses
its own ephemeral filesystem for `/data/openwork.db` (`infra/main.bicep` and
the live Container App were both updated to match). **This means all data
(work items, VAPID keys) is lost on every restart/redeploy/scale event.**
The storage account + file share are still deployed but unused - safe to
leave for now, cheap, or remove in a future cleanup.

**Real fix, not yet done**: move persistence off SQLite+Files entirely onto
something Container Apps can reach over the network without a file mount -
Azure Database for PostgreSQL Flexible Server (or Azure SQL Basic), using
the Container App's system-assigned managed identity for Entra-only auth
(no secrets, compatible with this tenant's shared-key-disabled policy).
Needs: new Bicep resource, EF Core provider swap (`Npgsql.EntityFrameworkCore.PostgreSQL`
or `Microsoft.EntityFrameworkCore.SqlServer`), connection string via managed
identity, and regenerating the EF migration the "Daily Driver" work just
added. Scoped as a separate follow-up, not done in this pass.

## Current infra state (`rg-open-work`, `uksouth`)

| Resource | Status |
|---|---|
| `openwork-logs` (Log Analytics) | ✅ healthy |
| `openworksttt7a3fgnr2bss` (Storage + Files share) | ✅ healthy, currently unused (see above) |
| `openwork-env` (Container Apps environment) | ✅ healthy |
| `openwork-app` (Container App) | ✅ healthy, running, `/healthz` returns 200 |

## Still to do

- [ ] Replace ephemeral SQLite with a networked DB (Postgres/SQL Flexible
      Server + managed identity) so data survives restarts - see "real fix"
      above. This is the main remaining gap.
- [ ] Once that's done, remove the now-unused storage account + file share
      from `infra/main.bicep` (or repurpose for something else).
- [ ] Review/merge the "Daily Driver" feature branch
      (`james-gould-daily-driver-agent`) - see `DAILY_DRIVER.md`.
- [ ] Decide whether to make the GHCR package public anyway (simpler long
      term than rotating the `read:packages` PAT used as the registry
      secret) via the GitHub web UI (Package settings → Change visibility).
