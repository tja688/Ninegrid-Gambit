# Batch Roadmap (Archived)

**Status:** Archived 2026-06-18. Batch 1–8 closeout complete.

Use **`table-nine-effect-landing`** for per-effect landing and compose analysis.

Historical roadmap preserved below for reference.

---

# Batch Roadmap

This roadmap maps user prompts like "落地批次1" to small verified work. Refresh exact pending lists before editing.

## Universal Micro-Cycle

Every batch follows the same loop:

1. **Refresh**: read rules, current plan note, relevant docs, current code with CodeGraph, and the pending list.
2. **Classify**: write the three-state classification for every effect in scope.
3. **Slice**: choose a reusable capability and 2-5 content conversions.
4. **Validate first**: add schema/unit/content/replay tests that fail for the missing behavior.
5. **Implement**: add atoms/actions/rules/content data using existing project patterns.
6. **Verify**: run architecture guard, targeted tests, full EditMode tests when code changed, and Luban generation when data changed.
7. **Record**: update `Assets/Notes/TableNine-Effect-Batch-Development-Plan.md`.

## Batch 0 - Fact Rebase

Use when the user asks for planning, review, or status before implementation.

Deliverables:

- current counts for implemented/pending/Luban/docs,
- a refreshed list of pending IDs by container,
- changed assumptions since the last note,
- the next recommended implementation batch.

No gameplay code changes unless the user explicitly asks.

## Batch 1 - Baseline Gate and Existing-Atom Quick Wins

Purpose: stop the "everything is a little bit started" feeling by making progress measurable and converting only effects that current atoms can already represent.

Scope:

- Add/adjust tests or helpers that report pending counts by container.
- Keep `Hardcoded` catalog as production truth; treat Luban as sample/parity path.
- Convert 1-3 effects that need no new common capability, or explicitly document that no safe quick-win exists.
- Good candidates: simple stat modifiers or existing `OnNodeStart`/`OnNodeEnd` spawns/heals if current atoms already support the exact semantics.

Acceptance:

- Pending count cannot accidentally increase.
- Converted effects have executable DSL and content tests.
- `P5EffectSystemTests` and `P6ContentLandingTests` remain green.

## Batch 2 - Dynamic Value Expressions v1

Purpose: unlock many damage/heal/stat effects without one-off action atoms.

Scope:

- Implement reusable value expression support for action atoms.
- Keep constant `amount` backward compatible.
- Add schema validation for expression fields.
- Convert 2-5 representative effects.

Suggested conversions:

- `help.fireball.pending`
- `help.impact_tutorial.pending`
- `help.shield_bash_tutorial.pending`
- `skill.thorn_skin.pending`
- `skill.hard.pending`

Acceptance:

- Tests prove constant amount still works.
- Tests prove values can read player/current target/event stats deterministically.
- Converted catalog DSL validates and executes.

## Batch 3 - Target Filters and Movement Control v1

Purpose: unlock target selection, non-player operations, random constrained targets, and movement cards.

Scope:

- Add selected/filtered/random target capability.
- Add reverse rotation or movement metadata only if needed by the selected slice.
- Convert 2-5 movement/target pending effects.

Suggested conversions:

- `help.rotation_wheel.pending`
- `help.swap_card.pending`
- `help.teleport_card.pending`
- `skill.thief_claims.pending`
- `skill.unstable.pending`
- `skill.random_walk.pending`

Acceptance:

- Random target tests use seeded RNG.
- Invalid/no-candidate cases are deterministic and non-crashing.
- Movement still flows through `GameAction` and emits expected events.

## Batch 4 - Lifecycle, Prevention, and Rule Modifiers v1

Purpose: implement effects that modify rules, prevent damage, or require scoped lifetime cleanup.

Scope:

- Add needed `RuleId`s and decision-point queries.
- Add runtime marker/scoped modifier cleanup where appropriate.
- Convert a small group of rule/prevention effects.

Suggested conversions:

- `help.ward_magic_card.pending`
- `relic.gold_armor.pending`
- `skill.taunt.pending`
- `skill.stray_cub.first_strike`
- `skill.first_strike.pending`
- `skill.blessing.pending`
- `help.doubling_tower.pending`

Acceptance:

- Tests cover activation, effect use, deactivation/cleanup, and non-stacking.
- Existing damage formula tests stay green.

## Batch 5 - Reward, Choice, and Content Actions

Purpose: connect effect DSL to reward/choice/content systems without embedding UI in Core.

Scope:

- Add effect actions for offer/grant/reward operations using existing `RewardSystem` and content actions.
- Convert a small group of chest/reward/player-skill effects.
- Keep deterministic choice offers in EventLog; presentation can consume later.

Suggested conversions:

- `help.common_chest_card.pending`
- `help.blue_chest_card.pending`
- `help.golden_chest_card.pending`
- `help.blood_conversion.pending`
- `skill.easy_road.pending`
- `relic.lucky_coin.pending`

Acceptance:

- Tests assert offered reward ids, grant actions, and catalog references.
- No UI dependencies enter `NineGrid.Core`.

## Batch 6 - Counters, Cause Tags, and Recursion Guards

Purpose: unlock "every N times", source-sensitive, and non-recursive chain effects.

Scope:

- Add counter/source/cause metadata where missing.
- Convert a small group of counter-heavy relics/skills.

Suggested conversions:

- kill/remove count relics from `遗物数据.md`,
- use-help-card count relics,
- `skill.learning_growth.pending`,
- flame doubling / no-recursion skills,
- source-specific violence/bone absorption skills.

Acceptance:

- Tests prove counters are per owner/effect when required.
- Tests prove recursive triggers stop where design says they stop.

## Batch 7 - Board Marks and Field Rules

Purpose: unlock battlefield state such as trap slots, blessed slots, adjacency rewrites, and movement locks.

Scope:

- Add board marks or rule modifiers only where they are genuine reusable state.
- Convert a small group of mark/field effects.

Suggested conversions:

- `help.bear_trap.pending`
- trap-grid relics,
- blessed-slot monster skills,
- `skill.range_expand.pending`,
- boss movement-lock skill.

Acceptance:

- Tests cover mark creation, visibility-relevant state, trigger, and cleanup at node end.

## Batch 8 - Luban Migration and CI Gates

Purpose: make P6 real: content changes should be data changes, pending should trend to zero, and CI should fail on regressions.

Scope:

- Migrate hardcoded catalog slices into `Assets/Tools/Luban/Datas`.
- Run `gen_table_nine.ps1` after each slice.
- Add parity tests between hardcoded and Luban for migrated content.
- Once stable, tighten `P6ContentLandingTests` from "pending exists" to "pending must not exceed known count", then eventually "pending == 0".

Acceptance:

- Luban sample grows in controlled slices.
- Generated catalog validates and creates drafts.
- CI catches unknown atoms, invalid schema, and pending-count regressions.

## R3/R4/R5 Mapping

- R3 is now "Luban skeleton exists, not production source". Batches 1 and 8 finish it incrementally.
- R4 is the pending burn-down. Batches 1-7 do this by capability, not by document order.
- R5 is replay/CI hardening. Every batch contributes tests; Batch 8 tightens the global gates.
