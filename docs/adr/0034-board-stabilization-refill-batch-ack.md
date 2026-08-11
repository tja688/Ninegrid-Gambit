---
status: accepted
---

# 盘面稳定化归 Core，补牌按批次 ack 推进

## 背景

「有非 Avatar 空格且抽牌堆有牌就补位」是战斗盘面的稳定边界，不应由击杀、Drain、Fusion 或某条表现剧本分别猜测。旧实现把补牌拆成多个表现侧 scheduler，导致节点开局、捕熊触发、非击杀移除和融合结果等路径的补牌时机不一致；表现层还需要临时处理融合结果的牌堆排除。

## 决策

- Core `IBoardStabilizationSystem` 是唯一的盘面稳定化权威。`NeedsRefill` 只在 `InteractionLoop`、节点未清关、抽牌堆非空且存在按 `FillEmptySlotsAction.FillOrder` 可填的非 Avatar 空格时为真。
- `ResolveNextSlice` 每次最多执行一轮 `FillEmptySlotsAction`。这轮 Fill 产生的触发动作可以继续改变 Core，但由下一次稳定态检查决定是否形成下一补牌批；不得把不动点循环塞进一个表现批次。
- 现代导演路径由 `BoardStabilizationScheduler` 反复追加：稳定态检查 → `ResolveBoardStabilizationCommand` → 盘面 `Present` → ack → 再检查。下一轮 Core 解算必须发生在上一轮 Present ack 之后；没有欠补位时只通过空检查，不打开空补牌批。
- 融合/重组结果通过 Core 的 `ShuffleIntoDrawPileAction` `deferRefill` 标记进入稳定化排除集合。稳定化期间优先使用其它牌，稳定化完成后由 Core 恢复结果的牌堆顺序；表现层不得改写抽牌堆。
- `PhaseSystem` 的 `ResolveUntilStable` 保留给开局和旧整拍兼容入口；导演分拍使用上述逐轮接口。清关后不再执行 Post-Kill 补位。
- 原 `DrainRefillScheduler`、`FusionRefillScheduler`、`FusionRefillPlanner`、`FusionRefillAftermath` 及对应表现命令已删除，不允许恢复为新的规则入口。

## 不变量

- 交互相位进入可操作稳定边界时，不存在「有可填空格 + 有牌却未声明欠补位」的 Core 状态。
- Present 未 ack 前，Core 不解算下一补牌切片。
- 一轮 Fill 内触发的新空位形成独立的下一补牌批，而不是在同一批内追算隐藏循环。
- 融合结果在稳定化期间不会被过早发到场上；稳定完成后结果仍按 Core 规定的牌堆顺序保留。
- 节点清关后的残留清场不再触发补牌飞行。

## 后果

- Core 承担欠补位判定、逐轮 Fill 和融合牌堆排除；表现层只负责 pacing 与 Present ack，不再维护 Drain/Fusion 补牌布尔或牌堆例外。
- 需要测试用“稳定态检查步 + 独立补牌批”描述时序，不能用固定 Tick 数推断某轮 Fill 是否发生；测试夹具必须显式准备抽牌堆供给。
- 开局和旧整拍调用仍可一次性收敛，但现代战斗导演保留 `Resolve → Present → ack` 的可见因果。

## 补记：机关空位补牌不触发「补牌触发型」效果（2026-08-11）

策划确认：**敌方机关效果（滚石等 `trap.*` 来源）移除场上卡造成的空位，其补牌不应触发「补牌触发型」效果**——捕熊陷阱不应因滚石移除邻卡而自毁（`需要修改项目.md`：「滚石移除卡时，若捕熊陷阱与被移除卡相邻，捕熊陷阱会把自己移除」）。捕熊陷阱仍对玩家侧造成的空位补牌（击杀/拾取/道具·遗物效果）开火。

- `RemoveCardAction` 在 `SourceDefId` 以 `trap.` 开头且被移除卡在场上时，将该格标记为机关空位（`BoardModel.MarkTrapVacated`；标记在格位被任何卡占用——补牌/旋转/打出——时消费，节点重置时清空）。
- `FillEmptySlotsAction` 两遍分派：先填正常空位，再填机关空位；机关空位的 `CardDealt` 事件带 `cause=refillAfterTrapRemoval`（`FillEmptySlotsAction.TrapVacatedRefillCause`）。两遍分派保证机关空位事件不与正常补牌混在同批，`EventCard` 语义（本批第一个 CardDealt）不误指机关空位。
- 捕熊陷阱 `tpl.trap.bear_trap.fill` 条件增加 `EventFilterExcludeCause(eventType=CardDealt, cause=refillAfterTrapRemoval)`：机关空位补牌批不满足条件 → 不触发。
- 击杀（KillAction）、拾取、道具/遗物效果的移除不标记，其补牌照常可触发。

## 相关

- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — 统一时间线与 Batch-ack
- [ADR-0012](0012-enemy-action-phase-volley.md) — 敌方行动阶段与盘面冻结
- [ADR-0018](0018-trigger-visible-causality.md) — 触发的可见因果
