# Daily Driver

An ADHD-support agent + product changes built on top of Open Work, so "pending
work in flight with Copilot" doesn't silently vanish out of sight.

## What was built

1. **Progression-state + querying on `WorkItem` (this repo's backend)**
   - New `WorkItemStatus` enum: `Backlog` / `InProgress` / `Blocked` / `Done`
     (`backend/src/OpenWork.Api/Models/WorkItemStatus.cs`).
   - `WorkItem.Status` is now the single source of truth for progression state.
     `IsDone` is kept as a computed property (`Status == Done`) for backward
     compatibility with the existing frontend and `/complete`/`/reopen`
     endpoints - nothing that reads `isDone` today breaks.
   - Severity already existed as `WorkItemCategory` (`Critical` / `Warning` /
     `Acceptable`) - no new enum was needed there, it was just wired into the
     new filtering.
   - `GET /api/workitems` now accepts repeated `status=` and `category=` query
     params to filter (e.g. `?status=Backlog&status=Blocked&category=Critical&category=Warning`),
     still returned sorted top-down by the existing priority rules.
   - New `GET /api/workitems/next` returns the single highest-priority item
     that is not `Done` and (by default) not `InProgress` - the "what should I
     do next" endpoint. Optional `?category=` filters it further, and
     `?excludeInProgress=false` opts back in if ever needed.
   - `Create`/`Update` accept an explicit `status`; omitting it still works via
     the legacy `isDone` boolean (mapped to `Done`/`Backlog`).
   - `Create` also accepts `manualCategory` (pins the category, same as the
     override on the card) and `externalKey`. When an item with that
     `externalKey` already exists - in any status, including `Done` - the
     existing item is returned with `200 OK` instead of creating a duplicate.
     The hourly sweep uses `copilot-session:<sessionId>` so re-running every
     hour never piles up duplicates, and an item you've marked Done stays done.
   - A first-ever EF Core migration (`InitialCreate`) was added, since the app
     previously used `Database.EnsureCreated()` with no migration history at
     all. `Program.cs` now calls `Database.Migrate()` on startup.
   - **Verified**: `dotnet build` succeeds; ran the API live against a fresh
     SQLite DB (the store at the time - it is now PostgreSQL), created Critical+Blocked and Critical+InProgress items via the
     real HTTP API, and confirmed `/next` correctly returns only the Blocked
     one, and that `status=`/`category=` filters work. Frontend (`npm run
     build`) still compiles cleanly against the changed API shape.

2. **Extraction mechanism** - implemented as the "Extract" + "Sync" phases of
   the hourly workflow (below), not a separate standalone tool. A sub-agent
   uses `list_sessions_and_chats`, `get_sessions_status`, and
   `session_store_sql` to find genuinely open work in *other* sessions
   (evidence: unresolved todos, a pending plan awaiting approval, or
   `awaiting_input`), then the workflow POSTs each item to
   `/api/workitems` as a normal `WorkItem` (category/status inferred by the
   sub-agent, priority derived from category). At most one item per session,
   keyed by `externalKey: "copilot-session:<sessionId>"` for de-duplication.

3. **The `/pending` command** - researched properly rather than guessed (see
   "What's real vs. convention" below). Implemented as a named custom agent,
   invoked via Copilot CLI's real `/agent` slash command (this is the true
   platform mechanism - there is no separate literal `/pending`
   slash-command system).

4. **The `Daily Driver` custom agent** -
   `.github/agents/daily-driver.agent.md`, the real, documented location/format
   for a project-scoped custom agent (`.agent.md` file, YAML frontmatter +
   Markdown instructions). Instructions are written around ADHD support (task
   paralysis, time-blindness, out-of-sight-out-of-mind, context-switch cost,
   small concrete next actions, no shame language, short replies) plus the
   practical mechanics: how to answer `/pending`, how to run the extraction
   sync on demand, and general Open Work querying/status-setting.

5. **Hourly background reminder sweep** - a registered dynamic workflow
   (`daily-driver-hourly-sweep`, authored via `dynamic_workflows_manage`) with
   4 phases (Extract → Sync → Check → Remind), using real sub-agents for the
   extraction and the reminder delivery (not inline logic), scheduled hourly
   via an Automation. **Verified by running it twice live** against a local
   test instance of the backend with `dryRun: true`/`false`: it genuinely
   found real pending work from other sessions, synced it into Open Work,
   correctly excluded `InProgress` items from the reminder, and (in the
   non-dry run) correctly skipped sending a message because no
   `creatorSessionId` was supplied in that ad-hoc test call.

