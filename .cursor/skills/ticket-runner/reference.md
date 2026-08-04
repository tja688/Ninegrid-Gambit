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

## Default queue

Project `.cursor/ticket-runner.config.json` may set `defaultParent` (e.g. `114`).  
Bare `plan` / `run` then resolve that Spec’s child task list (starting at the first child, typically parent+1) and run the **full** open queue serially. `--once` / `--model` / `--issues` are opt-in overrides only.

## Supervised worker

- Prefer `node.exe` + `index.js` over `agent.cmd`.
- PS 5.1 `Start-Process -ArgumentList` **array** joins with bare spaces (breaks `Ninegrid Gambit`); always pass **one** pre-quoted argument string.
- Redirect stdout/stderr to files (no console pipe inherit).
- When process `ExitCode` is `$null`, accept stream-json `{"type":"result","subtype":"success"}` as finished (avoids false FAIL after a clean land).
- Auth / ask heuristics: **soft only** (log `soft_signal`, never open escape-hatch). Real Cursor login failures still surface via idle/budget if the worker is actually stuck.
- Auth text filter (for soft log): Cursor Agent login/token only; ignore Unity Pipeline `401`, thinking, tool stdout.
- Ask text filter (for soft log): assistant-visible text only.
- If worker already exited or stream result success, skip escape-hatch entirely.
- Hard intervention triggers only: stdout idle, ticket budget, stream-json parse errors (≥8).
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
