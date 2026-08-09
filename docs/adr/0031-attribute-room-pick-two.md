---
status: superseded
superseded-by: ADR-0022
---

# 属性房三选二会话（#136）

> **已废止**（2026-08-09）：属性房恢复与其他战斗房相同——进房直接开战；开局按 `RoomDefinition.OpeningInjects` 加权自动注入 2 张属性卡（40/40/20，可重复），不再提供进房三选二会话与 `AttributeBoard` 场地板。

## 历史决策（已废止）

属性房曾是「开局自动按权重抽 2 张」→ 改为**玩家三选二会话**（进房 3 加权候选 → 选 2 → `RunModel.AttributePickDefIds` 本关开局注入）。玩家反馈与策划案《房间.md》一致：属性房不应单独搞选房 UI，应与战斗房一样选完直接进战。

## 现行行为

见 [ADR-0022](0022-node-loadout-model.md) 属性房条目：`BuildNodeDeckOptions` 对 `RoomKind.Attribute` 执行 `OpeningInjects` 加权池 `count=2`、`allowDuplicates=true`。

## 相关

- [ADR-0022](0022-node-loadout-model.md) — 开局注入容器与玩家侧卡组生成规则
