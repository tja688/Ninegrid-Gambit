---
status: accepted
---

# 主题化内容代号是历史残留的不透明主键

## 决策

1. **`contentId` / `deckId` / 个别 Presentation 类名（如 `SkeletonFusion*`）里的骷髅、巨龙等主题词，视为历史残留的虚构命名与不透明主键。** 以实现效果为准，不要按字面当成现行表现层世界观。
2. **效果原子语义名继续保留**（如 `TransferArmor`、`DealDamage`）——读名能猜机制即可。
3. **表现装配与 `displayName` 可自由换皮。** 玩家侧若需要自定义称呼，用另行映射的显示名，与逻辑 ID 解耦。
4. **`deckId` 是内部渠道**（卡背归属、怪物遭遇分组），不是玩家叙事上的「你是巨龙/骷髅阵营」露出。

## 为什么

早期内容与类名按当时皮设定命名；后续表现装配会换来换去，机制侧增量以新原子与卡牌装配为主。若把 ID 里的主题词读成现行世界观，容易误判产品意图或偏离效果实现。用约定消歧即可。

## 后果

- Agent / 人类看到骷髅、巨龙等字样时，按不透明主键与遗留类名处理，不被虚构命名带偏。
- 换皮优先动 `displayName` / 表现装配 / 另行称呼映射，而非假定必须改逻辑 ID。

## 相关

- [ADR-0008](0008-single-source-content-and-resources-loading.md) — 一卡一文件内容权威
- [ADR-0009](0009-parameterized-effect-templates.md) — 代号主键、中文只显示；分类三轴
- 根目录 [`AGENTS.md`](../../AGENTS.md) — Agent 入口摘要
- [`CONTEXT.md`](../../CONTEXT.md) — 「卡组」词汇
