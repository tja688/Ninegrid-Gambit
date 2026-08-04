## Problem Statement

策划案（`Assets/Docs/九宫格登神/05-遗物`）定义了 51 件遗物，但局内仍是旧实现混杂：约 29 张 JSON 中有 9 张效果与策划名/机制错位，约 22 张策划有、实现无，另有 3 张需要内核级能力（泡沫盔甲首段临时盾、锻造器具/金剑的遗物自身 run 内成长）。开局 Profession 仍挂「轻车熟路」且护甲为 1，与策划「腐朽顺劈斧 + 护甲 0」不符。奖励池按 `kind: Relic` 全量扫描，错位旧卡会继续进宝箱。表现层配置器通道已够用，但新/改遗物的名称、描述、效果装配与图标字段尚未按策划填齐。

## Solution

以策划案为唯一产品权威，分三轮把 51 件遗物全部接到可玩状态：错位旧卡迁入归档卡组且不参与接线；新建正确 `contentId` 与效果装配；扩展累计计量原子以解锁六件；落地泡沫盔甲与遗物 run 内成长并完成锻造器具、金剑。Profession 与开局遗物同步策划。表现层沿用既有卡面配置器：字段与效果挂齐即可，缺主图标由人工补，不挡合并。遗物合成（两白→蓝等）另票。不新增长期 ADR（落地正确即收）。

## User Stories

1. As a player, I want every relic named in the design doc to exist in-run with the documented effect, so that the build fantasy matches the design bible.
2. As a player, I want chest relic choices to only offer live (non-archive) relics, so that obsolete placeholder relics never appear.
3. As a player, I want my Warrior run to start with 腐朽顺劈斧 and 0 base armor, so that the opening loadout matches design.
4. As a player, I want 除甲刀 to grant +3 battle attack when the target has armor, so that armor-strip aggression works as designed.
5. As a player, I want 腐朽顺劈斧 to deal 1 damage to orthogonally adjacent monsters on battle, so that the unique starter cleave is real.
6. As a player, I want 荆棘甲 to reflect damage based on armor I lost in that battle, so that thorns track my armor spend.
7. As a player, I want 肌肉反击 to scale bonus damage with max HP (every 10), so that tanky HP converts into damage.
8. As a player, I want 金色宝箱 to add two golden chest cards to my side once on obtain, so that the relic pays off immediately.
9. As a player, I want 废物增幅器 to give +1 attack this node when I use a help card, so that junk-item play scales offense.
10. As a player, I want 铁盾 to grant +2 base armor and +1 damage reduction, so that the first reduction relic is playable.
11. As a player, I want 身体潜力 to shuffle a random help card into the battle deck every 10 HP damage taken, so that taking hits fuels items.
12. As a player, I want 超越维度 to rotate once on battle, so that the gold mobility relic works in combat.
13. As a player, I want newly designed assemblable relics (废物剑、血液循环/暴力/迸发、血再生、复合盔甲、金血、陷阱格、旋转倒刺、锐利长剑、金属血液、狂战士斧、转速引擎) to be obtainable and functional, so that mid-pool variety is complete.
14. As a player, I want already-correct relics (木系、飞刀袋、凤凰羽毛、渴望等) to keep working and match design text after audit, so that nothing silently drifts.
15. As a player, I want 打卡刀 / 旋转技巧 / 恐怖面罩 to count monster removals this node and fire every N, so that clear-tempo relics work.
16. As a player, I want 废物循环机 / 废物躯体 to count help cards used and fire every N, so that junk loops work.
17. As a player, I want 血魔 to grow max HP from total damage taken (including armor absorbs if that metric is required), so that masochist growth works.
18. As a player, I want 泡沫盔甲 to arm a next-hit temp shield the first time my current armor hits 0 in a node (the zeroing hit itself is not immune), and only cancel the first segment of a multi-hit, so that foam armor matches design edge cases.
19. As a player, I want 锻造器具 to spend 5 current armor at node start when armor ≥5 and permanently (+run) raise this relic's own base armor by 1, so that self-growth is on the relic not a generic player buff.
20. As a player, I want 金剑 to contribute +8 attack that decays by 1 per battle (floor 0) and grows by 1 per kill for the rest of the run, so that the sword's power is relic-scoped.
21. As a player, I want relic growth to reset on a new run, so that meta progression does not leak across runs.
22. As a content author, I want each live relic editable in the existing card presentation editor (name, description, effect assemblies, icon slot), so that I can finish copy and art without a new tool.
23. As a content author, I want archive relics kept for reference but excluded from reward wiring by simple deck convention, so that old content does not pollute pools.
24. As a developer, I want EditMode contracts and targeted QuickTest checks per round, so that each round is mergeable without a full PlayMode gate.
25. As a designer, I want relic synthesis left for a separate ticket, so that content landing is not blocked by economy crafting.

