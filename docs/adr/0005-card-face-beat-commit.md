---
status: accepted
---

# 卡面显示值由结算指令在表演锚点提交

## 决策

记录 Spec #53 决策 1–6（输出侧唯一提交出口）：

1. **卡面显示值只来自结算指令，永不回头读规则内核权威值。** 数值类指令携带结算后绝对值，卡面赋值不累加。权威值与卡面显示值分层；显示值有意滞后于权威值。
2. **不做兜底、不做强制对账。** 漏接指令 → 卡面停在旧值；`Settled` 后未消费的卡面归属指令只报诊断与断言，值一动不动。穷尽性靠测试期映射表，不靠运行时补救。
3. **锚点归属并入既有 `PresentationEventMap`，不另建第二张表。** 锚点枚举住在 `NineGrid.Core.PresentationBeat`；表演层新增节拍须改内核。
4. **v1 只要三个锚点**（命中 / 收尾 / 不上卡面）；升级路径写在枚举注释（若观感嫌晚可加 `PostImpact`，命中回调内延时二次报点即可）。
5. **不存在「定义值路径」。** 全世界只有一条路：卡面显示值一律来自结算指令。JSON `stats` 仅为内容作者出生值（经 Catalog 造卡），不参与投影提交。
6. **投影提交是瞬时的，数字跳动是装饰。** 锚点处赋值瞬时完成；滚动/回弹属旁路装饰道，不占编排主线、不参与就位回执。

v1 锚点：

- **Impact（命中）**：攻击/反击 `onCombatHit` 报点；血量/护甲类指令在此消费。
- **Settled（收尾）**：`PresentStep` 在表演通道完成之后、就位回执之前报点；观察型 `BaseStatModified` 等在此消费。探索/道具无命中帧时，须冲刷剩余 Impact：探索盘面 Drain 锚在**旋转/位移落地之后、Remove 之前**（同批若先有 Deal 补牌，须等 Deal+Rotate 都播完再 Impact，禁止在 Deal 前抢跑）；用道具仍在 Vacate 前报点。`OnSelfMove` / `OnMoveToSlot` 只匹配盘面→盘面 `CardMoved`（卡组入场落地不计）。
- **None（不上卡面）**：显式弃权，映射表必须附带非空理由。

生成类事件（`CardSpawned` / 带 uid 的 `CardDealt` / `AvatarAppeared`）在造卡或发牌时写入攻/甲/血绝对值，经 `CardFaceStatHandler` 与后续增量同一提交出口；`CardKilled` 携带 `RemainingHp` 做可见归零。Mapper 首次 Commit 只刷视觉。公开底盘数值 Setter 与 `MarkFieldDead` 直置零旁路已删除。

## 为什么

规则内核一批解算会算完本拍与全部连锁，表演开始时权威值已是终值。若卡面仍「现场问内核此刻多少」，物理上不可能取到中间值，于是出现「抬手蓄力时观察型加攻已涨、命中后才掉甲」的因果倒置。提交时机散落在命中帧直读与解算投影旁路时，作者无法预期新数值事件何时上卡面。

单一出口（排期器 → 卡面数值处理器 → `CommitPresentation`）与「无兜底」政策，对齐 [ADR-0002](0002-card-chassis-and-face-templates.md)「投影提交只由阻塞串行队列触发」与 [ADR-0004](0004-input-intake-two-axis-gating.md)「输入唯一收口」的结构：输出侧建立唯一出口，穷尽性靠测试期映射表护栏，不靠运行时补救。本票完全建立在 [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) 统一时间线与批次就位回执之上，不改动其结论。

## 考虑过的替代

- **表现层另建「指令→锚点→处理器」表**：否决——多一张人工对齐表即多一处失配；锚点列并进既有表演事件映射表，一行看完完整表演契约。
- **强制对账 / 表演结束拉回内核**：否决——历史教训是衍生 bug 多于挡掉的缺陷，且掩盖漏接。
- **细化内核批次粒度（可挂起续延）**：否决为本版范围——影响面远超卡面观感修复；本版用锚点排期解决观感，不动解算粒度。
- **「命中后」更细锚点**：延后；升级路径写在 `PresentationBeat` 枚举注释（`PostImpact`）。

## 后果

- 新增 `BattleBeatScheduler` / `CardFaceStatHandler`，由 `PresentationCompositionRoot` 注册；命中帧与 `PresentStep` 报点。
- 删除攻击路径命中帧 `SyncManagedCardPresentation` 与石头爱好者观察型提前同步。
- 用道具 Present 开头改为 `RefreshVisualsPreservingCommittedStatsOnAllSpawned`（只刷视觉）；数值仍由通道完成时的 Impact/Settled 消费。
- `CoreCardPresentationMapper` 保留视觉投影；已提交卡面的数值不被直读 Core 覆写；JSON `stats` 不再盖写运行时卡面。
- `MarkFieldDead` 只标死亡态；可见血量归零走 `KillCard` 指令的 `RemainingHp`。
- 玩家信息 HUD、金币已由 #61 收口到指令 + FlushBeats；Bounce 候选项已由 #62 收口到 `RewardOffered`/`OfferReward` 投影 + Settled。伤害飘字与 FX 脉冲已由 #60 收口到 Impact。
- **衔接补丁已移除（#61）**：Avatar 血甲 HUD 改由 `PlayerInfoHudBeatHandler` 在 Impact 用指令绝对值刷新；金币由 `GoldGainBeatHandler` 在 Settled 广播飞币。战中不再 `SyncFromCore`（开局/作弊白名单除外）。见 [ADR-0007](0007-unified-presentation-pipeline.md)。
- **#198 修正**：金币装饰由 `Settled` 改至 `Impact`（尸体 Vacate 前保出生点，与飘字同缝）；`ModifyGoldAction` 可选来源卡并在 `GoldModified` 写 `CardUid`，击杀/移除赏金与拾取加金传正确来源 uid。
- **Permanent 有效攻旁路（攻的绝对值语义）**：凡改「卡面可见攻」的路径（`AddStatModifier`/`CommitPermanentAttackFace`/补扫/`ModifyBaseStat` Attack 分支）一律发 `BaseStatModified(ResultValue=GetEffectiveInt(Attack))`——带常驻/条件修饰器（遗物、攻击图腾光环）时有效攻≠基础值，卡面显示须与伤害结算（有效攻）一致；禁止 `ModifyBaseStat` 提交基础值（历史缺陷：加攻卡显示落后于真实攻击力，见 `docs/adr` 旁路约定）。`Delta`/`Amount=StatId` 语义不变。
- **#62**：`RewardEntry` 携带展示用攻/甲/血；`RewardOffered` Beat=`Settled`；`CardFaceStatHandler` 消费 `OfferReward`；Bounce 删 `clearCombatStats` 数值旁路，spawn 后 `PresentStandalone` 二次提交。

## 相关

- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — 统一时间线与批次就位回执（本决策建立其上）
- [ADR-0002](0002-card-chassis-and-face-templates.md) — 卡牌底盘与投影 Commit；本决策真正落地「禁止队列外直刷」
- [ADR-0004](0004-input-intake-two-axis-gating.md) — 输入唯一收口；本决策为输出建立唯一出口（结构同源）
- [ADR-0007](0007-unified-presentation-pipeline.md) — 多处理器统一表现管线（卡面之外的装饰消费者）
- [ADR-0018](0018-trigger-visible-causality.md) — 触发可见因果；运动后 Impact 冲刷服从其产品不变量
