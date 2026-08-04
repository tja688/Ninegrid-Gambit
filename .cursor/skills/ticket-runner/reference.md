# Ticket Runner — reference

## Default model

Built-in / example config default: **`cursor-grok-4.5-high`**.

Published Cursor rename (forum): `grok-4.5-xhigh` → `cursor-grok-4.5-high`.

Live check (requires CLI login):

```powershell
& "$env:LOCALAPPDATA\cursor-agent\agent.cmd" login
& "$env:LOCALAPPDATA\cursor-agent\agent.cmd" --list-models
```

`doctor` validates slug format always; when authenticated it tries to find the id in `--list-models`.

## Supervised worker

- `Start-Process -RedirectStandardOutput/Error` to files (no console pipe inherit).
- Poll loop: process exit, file growth (idle), auth/ask/parse heuristics, 30m budget.
- One escape-hatch intervention agent; decisions: `kill_restart` | `continue_wait` | `pause_user`.
- No recursive intervention; second unhealthy → exit 3 pause for human.

## Machine logs

```
.cursor/ticket-runner/
  state.json
  runs/
    index.json          # max 3
    <runId>/
      run.json
      tickets/<n>/
        events.ndjson   # code-written only
        stdout.log
        stderr.log
        meta.json
        intervention/   # when triggered
```

Rotation deletes oldest run directory when starting a new `run`.

## Headless prompt wrapper

Every ticket prompt is wrapped with instructions: no questions, no worktree, proceed with defaults.
