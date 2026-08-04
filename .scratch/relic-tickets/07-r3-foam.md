## Parent

Part of #114

## What to build

Land 泡沫盔甲: +1 base armor; the first time current armor reaches 0 in a node, arm a temporary shield for the next damage instance; the hit that zeroed armor is not shielded; after the shield consumes, do not re-arm that node; multi-segment damage only cancels the first segment. Ship as live White relic with editor-editable fields. No ADR.

## Acceptance criteria

- [ ] Foam arming / non-arming of the zeroing hit / single re-arm rule covered by EditMode contract on the damage seam
- [ ] Multi-segment: only first segment cancelled
- [ ] Relic is live pool content; presentation fields filled
- [ ] Recompile console clean; one reproducible in-editor path encouraged

## Blocked by

- #115
