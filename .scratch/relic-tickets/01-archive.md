## Parent

Part of #114

## What to build

Introduce the relic archive deck convention and move the nine misaligned legacy relics into it so they no longer appear in any relic reward wiring. Live chest / blood-conversion pools must only expand non-archive relics. Keep archived JSON for reference; do not add heavy structural guards or ActivateRelic hard-rejects—pool (and grant-path) skip for the archive deck is enough.

## Acceptance criteria

- [ ] Archive deck id exists and the nine misaligned legacy relics are assigned to it
- [ ] Relic reward pool expansion never includes archive-deck relics (covered by EditMode contract)
- [ ] Profession / other grant paths do not accidentally grant archive relics
- [ ] Recompile console clean for touched areas

## Blocked by

None — can start immediately.
