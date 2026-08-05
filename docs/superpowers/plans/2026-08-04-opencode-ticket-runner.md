# OpenCode Ticket Runner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the practiced serial GitHub Issue workflow discoverable and runnable from OpenCode.

**Architecture:** Keep the existing Cursor runner untouched. Add a project-compatible `.agents/skills/ticket-runner` skill with a lean PowerShell supervisor that resolves queues through `gh` and starts one fresh `opencode run` per ticket. Add project OpenCode config for the primary/fallback models and dedicated implementer/doctor agents.

**Tech Stack:** OpenCode CLI, PowerShell 5.1+, GitHub CLI, JSON/NDJSON logs, OpenCode config schema.

---

### Task 1: Establish behavior checks

**Files:**
- Create: `.agents/skills/ticket-runner/tests/test-ticket-runner.ps1`

- [ ] **Step 1: Add static contract checks**

The test script should read the skill, runner, and config files and assert the
required discovery path, model IDs, fresh-session flags, queue commands, one
intervention limit, and dangerous-operation denial rules.

- [ ] **Step 2: Run the checks before implementation**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".agents/skills/ticket-runner/tests/test-ticket-runner.ps1"
```

Expected: FAIL because the migrated files do not exist yet.

### Task 2: Add the OpenCode skill and reference

**Files:**
- Create: `.agents/skills/ticket-runner/SKILL.md`
- Create: `.agents/skills/ticket-runner/reference.md`
- Create: `.agents/skills/ticket-runner/config.example.json`

- [ ] **Step 1: Document the OpenCode entry point and exact workflow**

Document `doctor`, `plan`, `run`, `status`, queue precedence, fresh `opencode
run` sessions, the two agents, the existing Unity test lock, and the
`NEEDS_HUMAN`/`PAUSED` behavior. Keep the skill operational rather than
repeating the long Cursor implementation.

### Task 3: Implement the supervisor

**Files:**
- Create: `.agents/skills/ticket-runner/scripts/ticket-runner.ps1`

- [ ] **Step 1: Implement queue resolution and local state**

Support `--parent`, `--issues`, `--label`, `--from`, `--once`, `--model`,
`--workspace`, `--config`, `--strict-close`, `--dry-run`, and `--json`. Resolve
task-list order before tracked Issues, then label order, and retain at most three
run archives.

- [ ] **Step 2: Implement the OpenCode process wrapper**

Start `opencode run --dir <root> --agent ticket-implementer --model <model>
--format json --auto <prompt>` with redirected stdout/stderr. Never pass
`--continue`, `--session`, or `--fork`. Treat a process exit as authoritative
only after scanning the final output for `TICKET_STATUS` and provider errors.

- [ ] **Step 3: Implement bounded supervision and one doctor**

Use the practiced 30-minute budget, 10-minute idle threshold, parse-error
threshold, one `ticket-doctor` decision, one restart maximum, and a paused
state when the decision is missing, ambiguous, or exhausted.

### Task 4: Add project OpenCode configuration

**Files:**
- Create: `opencode.json`
- Modify: `.gitignore`

- [ ] **Step 1: Configure models and agents against the published schema**

Set `model` to `deepseek/deepseek-v4-flash`, `small_model` to
`alibaba/qwen3.7-max`, disable autoupdate and snapshots, and define primary
`ticket-implementer` and read-only `ticket-doctor` agents with bounded steps,
denied questions/tasks where appropriate, and no git push/reset/clean.

- [ ] **Step 2: Ignore runtime artifacts**

Add `.opencode/ticket-runner/` to `.gitignore` without changing existing Cursor
state rules.

### Task 5: Verify and document the boundary

**Files:**
- Modify: `.agents/skills/ticket-runner/tests/test-ticket-runner.ps1`

- [ ] **Step 1: Run static contract tests after implementation**

Expected: PASS with no warnings.

- [ ] **Step 2: Validate JSON and PowerShell syntax**

Run `ConvertFrom-Json` for both JSON files and invoke the runner's `help` and
`status` commands. Run `opencode --version` and `opencode models` if the CLI is
installed; report the absence rather than treating it as a code failure.

- [ ] **Step 3: Check discovery paths and worktree cleanliness**

Confirm `SKILL.md` is under `.agents/skills/ticket-runner/`, the frontmatter
name matches the folder, and only intended files changed.
