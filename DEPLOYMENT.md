# Deployment status & next steps

This documents where Azure deployment of Open Work stands. The app itself is
finished, built, verified, and committed; **Azure deployment is blocked on a
permissions grant only the account/subscription owner can make** (see below).
Everything that could be prepared in advance - the Bicep IaC, the CI deploy
step, this doc - is done and ready to run the moment that's unblocked.

## Target environment (confirmed by user)

- Tenant: `16b3c013-d300-468d-ac64-7eda0820b6d3`
- Subscription: `2b4c73d5-769b-4b2f-9f5f-420c55fdee99` (`MCAPS-Hybrid-REQ-146787-2026-jamesgould`)
- Resource group: **`rg-open-work`** in `uksouth` - a brand-new, isolated
  group (user asked that it not share any existing infra). The subscription
  already has ~17 unrelated resource groups (`McapsGovernance`, `rg-ahm-*`,
  `jgould-dev-testing`, etc.) - do not reuse or touch these.

## ⚠️ Blocking issue: no write access on the subscription

The signed-in identity available to this agent (`jamesgould@microsoft.com`,
object id `3b305683-2eab-47e5-ba11-d5f26d653e94`) can **list** subscriptions
and resource groups, but a direct resource-group create call returns a
definitive, non-retryable authorization failure:

```
AuthorizationFailed: The client 'jamesgould@microsoft.com' ... does not have
authorization to perform action 'Microsoft.Resources/subscriptions/resourcegroups/write'
over scope '/subscriptions/2b4c73d5-769b-4b2f-9f5f-420c55fdee99/resourcegroups/rg-open-work'
```

This is almost certainly **Reader-only** access on this MCAPS subscription
(a `role_assignment_list` call for the same identity also failed with
`AuthorizationFailed` on `Microsoft.Resources/subscriptions/read`, consistent
with a narrow/custom role rather than Contributor+). No tool available to
this agent can grant itself additional RBAC - that has to come from whoever
administers this subscription (typically done via Entra/PIM or the Access
Control (IAM) blade in the portal).

**To unblock, grant `jamesgould@microsoft.com` `Contributor` on either:**
- the subscription `2b4c73d5-769b-4b2f-9f5f-420c55fdee99` (simplest), or
- scoped to just `rg-open-work` after creating that empty resource group
  manually first (tighter blast radius, if preferred).

Once either is done, deployment is a single command (see below) - no further
investigation needed.

## Done so far