## Exactly how `/pending` works

**What's real vs. convention**: Copilot CLI's `/agent` is a genuine,
documented platform slash command for selecting a custom agent
(`docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-custom-agents-for-cli`).
There is **no separate platform mechanism for an arbitrary custom slash
command like `/pending`** - custom agents are selected via `/agent`, by
explicit instruction ("use the daily-driver agent"), by inference, or via
`--agent`. So `/pending` as a literal trigger word is a **convention I've
implemented in the agent's own instructions**, not a platform feature: once
you're talking to the Daily Driver agent, typing the literal text `/pending`
is interpreted by its instructions as "give me the next item," because its
instructions explicitly say so. It is not a CLI-level slash command.

**How to actually invoke it, day to day:**

1. In an interactive Copilot CLI session (or this app), type `/agent` and
   choose **Daily Driver** - or just start a new session/chat and say
   "use the Daily Driver agent."
2. Type `/pending` (or ask "what's next").
3. The agent calls `GET /api/workitems/next` and replies with just the single
   top item (or "nothing pending" if there isn't one) - no list, no choices,
   by design (ADHD-friendly: one concrete thing, not a menu).

This also works via `copilot --agent daily-driver --prompt "/pending"` from a
terminal, since `daily-driver` is the agent's ID (derived from the file name).

## How the hourly reminder works, and what you need to do once

The workflow (`daily-driver-hourly-sweep`) does, every hour:

1. **Extract** - a sub-agent scans other in-flight Copilot sessions for
   genuinely open work (not finished/idle sessions).
2. **Sync** - creates what it finds in Open Work as `WorkItem`s via the
   real HTTP API (sessions already tracked are left untouched, via `externalKey`).
3. **Check** - queries Open Work for `Critical`/`Warning` items whose status
   is `Backlog` or `Blocked` (explicitly **excludes** `InProgress` and `Done`
   per your instruction - if you've started it, you won't be nagged about it).
4. **Remind** - if any are found, a sub-agent sends you a short, calm,
   non-judgment reminder via `send_session_message` to a specific session id,
   listing the items and suggesting exactly one concrete next action.

**How you'll actually see the reminder**: as a message delivered into a
specific Copilot session (the one that originally kicked off this build,
session id `93c73344-deb8-47f1-8ed6-11ad73db3d4c`, baked into the scheduled
prompt as `creatorSessionId`). This was the most reliable in-product delivery
channel available - there is no OS-level push/toast tool in my toolset.
**Limitation**: this only reaches you if that specific session is still open/
visible to you; if you archive or lose track of it, reminders stop being
visible there. If you'd rather reminders land somewhere else (e.g. a specific
chat you keep pinned), update the `creatorSessionId` value in the Automation
(Automations view) to that session's id.

