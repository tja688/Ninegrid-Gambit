---
name: ticket-runner
description: Use when serially landing GitHub Issues from a parent Spec, ordered task list, explicit issue list, or ready-for-agent queue in OpenCode, especially for headless Unity work.
compatibility: opencode
---

# OpenCode Ticket Runner

This is the project's serial GitHub Issue workflow. It is an orchestrator, not
a request to implement the ticket in the current chat. Each ticket gets a
fresh `opencode run`; the host process owns queue order, time budgets, logs,
and whether the next ticket may start.

## Entry

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".agents/skills/ticket-runner/scripts/ticket-runner.ps1" doctor
powershell -NoProfile -ExecutionPolicy Bypass -File ".agents/skills/ticket-runner/scripts/ticket-runner.ps1" plan
powershell -NoProfile -ExecutionPolicy Bypass -File ".agents/skills/ticket-runner/scripts/ticket-runner.ps1" run
```

Use `plan` before `run` when checking a non-default parent or queue. The
default project queue is `defaultParent` in `.opencode/ticket-runner.config.json`
(currently the child task list of Spec `#114`). Explicit `--parent`, `--issues`,
or `--label` overrides it.

## Queue and execution rules

1. Resolve an explicit issue list first, then a parent task list, then tracked
   Issues, then an open `ready-for-agent` label queue.
2. Preserve the resolved order. Never start the next ticket while the current
   one is failed or paused.
3. Start one `opencode run` per ticket with `ticket-implementer`, `--format
   json`, and no `--continue`, `--session`, or `--fork`.
4. The worker must not ask questions, start subagents, use worktrees, or begin
   another ticket. Missing decisions become `TICKET_STATUS: NEEDS_HUMAN`.
5. A worker is complete only when its process exits successfully and its final
   status is `TICKET_STATUS: IMPLEMENTED`. The host then checks the Issue state;
   strict closure is opt-in via `--strict-close`.

## Supervision

Defaults are deliberately the practiced Cursor values: 30 minutes per ticket,
10 minutes without output, eight malformed JSON event lines as a hard parse
signal, one `ticket-doctor` intervention, one restart at most, and 15 minutes
of grace after `continue_wait`. A missing or invalid doctor decision pauses the
queue for a human; it never triggers recursive rescue agents.

`ticket-doctor` is read-only. Its only decisions are `kill_restart`,
`continue_wait`, and `pause_user`. Provider/model failures may retry once with
the configured Qwen fallback; ordinary implementation failures do not silently
switch models.

## Unity-specific requirements

The worker follows `AGENTS.md`: no worktrees, Unity CLI for Editor operations,
`ai-workspace` for claims and EditMode test locking, and `gate-restart` before
restarting Unity. The runner itself must not run business tickets in the
current session.

## Runtime evidence

Machine-written logs are under `.opencode/ticket-runner/runs/<run-id>/` and are
ignored by Git. Inspect `state.json`, each ticket's `stdout.log`, `stderr.log`,
`events.ndjson`, and `meta.json` after a failure or pause.

## Commands

```powershell
# First child in the parent task list only
... ticket-runner.ps1 run --parent 114 --once

# Start at a particular child and continue through the queue
... ticket-runner.ps1 run --parent 114 --from 118

# Explicit order, with the primary model overridden for this batch
... ticket-runner.ps1 run --issues 115,116,117 --model deepseek/deepseek-v4-flash

# Inspect machine state
... ticket-runner.ps1 status --json
```
