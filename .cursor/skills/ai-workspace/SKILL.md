---
name: ai-workspace
description: >-
  Coordinate multiple AIs on one Unity workspace via a PowerShell CLI: claim
  slots before work, gate Editor restart when others are active, and run
  EditMode tests under a shared mutex with wait-and-share. Use when starting
  agent work in this repo, before quitting/relaunching Unity, before
  EditMode/run_tests, or when the user mentions AI workspace / claim / test lock.
---

# AI workspace coordinator

Multiple agents often share this repo and one Unity Editor. Use the CLI below
instead of ad-hoc `unity command run_tests` or unguarded Editor restarts.

## Entry

From the Unity project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".cursor/skills/ai-workspace/scripts/ai-workspace.ps1" <command> [options]
```

Help: no args, `help`, `-h`, or `--help` (also after a subcommand).

## Required workflow

1. **Start work** — pick a short `--agent` id (e.g. `cursor-main`, `issue-42`) and claim:

```powershell
... ai-workspace.ps1 claim --agent <id> [--note "..."]
```

2. **Normal Unity CLI** (`recompile`, `console`, `eval`, scene Pipeline) — allowed while claimed.

3. **EditMode tests** — **only** via:

```powershell
... ai-workspace.ps1 test --agent <id> [--filter <Name>]
```

Do **not** call `unity command run_tests` directly.

4. **Quit / relaunch Editor** (`unity-automated-launch`, kill Unity, Safe Mode recovery relaunch) — first:

```powershell
... ai-workspace.ps1 gate-restart --agent <id>
```

Exit `0` → proceed. Exit `2` → other live claims; **do not** restart; wait or coordinate.

5. **End work** (or leave the session):

```powershell
... ai-workspace.ps1 release --agent <id>
```

Long tasks: re-run `claim` periodically (heartbeat TTL 30 min).

## Commands

| Command | Purpose |
|---------|---------|
| `status` | Live claims + EditMode lock / last result |
| `claim --agent <id>` | Register / refresh claim |
| `release --agent <id>` | Drop claim |
| `gate-restart --agent <id>` | Deny restart if **other** claims exist |
| `test --agent <id> [--filter …]` | Mutex EditMode run; default wait+share if busy |
| `test-status` | Lock / last result only |

Useful flags: `--json`, `--project-path <dir>`, `test --no-wait`, `test --wait-timeout-sec N`.

Exit codes: `0` ok · `1` usage/error · `2` gate deny / busy+`--no-wait` · `3` test failure (shared failures also `3`, look for `shared=true`).

## Semantics (fixed)

- **Claims (1A):** many agents may claim. Aggressive Editor ops blocked while **any other** claim is live.
- **EditMode (2A):** one runner. If busy, default waits and **shares** the result when `--filter` matches; `--no-wait` exits `2` with owner info.

State (gitignored): `.cursor/ai-workspace/state.json` and `logs/`.

## Aggressive ops (blocked by gate-restart)

- Quit / kill / relaunch Unity Editor
- `unity-automated-launch` / any `-automated` restart path
- Anything that drops the shared Pipeline session for other agents

Not gated: ordinary `unity command` compile/console/eval/scene edits.

## Related

- Unity CLI overview: `docs/agents/unity-cli.md`
- Editor launch: `.cursor/skills/unity-automated-launch/`