- **App**: backend `.NET 10` + frontend `React 19`, built and verified to
  compile and run correctly. Committed locally to `main` (commits `3eddee2`,
  `5f080f1`, plus infra commits below) - **still not pushed to GitHub**
  (nothing has asked for that yet, and pushing now would trigger CI that
  can't deploy anyway).
- **Infra as code**: `infra/main.bicep` + `infra/main.parameters.json` -
  compiles cleanly (`az bicep build`, zero errors/warnings). Provisions,
  all inside `rg-open-work` only:
  - Log Analytics workspace (Container Apps logs)
  - Storage account + Azure Files share (`openwork-data`, 5 GiB default),
    mounted into the Container Apps environment and then into the container
    at `/data` - this is where the SQLite DB and generated VAPID keys live
    (see `VapidKeyProvider.cs`), so state survives restarts/redeploys.
  - Container Apps managed environment
  - Container App: single container, image `ghcr.io/james-gould/open-work:latest`
    by default, **target port 8080** (matches the `Dockerfile`'s
    `ASPNETCORE_URLS=http://+:8080`), external ingress, `minReplicas: 1` /
    `maxReplicas: 1` (an always-on instance is required: the
    `NudgeBackgroundService` needs to keep running, and SQLite-on-a-volume
    doesn't support >1 replica), 0.5 vCPU / 1 Gi.
  - Deliberately **no ACR**: CI already pushes to GHCR
    (`ghcr.io/james-gould/open-work`), so the Container App pulls directly
    from there. If the GHCR package is left **public** (one-time: repo →
    Packages → open-work → Package settings → Change visibility), no
    registry credentials are needed at all. If it must stay private, the
    template accepts `ghcrUsername` + `ghcrToken` (a PAT with
    `read:packages`) and wires up a Container Apps secret instead.
- **CI** (`.github/workflows/build-and-publish.yml`): `deploy` job (gated
  behind `vars.ENABLE_AZURE_DEPLOY == 'true'`, currently unset/disabled) now
  does `az containerapp update --image ghcr.io/<repo>:<sha>` against the
  already-deployed Container App, rather than trying to provision anything -
  infra provisioning is a one-time, manual `az deployment group create`
  (below), CI only ever updates the image after that.
- Confirmed Docker Desktop is installed locally
  (`%LOCALAPPDATA%\Programs\DockerDesktop`), started it, confirmed the engine
  responds (`docker info` → Server Version 29.8.2). Frontend (`npm run build`)
  and backend (`dotnet build`/`dotnet run`) both independently verified
  working in earlier sessions.
- Installed Azure CLI 2.91.0 locally (`winget install --id Microsoft.AzureCLI
  -e --source winget`; the default `msstore` source fails with a DNS error in
  this environment, `--source winget` is required). Not logged in - see
  below.

## Known non-blocking loose ends

- **Full multi-stage `docker build .` has not completed successfully in this
  sandboxed dev environment.** It fails at `dotnet restore` with a TLS
  handshake failure reaching `api.nuget.org`, caused by this machine's
  corporate network/proxy intercepting outbound HTTPS from containers
  specifically (regular host-level `dotnet restore`/`npm run build` both
  work fine, and even trusting the exported corporate root CA inside the
  image didn't fix it - it looks like a network-path issue, not a cert-trust
  one). **This is specific to this sandboxed machine's network, not the
  Dockerfile or the app** - GitHub Actions' hosted runners have no such
  proxy and already build this exact Dockerfile successfully in the
  `docker` CI job. No Dockerfile changes were made for this; it isn't a
  defect to fix, just an environment limitation of local verification here.
- `az login` was attempted (`--use-device-code`) to get a CLI session that
  could run `az group create` as a fallback once permissions are granted,
  but device-code login needs a human to complete it in a browser, so it
  was left unfinished/stopped. Not needed if access is granted to the
  identity the `azure-mcp-*` tools already use.
- Backend has 2 pre-existing NU1903 advisory warnings (`Microsoft.OpenApi
  2.0.0`, `SQLitePCLRaw.lib.e_sqlite3 2.1.11`), transitive, not addressed -
  see README.

## Exact steps to finish, once Contributor access is granted

1. Create the resource group (if not already created as part of granting
   scoped access):
   ```powershell
   az login   # or reuse whatever credential now has Contributor
   az group create -n rg-open-work -l uksouth --subscription 2b4c73d5-769b-4b2f-9f5f-420c55fdee99
   ```
2. Deploy the infra:
   ```powershell
   az deployment group create -g rg-open-work `
     --template-file infra/main.bicep `
     --parameters infra/main.parameters.json `
     --subscription 2b4c73d5-769b-4b2f-9f5f-420c55fdee99
   ```
   This stands the app up immediately pulling
   `ghcr.io/james-gould/open-work:latest` - but that GHCR package doesn't
   exist yet until step 3, so the Container App will show a pull failure
   until then (expected, self-heals once the image exists).
3. Push the local commits to GitHub (`git push origin main`) so the existing
   `build`/`docker` CI jobs run and publish
   `ghcr.io/james-gould/open-work:latest` to GHCR. Make that package public
   (Package settings → Change visibility) so the Container App (and CI's
   future `containerapp update` calls) need no registry credentials.
4. Set these in the GitHub repo (Settings → Secrets and variables → Actions):
   - Variables: `ENABLE_AZURE_DEPLOY=true`, `AZURE_RESOURCE_GROUP=rg-open-work`,
     `AZURE_CONTAINER_APP_NAME=openwork-app`
   - Secret: `AZURE_CREDENTIALS` - a service principal JSON, created once
     with `az` authenticated as the now-Contributor identity:
     ```powershell
     az ad sp create-for-rbac --name sp-open-work-ci `
       --role Contributor `
       --scopes /subscriptions/2b4c73d5-769b-4b2f-9f5f-420c55fdee99/resourceGroups/rg-open-work `
       --sdk-auth
     ```
     (paste the JSON output directly as the secret value)
5. From then on, every push to `main` rebuilds, republishes to GHCR, and
   updates the Container App's running image automatically.
6. Verify: hit `https://<containerAppFqdn output>/healthz` - should return
   `{"status":"ok"}`. Get the FQDN from the `az deployment group create`
   output, or `az containerapp show -g rg-open-work -n openwork-app --query
   properties.configuration.ingress.fqdn`.
