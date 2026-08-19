---
status: accepted
---

# 规则书开页演示是脚本傀儡

规则书右页的循环讲解是**开页演示**：脚本驱动演示物（正式卡牌底盘 + 卡面，或格位示意），不嵌入 Core 小局，也不借用场上真卡 / 真卡组 / 真道具卡格 / `PresentationDirector`。规则书打开是活性遮盖，场地仍在走；再开一局或劫持场上对象都会和正在打的那局抢单例、占格与飘字。一开页只露该页主语；打出或发放进虚空；循环用演示复位（短淡出再淡入）。旋转开页用完整 3×3 加示意裁切，不把半边格当成规则盘。

## 考虑过而没选

- 书里再跑一局隔离 Core：要再养 IntentIntake / Batch-ack / 正 UID，和活性遮盖叠在一起，只为了让数字碰巧算对。
- 打开书时借场上真卡或 `CardDeckManagerSingleton`：会改玩家正在打的那局。

## 相关

- [CONTEXT.md](../../CONTEXT.md) — 开页演示 / 演示物 / 格位示意 / 示意裁切 / 虚空 / 演示复位
- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — 真战斗表演仍只走导演时间线
- [ADR-0004](0004-input-intake-two-axis-gating.md) — 活性遮盖不改 InputOwner、不暂停主线
- [ADR-0042](0042-tutorial-level-module.md) — 教程与规则书不是同一件事
