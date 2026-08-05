# OpenCode Ticket Runner Design

## Goal

Port the practiced Cursor `ticket-runner` workflow to OpenCode without changing
its operational contract: ordered GitHub Issue queues, one fresh agent run per
ticket, bounded supervision, one rescue decision, and machine-readable local
run history.

## Design

The project-local skill lives at `.agents/skills/ticket-runner/`, which OpenCode
discovers for project-compatible skills. Its PowerShell entry point is separate
from the skill instructions so the workflow remains repeatable and does not
depend on the current chat remembering the state machine.

The runner invokes `opencode run` once per ticket with `--dir`, the dedicated
`ticket-implementer` agent, `--format json`, and no continuation/session flags.
The worker receives the repository's existing `AGENTS.md` rules and a prompt
that names exactly one Issue. A `ticket-doctor` agent is used at most once when
the worker is unhealthy; it is read-only and returns one of
`kill_restart`, `continue_wait`, or `pause_user`.

The runner retains the existing defaults: 30-minute ticket budget, 10-minute
output idle threshold, one intervention, one restart attempt, 15-minute grace
after `continue_wait`, strict serial queueing, optional strict Issue closure,
and a maximum of three local run archives. Runtime state is written under
`.opencode/ticket-runner/`, which is ignored by Git.

## Model Configuration

The project default model is the canonical Models.dev identifier
`deepseek/deepseek-v4-flash`. The configured fallback is
`alibaba/qwen3.7-max`. The runner only switches to the fallback after a
provider/model failure; ordinary implementation failures are not hidden by a
model retry.

`opencode.json` configures these models and the two dedicated agents. It does
not make `ticket-implementer` the default interactive agent, disable subagents
globally, or add an SDK dependency.

## Non-Goals

- Replacing the existing Cursor runner.
- Building a permanent OpenCode Server/SDK service.
- Automatically closing or merging Issues from the host process.
- Adding a new GitHub workflow or changing the repository's issue conventions.
