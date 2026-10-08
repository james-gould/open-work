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
     `?includeInProgress=true` opts back in if ever needed.
   - `Create`/`Update` accept an explicit `status`; omitting it still works via
     the legacy `isDone` boolean (mapped to `Done`/`Backlog`).
   - A first-ever EF Core migration (`InitialCreate`) was added, since the app
     previously used `Database.EnsureCreated()` with no migration history at
     all. `Program.cs` now calls `Database.Migrate()` on startup.
   - **Verified**: `dotnet build` succeeds; ran the API live against a fresh
     SQLite DB, created Critical+Blocked and Critical+InProgress items via the
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
   sub-agent, priority derived from category).

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
2. **Sync** - upserts what it finds into Open Work as `WorkItem`s via the
   real HTTP API.
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

**One-time manual step you must do (I cannot do this for you):** I drafted
the hourly Automation via `save_workflow` with `user_confirmation: "dialog"`,
which opens the workflow editor for review rather than silently saving it -
confirmed by checking `list_workflows()` immediately after, which returned
empty. **You need to open the Automations view, review the drafted "Daily
Driver hourly sweep" automation, and click Save/Enable.** This is a deliberate
one-click human-in-the-loop checkpoint (I didn't want an agent silently
scheduling an hourly job that pages you without you seeing it first), and I
have no way to click through that dialog myself.

**Design workaround worth knowing about**: dynamic workflows authored via
`dynamic_workflows_manage` are documented as "extension-authored,
session-scoped," and it's unverified whether a workflow registered in *this*
session persists and is runnable from the *brand-new* session a scheduled
Automation spawns each hour. Rather than rely on unverified cross-session
persistence, the Automation's saved prompt embeds the **full workflow source
verbatim** and re-authors it (idempotently - `dynamic_workflows_manage` author
is safe to repeat) at the start of every hourly run, before calling it. This
is slightly wasteful (re-registers an already-identical workflow every hour)
but removes the persistence risk entirely.

## Assumptions made (please confirm/adjust)

- **Open Work is the backing store for "pending workloads in flight with
  Copilot."** Per your own note, this was a working assumption I ran with:
  extracted to-dos are synced in as real `WorkItem`s, not kept in a separate
  system.
- **`apiBaseUrl` placeholder**: the Automation currently points at
  `http://localhost:5080` (the backend's dev default). Since the Azure
  Container Apps deployment (`rg-open-work`) is mid-flight and explicitly out
  of scope for this task, I could not supply a real public URL. **Update the
  `apiBaseUrl` in the Automation once that deployment is live** (or keep
  `localhost:5080` if you intend to keep running Open Work locally and the
  hourly job's own CLI session also runs locally - behavior will differ
  depending on what host the scheduled session actually runs on; this is
  flagged as unverified).
- **`Migrate()` vs. `EnsureCreated()`**: switching to real EF Core migrations
  is the correct long-term fix, but any already-deployed SQLite DB that was
  created via the old `EnsureCreated()` call has no `__EFMigrationsHistory`
  table. If such a DB exists in the Azure deployment, the first
  `Migrate()` call against it will likely fail (tables already exist). This is
  a known, disclosed tradeoff - out of scope to fix here per your instructions,
  but you'll need to either delete/recreate that DB, or manually stamp its
  migration history, before this code runs against it.
- **Severity inference for extracted items** defaults to conservative
  mapping: "stuck/blocking/awaiting your decision" → `Critical`; "should
  happen soon" → `Warning`; everything else → `Normal`/`Acceptable`. The
  sub-agent is instructed to be conservative and only include items it has
  real evidence are still open.
- **Reminder delivery session** defaults to the creator session id from this
  build. You may want to repoint it (see above).

## Not fully automated / flagged limitations

- Enabling the hourly Automation requires your one-time manual click (above) -
  I deliberately used the review dialog rather than silently registering it.
- Cross-session persistence of the dynamic workflow registration across the
  Automation's fresh scheduled sessions is unverified; worked around by
  re-authoring it every run (see above) rather than assumed to be safe.
- No true OS-level push/toast notification exists in my toolset; the reminder
  is delivered as an in-app session message, which only surfaces if you still
  have that session open/visible.
