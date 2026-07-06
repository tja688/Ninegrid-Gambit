---
name: table-nine-effect-landing
description: >-
  Land TableNine/NineGrid effects end-to-end on the existing DSL foundation:
  classify help-card/relic/player-skill/monster-skill semantics, compose or extend
  atoms, update TableNineContentCatalog, sync Luban, and verify with EditMode tests.
  Use when the user asks to 落地/实现 a specific effect, analyze whether an effect
  can be composed from existing atoms, add a new atom or RuleModifier, or fix effect
  DSL validation/runtime behavior. Not for batch-roadmap planning.
---

# TableNine Effect Landing

Long-term operational skill for landing individual effects on the post-batch-8 foundation.

## Outcomes

Every landing task should produce:

- **Accurate classification** — exactly one primary lane: `Modifier`, `RuleModifier`, or `Triggered`.
- **Minimal correct diff** — compose existing atoms first; extend capability only when composition fails.
- **Verified behavior** — schema validation, narrow content test, CI gates green.

## Required Context

Before landing or analyzing:

1. Follow project rules in `.cursor/rules/`; never hand-edit `.unity` files.
2. Read `references/baseline-snapshot.md` and refresh pending/atom counts if the task touches catalog.
3. Read `references/effect-taxonomy.md` before classifying or choosing atoms.
4. Read `references/landing-workflow.md` (full stack) or `references/compose-analysis.md` (analysis only).
5. Use CodeGraph for symbol/architecture lookup. Use `rg` for pending IDs, design text, catalog rows.

Consult only as needed:

- `Assets/Notes/九宫牌局权威顶层架构设计.md`
- `Assets/Notes/TableNine-Effect-Batch-8-Final-Landing-Report.md`
- `Assets/Docs/九宫牌局/01-机制规则/效果类型隔离规范.md`
- `Assets/Docs/九宫牌局/00-核心概念/效果.md`
- Container data docs under `Assets/Docs/九宫牌局/07-数据/` and `04-技能/`

## Two Modes

### Mode A — Full-stack landing (default)

Trigger: user says **落地** / **实现** `skill.xxx`, `help.xxx`, `relic.xxx`, etc.

Follow `references/landing-workflow.md` end-to-end:

1. Intake design semantics + container rules
2. Classify (output classification before editing)
3. Gap analysis — compose vs extend atoms/rules/actions
4. Implement — catalog-first (`TableNineContentCatalog.cs`)
5. Sync Luban — export + `gen_table_nine.ps1`
6. Verify — `references/verification-checklist.md`
7. Report — structured summary (see below)

**Content source of truth:** `Assets/Scripts/NineGrid.Content/Catalog/TableNineContentCatalog.cs`  
Then export via `TableNine/Content/Export Hardcoded Catalog To Luban Datas` or `TableNineLubanDataExporter`, then run `.\Assets\Tools\Luban\gen_table_nine.ps1`.

**Pending conversion contract:** replace `Pending(...)` with `Impl(...)` + executable JSON. Never mark Implemented with empty JSON, weakened validation, or skipped activation tests.

**Compose-first rules** (see `references/effect-taxonomy.md`):

- Constant damage/heal → `DealDamage` / `Heal` + `amount`
- Dynamic values → `value` expression field, not a new action atom
- Filtered targets → `FilteredCards` / `SelectedCards` + filters
- Rule changes → `AddRuleModifier` + decision-point query
- Composite logic → `Sequence` / `Conditional` / `WeightedRandom` / `Repeat`
- Multi-slot triggers → multiple effect rows (e.g. `skill.air_strike.slot1/3/7/9`)

If estimated new Core surface exceeds ~3 files (new atom + RuleModifier + GameAction), state the slice before implementing unless the user already scoped a single effect.

### Mode B — Compose analysis only

Trigger: user says **分析** / **能否拼** / **怎么拼** for an effect.

Follow `references/compose-analysis.md` only. **Do not modify the repo**, run Luban, or change pending gates.

## Implementation Rules

- Preserve QFramework layering. State changes only through `GameAction` or Model methods called from Actions.
- Classify into exactly one primary state before coding.
- Extend `EffectAtomSchemas` whenever adding atoms, fields, enums, or range rules.
- New `GameAction` → implement `Apply()` + `EmitEvents()`, enqueue via pipeline only.
- Presentation must not own logic; Core must not reference Unity UI APIs.
- Do not auto-expand catalog beyond the requested effect id unless the user asks.

## Verification

For C# or catalog changes:

- Prefer Unity MCP: refresh/import, read Console, run `NineGrid.Core.Tests` EditMode.
- Fallback: `.\Assets\Notes\CI\run-core-tests.ps1`
- Architecture guards: `.\Assets\Notes\CI\check-core-guards.ps1`
- Luban: `.\Assets\Tools\Luban\gen_table_nine.ps1` after export

Full checklist: `references/verification-checklist.md`

## Report Shape

Finish landing tasks with:

- Classification (三态 + lifecycle + compose vs extend decision)
- Changed files and why
- Converted effect ids
- Tests run and results
- Remaining gap if still blocked
- Known risks (e.g. Presentation target selection not wired)

Only write `Assets/Notes` reports when the user explicitly asks.

## Reference Index

| File | Purpose |
|:--|:--|
| `references/baseline-snapshot.md` | Current counts, architecture contract, refresh commands |
| `references/effect-taxonomy.md` | Three-state classification, capability clusters, accuracy rules |
| `references/atom-catalog.md` | Registered atoms, typical JSON fields, compose patterns |
| `references/landing-workflow.md` | Full-stack 7-step loop with decision tree |
| `references/compose-analysis.md` | Analysis-only output template + worked example |
| `references/file-touch-map.md` | Which files to edit per change type |
| `references/verification-checklist.md` | Fixed validation steps and failure triage |
| `references/pending-gaps.md` | Read-only snapshot of 32 pending effects by capability cluster |

## Out of Scope

- Batch N roadmaps (`落地批次1/2/...`) — archived; use per-effect landing instead.
- Design-doc full inventory vs runtime catalog — only on explicit user request.
- Automatic pending-count note maintenance — refresh on demand via `rg`, not a standing task.
