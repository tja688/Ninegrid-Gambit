---
name: table-nine-effect-batches
description: Plan, analyze, and implement small verified TableNine/NineGrid effect-system batches in the Unity 6 QFramework project. Use when the user asks to "落地批次1/2/3", continue P5/P6/R3/R4/R5 repairs, burn down PendingAtom content, migrate TableNine content to Luban, classify help-card/relic/player-skill/monster-skill effects, design reusable effect atoms, or make a full/accurate/verified development plan for the Ninegrid Gambit effect DSL.
---

# TableNine Effect Batches

## Purpose

Use this skill to turn TableNine's large effect backlog into small implementation batches that follow the existing architecture: deterministic GameAction pipeline, Stat/Rule/Triggered effect separation, data-driven DSL, Luban content, and Unity EditMode validation.

The user cares about three outcomes:

- **Full**: know what exists, what works, what is pending, what is missing, and what must be migrated.
- **Accurate**: classify each effect by its real semantic shape before choosing atoms or code locations.
- **Verified**: land a small slice, prove it with tests/console, update the plan, then continue.

## Required Context

Before planning or implementing a batch:

1. Read project rules at `rules.md`; never edit `.unity` files by hand.
2. Read `Assets/Notes/TableNine-Effect-Batch-Development-Plan.md` if present.
3. Read `references/current-map.md` and `references/batch-roadmap.md`.
4. Read `references/effect-taxonomy.md` when classifying effects, choosing atom boundaries, or touching `PendingAtom`.
5. Use CodeGraph first for code architecture/symbol lookup. Use shell `rg` for document inventory and exact pending lists.

Key project documents to consult only as needed:

- `Assets/Notes/九宫牌局权威顶层架构设计.md`
- `Assets/Notes/P5-P6-QA-Report.md`
- `Assets/Notes/Luban-Content-Pipeline-Setup.md`
- `Assets/Docs/九宫牌局/00-核心概念/效果.md`
- `Assets/Docs/九宫牌局/01-机制规则/效果类型隔离规范.md`
- `Assets/Docs/九宫牌局/07-数据/帮助卡数据.md`
- `Assets/Docs/九宫牌局/07-数据/遗物数据.md`
- `Assets/Docs/九宫牌局/04-技能/玩家技能.md`
- `Assets/Docs/九宫牌局/04-技能/怪物技能.md`

## Batch Command Handling

When the user says "落地批次 N":

1. Refresh the facts for that batch: pending IDs, related docs, current atoms, tests, and Luban rows.
2. Restate the selected batch scope in 3-6 concrete bullets before editing.
3. Keep the slice small. Prefer one reusable capability plus 2-5 representative content conversions.
4. Add or update validation first or alongside the implementation.
5. Run the narrowest relevant tests, then the full `NineGrid.Core.Tests` EditMode suite when code changed.
6. Update `Assets/Notes/TableNine-Effect-Batch-Development-Plan.md` with completed items, remaining pending count, and next recommended batch.

If the requested batch number is ambiguous, use `references/batch-roadmap.md` as the source of truth.

## Implementation Rules

- Preserve QFramework layering. Controllers mutate state only through Commands; Systems/Commands use `this.GetModel`, `this.GetSystem`, `this.SendEvent`, etc.
- Every state mutation must flow through `GameAction` or an existing Model method intentionally called by an Action/factory.
- Classify effects into exactly one primary lane before implementation: `Modifier`, `RuleModifier`, or `Triggered`.
- Build common atoms before special content. If multiple pending effects need the same capability, implement the capability once and convert a small representative set.
- Extend schema validation whenever adding a new atom, field, enum, or range rule.
- Keep `TableNineContentCatalog` as the hardcoded truth until the roadmap explicitly migrates that slice to Luban.
- Do not make `PendingAtom` disappear by weakening validation or skipping activation. A converted effect needs executable DSL and tests.

## Verification

For any C# or Unity asset change:

- Prefer Unity MCP: refresh/import or wait for compile, read Console errors, then run EditMode tests for `NineGrid.Core.Tests`.
- If Unity MCP is unavailable and the editor is closed, use `.\Assets\Notes\CI\run-core-tests.ps1`.
- Run `.\Assets\Notes\CI\check-core-guards.ps1` for architecture guard changes.
- For Luban data/table changes, run `.\Assets\Tools\Luban\gen_table_nine.ps1`, then run Luban/content tests.

Do not mark a batch done unless the relevant test names and pending-count movement are reported.

## Output Shape

For batch work, finish with:

- changed files and why they matter,
- exact effects/capabilities converted,
- tests/validation run and result,
- remaining known risk,
- the next smallest batch.
