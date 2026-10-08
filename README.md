# Open Work

A personal work-tracking tool built around the specific ADHD patterns described in
[`INTENT.md`](./INTENT.md): losing track of items once they shift from "waiting on someone
else" to "waiting on me," hyper-focusing on a small distracting detail instead of the actual
task, and not noticing (or returning to) a `Critical` item unless something actively nudges
you back to it.

It's a single container: a .NET 10 minimal API backend (EF Core on PostgreSQL) serving a
React 19 + TypeScript single-page app, plus a genuine Web Push "nudge" mechanism so a stalled
Critical item can reach you even if the tab isn't open. It's deployed to Azure Container Apps
(see [`DEPLOYMENT.md`](./DEPLOYMENT.md)), and the [Daily Driver](./DAILY_DRIVER.md) Copilot
agent feeds pending work from in-flight Copilot sessions into it hourly.

## How it works

### Categories & sorting

Every item is placed into one of three categories, purely from its dates (unless manually
overridden):

| Category | Color | Rule |
|---|---|---|
| **Critical** | red | `requiresActionAt` or `completionDueAt` is now in the past (overdue) |
| **Warning** | yellow | `requiresActionAt` is within the next 2 hours, or `completionDueAt` is within the next 2 days |
| **Acceptable** | green | Neither of the above |

Any item's category can be manually overridden from the card (e.g. a stale `Critical` you've
already handled, or one that's scoped down). The manual value always wins over the computed
one until it's cleared back to "Auto."

Within the list, items are sorted: **category** (Critical → Warning → Acceptable), then by
**`requiresActionAt` descending** (the most recent "it's your turn" items come first; items
with no `requiresActionAt` sink to the bottom of their group), then by **`completionDueAt`
ascending** (soonest deadline first), then by **priority** (Critical > Warning > Acceptable) as
a final tiebreaker. All of this logic lives in one place —
[`backend/src/OpenWork.Api/Services/Prioritization.cs`](backend/src/OpenWork.Api/Services/Prioritization.cs)
— if the thresholds above don't match how you actually think about urgency, that's the file to
tune.

### Quick add

The add form is deliberately just title + optional details/link + priority + a due date, with
`+1 day` / `+3 day` / `+7 day` quick-select buttons next to the raw date picker, so capturing a
stray thought takes a few seconds on any device.

### Editing & status

Expanding a card lets you change any field (title, details, link, priority, due date, category
override) and set its status: **Not started**, **In progress**, or **Blocked**. "Done" removes
it from the list. Items you've marked **In progress** are never nudged - you're already on it.

### Marking "it's your turn"

Each item has a "Mark action required now" toggle, which stamps `requiresActionAt` with the
current time. That's the field meant to represent a state change landing back on you (a PR
comment, a reply, a cleared blocker) — ticking it makes the item jump to the top of its
category and, once it crosses into Critical, start nudging you.

### Nudges (the "remote reminder" piece)

A background service in the backend sweeps every open item that isn't already In progress
once a minute. Anything that's (still) Critical and hasn't been nudged in the last 30 minutes triggers a real
[Web Push](https://developer.mozilla.org/en-US/docs/Web/API/Push_API) notification to every
browser/device you've opted in on — delivered by the browser's push service, so it reaches you
even if the app isn't open. A VAPID key pair is generated once on first run and persisted in
the database so restarts don't invalidate existing subscriptions.

Turn this on via the "Enable Critical-item nudges" button in the header (requires a one-time
browser permission prompt). **On iOS, Web Push only works for a PWA added to the home screen**
(Settings → Share → Add to Home Screen) on iOS 16.4+ — this is a platform limitation of Safari,
not a bug here.

## Running locally

**Database** - a local PostgreSQL matching `appsettings.Development.json`:

```powershell
docker run -d --name openwork-pg -p 5432:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=openwork-dev postgres:16-alpine
```

**Backend** (`.NET 10 SDK` required) - migrations are applied automatically on startup:

```powershell
cd backend
dotnet run --project src/OpenWork.Api
```

Runs on `http://localhost:5080`.

**Frontend** (Node 22+ required), in a second terminal:

```powershell
cd frontend
npm install
npm run dev
```

Runs on `http://localhost:5173` and proxies `/api` and `/healthz` to the backend above.

## Running the container

```powershell
docker compose up --build
```

Starts the app on `http://localhost:8080` alongside a PostgreSQL container, with data kept in
the named volume `open-work-db`. The image itself is stateless - pass
`ConnectionStrings__Default` to point it at any PostgreSQL instance.

## CI/CD

[`.github/workflows/build-and-publish.yml`](.github/workflows/build-and-publish.yml) runs on
every push/PR to `main`:

1. **Build & verify** — restores/builds the .NET solution and builds the frontend, so a broken
   commit fails fast.
2. **Build & push container image** (push to `main` only) — builds the single-container image
   and pushes it to GitHub Container Registry as `ghcr.io/<this-repo>:latest` and
   `:<commit-sha>`.
3. **Deploy to Azure** (push to `main`, enabled via the `ENABLE_AZURE_DEPLOY` repo variable) —
   signs in to Azure with GitHub OIDC and points the Container App at the new image tag.

Infrastructure is defined in [`infra/main.bicep`](infra/main.bicep); the live environment,
identity setup, and operational notes are in [`DEPLOYMENT.md`](DEPLOYMENT.md).

## Project layout

```
backend/   .NET 10 minimal API — models, EF Core (PostgreSQL) + migrations, prioritization logic,
           Web Push nudge background service, CRUD + push endpoints
frontend/  React 19 + TypeScript + Vite SPA, PWA manifest + service worker for push
infra/     Bicep for the Azure Container Apps + PostgreSQL deployment
Dockerfile Multi-stage build: Vite build → .NET publish → single ASP.NET Core runtime image
```
