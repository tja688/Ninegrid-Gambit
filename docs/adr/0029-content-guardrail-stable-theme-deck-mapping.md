---
status: accepted
---

# 内容护栏：七套稳定 ID—策划槽位—sequence 映射（#127）

## 决策

1. **七套正式主题卡组的稳定槽位契约以代码快照落于 `NineGrid.Content` 的 `ThemeDeckStableMapping`**：七套 × sequence 1–5 每槽唯一 contentId，源自策划七套机制表（`Assets/Docs/九宫格登神/04-敌人侧卡牌信息/怪物卡.md`）与人工确认的当前内容数据（2026-08-05 快照）。deckId / contentId 是历史残留不透明主键（ADR-0014），契约只承载「哪个 deckId 的哪个序列槽位是哪张卡」，不承载任何主题语义或显示文案；`designSlotName`（近战1/远程2…）是表现层策划对照名，不进契约。
2. **displayName 是可变文案，不属于任何契约**。测试禁止断言生产内容 displayName——结构护栏 `ContentDisplayNameAssertGuardrailTests` 强制（白名单仅限自建夹具与代码常量）；改名 displayName 不得导致测试失败或运行逻辑变化。
3. **两条独立校验层，不得混为一谈**：
   - 槽位映射正确性（`ThemeDeckMappingVerifier`）：七套每槽唯一、无重复 sequence、无缺槽、无越界成员、序列 5 恒为层主且 1–4 非层主。当前数据必须通过。
   - 正式可达性（`ThemeDeckFormalReadiness`）：七套在 monster_decks.json 非 Reserve、槽位卡非 Reserve、过渡卡组（`deck.transition`）清空。当前数据**必须不通过**，逐条 blocker 即交付前置报告。
4. **后续各套迁移票以 `ThemeDeckFormalReadiness.Report.Blockers` 为机器可验证的前置清单**：逐项清空后 `Reachable = true`，该套即完成正式启用准备；每迁移一套同步更新 `ThemeDeckStableMappingTests` 中当前 blocker 的期望。

## 为什么

实现按历史英文 ID 字面做判断会在换皮 / 迁移时被名字带偏；测试断言可变显示名会让内容改名直接变红。先建立与名字解耦的稳定契约，再谈逐套迁移（#127 non-goals：不在本票启用 deck_kind、不清空 transition、不迁移 effectAssemblies、不按 contentId 英文词义重命名内容）。

## 后果

- 新增 / 移动七套怪物的槽位归属必须同步 `ThemeDeckStableMapping`；契约与数据不一致会被测试明确报出（`slot_duplicate` / `slot_gap` / `slot_mismatch` / `boss_flag` / `member_out_of_range` / `content_reused` / `deck_reserve` / `slot_card_reserve` / `transition_not_cleared`）。
- 机制键控的运行时硬编码（如 `FragmentRecombineDedup` 的 recombine 伙伴 ID、`PhaseSystem` 的 `skill.holy_duel`、`SkeletonFusionPresentationScanner` 的融合技能 ID）是效果机制的不透明 ID 分支，不是主题语义判断，按 ADR-0014 保留。
- 「映射正确」与「正式可达」分层：七套当前处于映射正确但未正式可达的状态；正式启用以 `ThemeDeckFormalReadiness` 转绿为门槛。

## 相关

- [ADR-0014](0014-theme-ids-are-legacy-opaque.md) — 主题化 contentId / deckId 是历史残留不透明主键
- [ADR-0022](0022-node-loadout-model.md) — 主题卡组按层绑定与序列抽卡
- [`docs/code-map/tests.md`](../code-map/tests.md) — 测试地图（#127 契约测试与结构护栏）
- [`CONTEXT.md`](../../CONTEXT.md) — 「主题怪物卡组」「序列」词汇
