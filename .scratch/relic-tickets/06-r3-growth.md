## Parent

Part of #114

## What to build

Introduce run-scoped per-relic mutable contribution (cleared on new run, kept across nodes) sufficient for 锻造器具 (node-start: if current armor ≥5, spend 5 and permanently +1 this relic's own base armor for the run) and 金剑 (+8 relic attack; −1 per battle floor 0; +1 per kill for the run). Ship both relics as live content with editor-editable fields. No ADR—behavior correctness is the close bar.

## Acceptance criteria

- [ ] Relic-scoped run state exists at the agreed high seam and resets on new run
- [ ] 锻造器具 and 金剑 match design numbers/triggers in EditMode (or equivalent) contracts
- [ ] Both are live pool content (Blue / Gold as designed); presentation fields filled
- [ ] Recompile console clean; one reproducible QuickTest path each encouraged

## Blocked by

- #115
