---
status: accepted
---

# 部分道具卡可在非战斗相位使用（usableOutsideBattle）

## 决策

道具卡增加**卡级** JSON 声明 `usableOutsideBattle`（bool，缺省 `false` = 战斗限定）。标 `true` 的卡在**非战斗相位**也可被使用：

- **放行相位**：`GamePhase.RoomChoice`（节点 4 离开、节点 7 层主房战前缓冲）与 `GamePhase.RewardItemChoice`（商店 / 卡店 / 属性房 / 奖励房会话）。`GamePhase.RoomEvent`（进房瞬态相位）**不放行**。
- **手势**：与战斗内一致——把手牌（道具卡格）拖到棋盘空位，经既有 ApplyZone → IntentIntake → Director → Core 链路；不可用的卡拖上去照旧回手，v1 不加提示文案。
- **语义**：使用即消耗（`ExecuteUseItem` → `ConsumeUsedItemIfStillInItemSlots`），与战斗内完全相同；满血使用允许浪费（`HealAction` 钳制到 `MaxHp`，不引入 HP 读表门禁）；不推进九宫格互动计数、不触发清关（`CompleteNodeIfCleared` 已有 InteractionLoop 相位守卫）；效果经既有触发器与原子（`Heal` / `ModifyGold` / `GainArmor`…）执行，写入跑图持久数据（Avatar `Stats` / `PlayerModel.Coins`）。
- **首批**：恢复药水（`help.healing_potion`，+10 血）、生日蛋糕（`help.food_card`，回满）、钱袋子（`help.gold_card`，+50 金）。护甲类不入选——`CurrentArmor` 每个节点开局重置（`ResetCurrentArmorAction`），非战斗叠甲跨不进下一战，须先解决跨节点语义再谈。
- **内容护栏**：`ContentHygieneValidator` 新增告警——标 `true` 但效果装配含战斗依赖（目标 `SelectedCards` / `AllMonsters` 等）→ 告警，防策划误标。

## 为什么

旧规则是非战斗相位一刀切「道具卡格仅回收，不合法打出」（`PhaseSystem.RefreshLegalCommands` 的 RoomChoice 分支注释所引，实现层级原本编码为相位白名单硬门禁）。本决策把门禁从「相位级全禁」改为「相位放行 + 卡级豁免」：策划想要的辅助类道具（回血、获金）在跑图间隙有使用价值，但战斗道具（火球、炸弹、破击锤等）仍必须留在战斗内，不能一刀切全放行。

选择**卡级字段**而非效果模板级声明的理由：策划要**逐卡**控制同一模板在不同卡上的非战斗行为（同一 `tpl.heal_player_on_use_help_card` 可挂多张卡，命运不必相同）；显式标注优于隐式推导（自动推导「模板无战斗依赖即可用」会让「装错」静默化，违背 ADR-0010 的「声明即责任」）；忘标的风险（新卡漏标 → 静默战斗限定，症状温和）由内容卫生告警兜底。本声明与**效果责任自陈**（ADR-0010）正交：`requires` 管辖「效果适用于什么场景」，`usableOutsideBattle` 管辖「卡在哪些使用相位可被使用」。

## 考虑过的替代

- **效果模板 requires 加相位 token**：否决——模板复用率高，逐卡粒度诉求无法满足；同一模板两卡行为不同时 token 反而产生误导。
- **自动推导**（模板无场上实体依赖即非战斗可用）：否决——隐式、装错不可见。
- **全道具卡一刀切放行**：否决——正是本决策要避免的失败模式，火球/炸弹在非战斗无目标可打。
- **非战斗使用前阻止满血浪费**：否决——需在合法性层读 Hp，且与战斗内行为不一致；浪费是玩家决策。
- **v1 加「仅限战斗使用」提示文案**：推迟——与现状（非战斗拖战斗卡回手）一致即可，文案管道另票。

## 后果

- **ADR-0025 相关条款局部废止**：RoomChoice 相位「道具卡格仅回收，不合法打出」对 `usableOutsideBattle=true` 的卡不再成立；`PhaseSystem.cs:2117` 注释随代码更新。道具卡格的持续持有、回收、满拒入等其余条款不变。
- **三处相位硬门禁改动**（PhaseSystem `RefreshLegalCommands` + 门禁版 `UseItem` 卡级裁决；`BoardIntentLegality.IsBoardCommandPhaseWithoutLock` / `TryExplainUseItem`；`ValidateHandDragApplyAsync` 非 InteractionLoop 放行需卡级通过）。
- **非战斗使用表演**：走统一导演 + **非锁步冲刷**（`BattleBeatFlush.PresentEventLogSlice`）驱动 HUD（`Healed→UpdateHp`、`GoldModified→UpdateGold`），不复用战斗的锁步 Batch-ack 与交战通道；卡离手表现复用既有消耗路径。非战斗金币飘字沿用 `InRoomGoldPresentation` 管道。
- **经济影响**：钱袋子在商店会话内 +50 金立即可用（先买后用、回收价 10 金 vs 使用 50 金）；属策划预期内的辅助收益，不另设经济护栏。
- **内容侧**：`CardPresentationIndexIO` 扫盘自动纳入新字段；`ContentHygieneValidator` 新增告警规则。

## 相关

- [ADR-0025](0025-item-slots-run-persistent-hold.md) — 道具卡格持续持有；「非战斗仅回收」条款被本决策局部废止
- [ADR-0010](0010-self-declared-effect-responsibility.md) — 效果责任自陈；本声明与其正交
- [ADR-0021](0021-run-progression-in-core.md) — 跑图编排与相位
- `CONTEXT.md` — 非战斗可用