**Enabling**: the Automation ("Daily Driver hourly sweep") was drafted via
the review dialog and has since been saved and enabled in the Automations
view. Its prompt reads the workflow source from
[Canonical workflow source](#canonical-workflow-source) below on every run, so
changing the workflow only means editing that section (on the branch checked
out in the local `open-work` clone - the Automation runs there).

**Design workaround worth knowing about**: dynamic workflows authored via
`dynamic_workflows_manage` are documented as "extension-authored,
session-scoped," and it's unverified whether a workflow registered in *this*
session persists and is runnable from the *brand-new* session a scheduled
Automation spawns each hour. Rather than rely on unverified cross-session
persistence, the Automation reads the **full workflow source** from this file
and re-authors it (idempotently - `dynamic_workflows_manage` author
is safe to repeat) at the start of every hourly run, before calling it. This
is slightly wasteful (re-registers an already-identical workflow every hour)
but removes the persistence risk entirely.

## Authentication

Every `/api/workitems*` and `/api/auth/*` route now requires a bearer token
(`Authorization: Bearer <jwt>`), except `GET /api/auth/status`. This was added
so the live app isn't reachable by anyone who finds the URL.

- **The owner locks the app with a PIN**, not a password: on a blank install,
  the web UI shows a "choose a PIN" screen (`POST /api/auth/setup`); on every
  later visit it validates the JWT already in `localStorage` and only demands
  the PIN (`POST /api/auth/login`) when that token is absent or invalid. Setup
  and login both return a JWT that's valid for 365 days - deliberately
  long-lived, since this app is driven by an unattended hourly sweep as much
  as a browser, and a silently-expiring token would reintroduce the exact "it
  stops working and nobody notices" failure mode this whole project exists to
  avoid.
- **The browser** stores its token in `localStorage` and attaches it to every
  request; logging out just clears it client-side (the token itself isn't
  revoked server-side - there's no server-side session to revoke).
- **The agent persists its own copy of that same kind of token** in a
  user-scoped environment variable, `OPEN_WORK_API_TOKEN`, set with
  `[Environment]::SetEnvironmentVariable("OPEN_WORK_API_TOKEN", "<token>",
  "User")` so it survives across terminals/sessions on this machine. The
  stored value should be the raw JWT; the hourly Automation normalizes it
  before constructing the header. Every
  HTTP call this agent makes to Open Work - the `/pending` lookup, general
  querying, and the hourly workflow's `fetch` calls - must send
  `Authorization: Bearer $env:OPEN_WORK_API_TOKEN`.
- **If a call comes back `401`**, the token is missing, malformed, or the
  signing key was rotated: tell the user plainly, don't guess at a fix. Fixing
  it means logging in again (PIN) to mint a fresh token and re-persisting it
  to the env var - the user has to supply the PIN; the agent cannot recover
  or guess it.
- **Lost-PIN recovery** is a passkey (WebAuthn/FIDO2)-gated wipe, registered
  once while already logged in (`POST /api/auth/recovery/register-options`
  → `/register`), and exercised from the login screen's "Forgot your PIN?"
  link (`/recovery/assertion-options` → `/recovery/wipe`). There is
  deliberately no gentler recovery path: a single-user, self-hosted app has
  no password-reset email to send anywhere, so the only alternative to "prove
  you hold a registered authenticator" is "lose everything and start over."
  Successful recovery wipes every `WorkItem`/push subscription and the PIN
  itself, returning the install to its blank-install state.

## Assumptions made (please confirm/adjust)

- **Open Work is the backing store for "pending workloads in flight with
  Copilot."** Per your own note, this was a working assumption I ran with:
  extracted to-dos are synced in as real `WorkItem`s, not kept in a separate
  system.
- **`apiBaseUrl`**: the Automation points at the live Azure Container Apps
  deployment (`https://openwork-app.delightfulpebble-ad1c96d3.uksouth.azurecontainerapps.io`).
  If the API is unreachable the run logs that and stops quietly.
- **Severity inference for extracted items** defaults to conservative
  mapping: "stuck/blocking/awaiting your decision" → `Critical`; "should
  happen soon" → `Warning`; everything else → `Normal`/`Acceptable`. The
  sub-agent is instructed to be conservative and only include items it has
  real evidence are still open.
- **Reminder delivery session** defaults to the creator session id from this
  build. You may want to repoint it (see above).

## Not fully automated / flagged limitations

- Cross-session persistence of the dynamic workflow registration across the
  Automation's fresh scheduled sessions is unverified; worked around by
  re-authoring it every run (see above) rather than assumed to be safe.
- No true OS-level push/toast notification exists in my toolset; the reminder
  is delivered as an in-app session message, which only surfaces if you still
  have that session open/visible.
- The workflow resolves authentication internally: an explicit
  `args.authToken` override wins, then the process environment, then (on
  Windows) the user-scoped `OPEN_WORK_API_TOKEN` value. It never treats the
  literal string `$env:OPEN_WORK_API_TOKEN` as a token, and strips an optional
  leading `Bearer ` before constructing the header. If the value is missing
  or malformed, the run stops before extraction rather than sending an
  invalid authorization header. Re-login (PIN) and update the env var to fix
  a missing or invalid token.

## Canonical workflow source

This is the source of truth for `daily-driver-hourly-sweep`. The hourly
Automation reads this `meta` and `run` and re-authors the workflow with
`dynamic_workflows_manage` (`operation: "author"`) before each run.

### meta

```json
{
  "name": "daily-driver-hourly-sweep",
  "description": "Daily Driver hourly reminder sweep: extracts pending to-dos from in-flight Copilot sessions via a sub-agent (one per session), upserts them into Open Work as WorkItems (de-duplicated via externalKey copilot-session:<sessionId>), then checks for Critical/Warning items that are not InProgress and reminds the user via a sub-agent sending a session message. All Open Work API calls send Authorization: Bearer <authToken>. args: { apiBaseUrl: string (Open Work API base URL), authToken: string (bearer token for the Open Work API, from the OPEN_WORK_API_TOKEN env var), creatorSessionId?: string (session to message with the reminder), dryRun?: boolean (skip sending the real reminder message, just log it) }.",
  "phases": [
    { "title": "Extract", "detail": "Sub-agent scans other in-flight Copilot sessions for genuinely pending work" },
    { "title": "Sync", "detail": "Upsert extracted items into Open Work as WorkItems" },
    { "title": "Check", "detail": "Query Open Work for Critical/Warning items that are not already InProgress" },
    { "title": "Remind", "detail": "Sub-agent delivers an ADHD-friendly reminder for any such items found" }
  ],
  "argsSchema": {
    "type": "object",
    "required": ["apiBaseUrl"],
    "properties": {
      "apiBaseUrl": { "type": "string" },
      "authToken": { "type": "string" },
      "creatorSessionId": { "type": "string" },
      "dryRun": { "type": "boolean" }
    }
  }
}
```

### run

```js
async ({ args, log, phase, agent, step, signal }) => {
  const apiBaseUrl = String(args.apiBaseUrl || "").replace(/\/$/, "");
  let rawAuthToken = String(args.authToken || "").trim();
  if (!rawAuthToken && typeof process !== "undefined") {
    rawAuthToken = String(process.env.OPEN_WORK_API_TOKEN || "").trim();
  }
  if (!rawAuthToken && typeof process !== "undefined" && process.platform === "win32") {
    try {
      const { execFile } = await import("node:child_process");
      const { promisify } = await import("node:util");
      const execFileAsync = promisify(execFile);
      const result = await execFileAsync(
        "powershell",
        [
          "-NoProfile",
          "-NonInteractive",
          "-Command",
          "[Environment]::GetEnvironmentVariable('OPEN_WORK_API_TOKEN', 'User')"
        ],
        { windowsHide: true, signal }
      );
      rawAuthToken = String(result.stdout || "").trim();
    } catch (err) {
      log(`Could not read the user-scoped OPEN_WORK_API_TOKEN: ${String(err && err.message || err)}`);
    }
  }
  const authToken = rawAuthToken.replace(/^Bearer\s+/i, "").trim();
  const authHeaders = authToken ? { Authorization: ["Bearer", authToken].join(" ") } : {};
  const creatorSessionId = args.creatorSessionId || null;
  const dryRun = !!args.dryRun;

  if (!/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(authToken)) {
    log("Open Work sync skipped: OPEN_WORK_API_TOKEN is missing or is not a valid JWT. Log in with the PIN, then persist the raw token in the user-scoped environment variable.");
    return { synced: [], remindedCount: 0, items: [], authError: true };
  }

  phase("Extract");
  const extraction = await agent(
    "You are helping the Daily Driver ADHD-support agent find genuinely pending, actionable work the user has in flight across GitHub Copilot sessions OTHER than this workflow run (also ignore any session that is itself a 'Daily Driver hourly sweep' run). " +
    "Use list_sessions_and_chats and get_sessions_status to see live session state (busy/idle, pending plan approvals, awaiting_input questions). " +
    "Use session_store_sql to look for sessions with open/unresolved todos, an unresolved pending_plan, or awaiting_input, as evidence of genuinely open work - not sessions that look finished or idle with no loose ends. " +
    "Do not include work that is already clearly completed, merged, or closed. Be conservative: only include an item if you have real evidence it is still open. " +
    "Return at most ONE item per session - the single most important open thing in that session - and set sessionId to that session's id exactly as returned by list_sessions_and_chats (it is used to de-duplicate across hourly runs). " +
    "For each item, decide: " +
    "- severity: 'Critical' if it is blocking, urgent, or the session is stuck awaiting the user's input/decision; 'Warning' if it should happen soon but is not urgent; 'Normal' otherwise. " +
    "- status: 'InProgress' if the session shows active work currently happening on it; 'Blocked' if it is stuck awaiting something (e.g. awaiting_input); otherwise 'Backlog'. " +
    "Return at most 10 of the most important items as JSON: { \"items\": [ { \"sessionId\": string, \"title\": string, \"body\": string, \"severity\": \"Critical\"|\"Warning\"|\"Normal\", \"status\": \"Backlog\"|\"InProgress\"|\"Blocked\" } ] }. " +
    "title should be short and concrete (e.g. reference the session name/PR/issue). body is one or two sentences of context, written plainly with no judgment language.",
    {
      label: "extract-pending-work",
      schema: {
        type: "object",
        required: ["items"],
        properties: {
          items: {
            type: "array",
            items: {
              type: "object",
              required: ["sessionId", "title", "severity", "status"],
              properties: {
                sessionId: { type: "string" },
                title: { type: "string" },
                body: { type: "string" },
                severity: { type: "string", enum: ["Critical", "Warning", "Normal"] },
                status: { type: "string", enum: ["Backlog", "InProgress", "Blocked"] }
              }
            }
          }
        }
      }
    }
  );
  const items = (extraction && Array.isArray(extraction.items))
    ? extraction.items.filter((i) => i && i.sessionId && i.title).slice(0, 10)
    : [];
  log(`Extracted ${items.length} candidate item(s) from in-flight sessions.`);

  phase("Sync");
  const severityToCategory = { Critical: "Critical", Warning: "Warning", Normal: "Acceptable" };
  const severityToPriority = { Critical: 10, Warning: 5, Normal: 1 };
  const synced = await step("sync-items-v2", async () => {
    const results = [];
    for (const item of items) {
      // externalKey makes the POST idempotent: the API returns the existing item (200)
      // instead of creating a duplicate when this session was already synced.
      const body = {
        title: item.title,
        body: item.body || "",
        priority: severityToPriority[item.severity] || 1,
        status: item.status || "Backlog",
        manualCategory: severityToCategory[item.severity] || "Acceptable",
        externalKey: `copilot-session:${item.sessionId}`,
      };
      if (item.severity && item.severity !== "Normal") {
        body.requiresActionAt = new Date().toISOString();
      }
      try {
        const res = await fetch(`${apiBaseUrl}/api/workitems`, {
          method: "POST",
          headers: { "Content-Type": "application/json", ...authHeaders },
          body: JSON.stringify(body),
          signal,
        });
        results.push({ title: item.title, ok: res.ok, created: res.status === 201, httpStatus: res.status });
      } catch (err) {
        results.push({ title: item.title, ok: false, created: false, error: String(err && err.message || err) });
      }
    }
    return results;
  });
  const createdCount = synced.filter((r) => r.created).length;
  const existingCount = synced.filter((r) => r.ok && !r.created).length;
  const unauthorizedCount = synced.filter((r) => r.httpStatus === 401).length;
  log(`Synced ${synced.filter((r) => r.ok).length}/${synced.length} item(s): ${createdCount} new, ${existingCount} already tracked.`);
  if (unauthorizedCount > 0) {
    log(`${unauthorizedCount} sync call(s) returned 401 - authToken is missing or invalid; re-login and refresh OPEN_WORK_API_TOKEN.`);
  }

  phase("Check");
  const pending = await step("check-pending-v1", async () => {
    const url = `${apiBaseUrl}/api/workitems?category=Critical&category=Warning&status=Backlog&status=Blocked`;
    try {
      const res = await fetch(url, { headers: authHeaders, signal });
      if (!res.ok) return [];
      return await res.json();
    } catch {
      return [];
    }
  }, { volatile: true });

  phase("Remind");
  let remindedCount = 0;
  if (Array.isArray(pending) && pending.length > 0) {
    remindedCount = pending.length;
    const summaryLines = pending.map((p) => `- [${p.category}] ${p.title}`).join("\n");
    log(`Pending Critical/Warning item(s) not in progress:\n${summaryLines}`);
    if (!dryRun && creatorSessionId) {
      await agent(
        "You are the Daily Driver ADHD-support agent. Send ONE short, calm, non-judgmental reminder message using send_session_message to session id '" +
        creatorSessionId + "'. " +
        "The message must: list these pending items (title + one line of context each), explicitly note they are not already in progress, and end with exactly one small concrete suggested next action (pick the single highest-priority item). " +
        "No shame/judgment language. Keep it short. Items:\n" + summaryLines,
        { label: "deliver-reminder" }
      );
    } else {
      log(dryRun ? "dryRun set - skipping real reminder message." : "No creatorSessionId provided - skipping real reminder message.");
    }
  } else {
    log("No Critical/Warning items pending outside of in-progress work. Nothing to remind.");
  }

  return {
    synced,
    remindedCount,
    items: Array.isArray(pending) ? pending.map((p) => p.title) : [],
  };
}
```
