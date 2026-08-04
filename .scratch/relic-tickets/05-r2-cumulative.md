## Parent

Part of #114

## What to build

Extend OnCumulative (or equivalent highest existing seam) with the metrics needed for cumulative relics—at least monster-removed and help-card-used counts, plus the damage-taken metric required for 血魔 if current hpLost is insufficient. Then land 打卡刀、旋转技巧、恐怖面罩、废物循环机、废物躯体、血魔 as live relics with design-accurate every-N behavior.

## Acceptance criteria

- [ ] New metrics observable via EditMode contracts (count and fire every N)
- [ ] All six relics exist, pool-eligible, assemblies correct, presentation fields filled
- [ ] 恐怖面罩 only removes normal-level board monsters as designed
- [ ] Recompile console clean; QuickTest spot-check optional but encouraged

## Blocked by

- #115