## Implementation Decisions

- Product authority is the current design bible under the 九宫格登神 docs tree (51 relics). Old 58-count audits are obsolete.
- Three engineering rounds: (R1) archive + template-assemblable content + Profession sync + keeper audit; (R2) OnCumulative metric extensions + six cumulative relics; (R3) foam armor + per-relic run growth for 锻造器具 and 金剑.
- Misaligned legacy nine relics move to `deck.relic_archive` and must not participate in reward/grant wiring. Keep their JSON/effects for reference; do not build heavy structural guards or ActivateRelic hard-rejects—convention plus reward-pool expand skip for that deck (and equivalent grant paths as needed).
- Replacement and net-new relics use fresh `relic.<snake_case_english>` ids; `displayName` and rarity/role follow the design doc. Keepers that already match names retain existing content ids.
- Unique starter 腐朽顺劈斧 maps to existing `ContentRarity.Red` so chest pools (White/Blue/Gold only, red weight 0) do not offer it; Profession is the grant path.
- Profession sync in R1: initial relic = new 腐朽顺劈斧 id; base armor 0.
- Presentation: existing configurator is sufficient; fill name/description/effect assemblies; missing main icons are human-filled and non-blocking.
- No new ADR for foam/growth; ship when behavior matches design.
- Relic self-growth is run-scoped and persists across nodes; cleared on new run.
- Foam armor: first time current armor reaches 0 in a node, arm a one-shot “next damage” shield; the hit that zeroed armor is not shielded; after the shield consumes, do not re-arm that node; multi-segment hits only cancel the first segment.
- Testing seams (prefer existing, minimize new):
  1. Relic content JSON + effect assemblies → catalog projection (primary seam for most relics)
  2. Reward pool query expansion (archive deck exclusion)
  3. Profession / initial grant
  4. OnCumulative metrics (R2)
  5. Relic-instance run state + standard damage formula path (R3 only new high seam)
- Prior art: relic inventory/economy contracts, node-start relic card grants, content catalog validation / JSON projection tests.

## Testing Decisions

- Good tests assert observable external behavior (catalog membership, pool candidacy, Profession grant, trigger outcomes, damage/armor numbers)—not private template wiring details.
- Prefer EditMode contracts at the seams above; reuse patterns from relic economy, node-start grant, and content projection tests.
- Per-round close bar: affected EditMode green via ai-workspace test mutex; recompile console clean of new Error/Exception/Assert; R1/R2 spot QuickTest; R3 one reproducible path each for foam / forge / gold sword. Full PlayMode suite is not the default gate.
- Archive exclusion must be covered so archived relics never appear in relic chest / blood-conversion pool expansion.

## Out of Scope

- Relic synthesis (two white → blue, two blue → gold)—separate ticket.
- New dedicated relic configurator UI beyond the existing card presentation editor.
- Authoring missing main icon art (human).
- Long-lived ADR for foam armor or per-relic growth.
- Restoring or wiring obsolete relics from the deleted 58-count design.
- Changing unrelated systems (trap decks, monster theme routing) except where a shared atom is extended for relics.

## Further Notes

- Grill consensus: three-round cut by engineering cost; archive by simple convention; full semantic landing for hard three; no ADR.
- Gap probe note remains non-authoritative process material; this spec is the tracker authority for implementation.
- Child tickets slice R1–R3 into tracer bullets with explicit blocked-by edges.

## Child tickets

- [ ] #115 归档卡组与奖池排除
- [ ] #116 R1 九件错位新建 + Profession
- [ ] #117 R1 可拼新建包
- [ ] #118 R1 留用审计
- [ ] #119 R2 OnCumulative + 六件
- [ ] #120 R3 成长 + 锻造 + 金剑
- [ ] #121 R3 泡沫盔甲

**Frontier now:** #115, #118
