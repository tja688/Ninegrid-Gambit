---
status: accepted
---

# 卡面数值投影统一对账缝（Core 自动 diff-emit）

## 背景

ADR-0005 确立「卡面显示值只来自结算指令 + 表演锚点消费」后，生成之后的数值**发射完整性**一直靠人肉维护：凡是可能改变「有效攻 / 有效甲」的地方，都要在 Core 里手工追加卡面提交事件补扫（`Append*FaceCommit` 家族）。九宫格的数值源持续膨胀（遗物、邻接光环、规则乘区、拓扑变更、规则卸载、Once 消耗回退……），每新增一个源头就要记得在 N 个 call site 补一刀；漏一处 → 卡面显示与实际结算对不上，且只在特定内容组合下暴露。实际已知的病灶：

- 条件型 Permanent 攻（狂战斧、血之暴力）条件翻转当拍存在「拍内滞后」，要等下一次击杀 / 移除补扫才对齐；
- `RemoveRuleModifiersBySourceAction` 按 Source 卸载规则**完全没有**补扫（静默泄漏路径）；
- 单格移动（非 Swap/Rotate）翻转邻接光环无人补扫；
- 玩家攻击动态增减（多遗物叠加）时卡面攻与实际伤害口径时常错位——用户报告的核心痛点。

## 决策

把「谁改了数值就要记得补发卡面事件」的离散手工补扫，收敛为 **Core 动作管线内一个自动化的统一对账缝**：

1. **单一有效值口径（oracle）**。卡面显示与伤害结算共用同一组函数：
   - 攻 = `CardFaceEventValues.GetFaceAttack`（有效攻；怪物另加 `EnemyAttackDelta` 规则修正）。`PhaseSystem.GetAttackDamage` 与 `EstimateWillKillQuery` 已改为直接调用它——**显示 ≠ 结算在源头上不可能**；
   - 玩家卡面攻 = `CardFaceEventValues.GetProjectedPlayerBattleAttack`（下一次对怪交战的 `DamageMultiplier`/`DamageFlatDelta` 投影；无怪 / 无规则时退化为有效攻）；
   - 甲 = `StatArmorUtility.GetCurrentArmor`（当前护甲；与 `DealDamageAction` 吸收口径同源）；
   - 血 = 基础血（`DealDamageAction` 读写同源，见 `CardFaceReconciliation.GetFaceHp`）；
   - 行动倒计时 = `AttackPatternCountdown` 计数器（仅 `HasActiveRhythm` 卡）。
2. **投影账本 + 自动 diff-emit**。新增 `CardFaceLedgerModel`：按 uid 记录每张卡各通道「最后一次经结算指令投影到卡面」的绝对值。账本**只从事件日志学习**，消费语义与表现层 `CardFaceStatHandler` 一字对齐（血类指令顺带提交同事件甲、`ModifyBaseStat(Armor)` 不写卡面等），因此账本值＝本批播完后卡面将显示的值。`ActionPipelineSystem.ResolveAction` 在**每个动作（含嵌套触发 / FollowUp）自身事件入日志之后**调用 `CardFaceReconciliation.ReconcileAfterAction`：扫新事件进账本 → 对盘面九格 + Avatar 重算 oracle → 与账本 diff → **有变化才**追加绝对值提交事件（`BaseStatModified` / `ActionCountdownChanged`，`Cause=faceReconcile`）。
3. **表演锚点时机保持**。对账事件直接进事件日志（不进 `result.Events`，不参与规则触发），紧邻因果动作、位于其自身事件之后、`ActionFinished` 之前；之后完全复用既有 `PresentationEventMap` 锚点（`BaseStatModified`→Settled）/ `BattleBeatScheduler` / `CardFaceStatHandler` 通路。命中帧扣血扣甲仍由交战动作显式发 `DamageDealt`/`HpChanged`/`ArmorChanged`（Impact）——这些事件同步进账本，对账缝对其零发射；「玩家抬手命中帧才见扣血」的体验不动。
4. **表现层零对账**。`CardFaceStatHandler` / `PlayerInfoHudBeatHandler` 维持「只从指令赋绝对值」的哑消费者；卡面甲与 HUD 甲两轨纪律不变（`BaseStatModified(CurrentArmor)` 写卡面、`ModifyBaseStat(Armor)` 只驱动 HUD）；View 仍禁直读 Core。
5. **首见静默对齐**。账本某通道为 null（存档恢复、测试直摆盘面等无生成事件的入场）时，首次对账按当前 oracle 播种、不发事件；此后任何变化正常 diff-emit。账本随 `ActionPipelineSystem.Clear()`（新局 `InitialGameFactory` 已调用）复位，卡离场（`CardKilled`/`CardRemoved`）即删除条目，uid 复用安全。

## 不变量

- **任意动作批执行完毕后：按表现层消费语义重放事件日志得到的卡面值 == oracle 重算值**，对盘面全部实体 + Avatar 成立（回归护栏：`CardFaceReconciliationRegressionTests.RandomActionStorm_ReplayedFacesAlwaysMatchOracle` 等）。
- 对账提交事件位于其因果动作的事件窗口内（自身事件之后、`ActionFinished` 之前），锚点语义不回退。
- 新增数值源（遗物 / 技能 / 原子 / 规则）**零手工接线**卡面提交。
- diff 后才发事件：同值不发、无变化不刷屏。

## 被废除的旧机制（已删除，不留双轨）

`CardFaceEventValues` 中的补扫家族及其全部 call site：

| 已删除 | 原 call site |
|---|---|
| `AppendPermanentAttackFaceCommit` | `AddStatModifierAction`（Permanent Attack 旁路）、`DealDamageAction`（Once 乘区消耗回退）、`DeactivateOwnerEffectsAction`（宿主本人）、`ModifyRelicRunContributionAction`、`CommitPermanentAttackFaceAction` |
| `AppendProjectedBattleAttackFaceCommit` | `AddRuleModifierAction`（DamageMultiplier 投影） |
| `AppendConditionalPermanentAttackFaceCommitsForBoard` | `RemoveCardAction`、`KillAction`、`RotateBoardClockwiseAction`、`SwapBoardSlotsAction`、`DeactivateOwnerEffectsAction` |
| `AppendEnemyAttackDeltaFaceCommitsForBoardMonsters` | `GrantRelicAction`、`DiscardRelicAction`（连带删除 `RelicEnemyAttackDeltaRules` 探测类） |
| `AppendCurrentArmorFaceCommit` | `SyncAdjacentBorrowedArmorAction` |
| `CommitPermanentAttackFaceAction` 整个动作类 | `EffectSystem.ActivateModifier`（kind:Modifier 路径入队） |
| 条件扫描辅助 `HasConditionalPermanentAttackModifier` / `HasConditionalEnemyAttackDeltaRule` | 随补扫家族删除 |

**保留（非补丁，属主事件正确性）**：生成类 `WithFaceAbsolutes`/`AddWithFaceAbsolutes`；`ModifyBaseStatAction` Attack 分支的 `ResultValue=GetFaceAttack`（持有区卡不在对账范围，主事件必须自带正确绝对值）；`TryAppendActionCountdownChanged` 与显式倒计时事件；交战命中帧的 `HpChanged`/`ArmorChanged`（Impact 时机语义）。

## 后果与已知取舍

- 交战窗内 Battle/Once 作用域修正的挂载与清除会各产生一次对账事件，同批 Settled 先后抵消，卡面无可见跳变（仅事件噪声，可由 `Cause=faceReconcile` 过滤诊断）。
- 玩家投影攻以「盘面第一只怪」为代理目标求值（与旧 `AppendProjectedBattleAttackFaceCommit` 同一近似）；按目标条件分歧的乘区仍是近似显示。与旧行为的差别：代理怪变化（首怪被杀 / 新怪登场）时卡面攻会跟随投影刷新——更真实，但显示可能随盘面小幅变动。
- 有效血若与基础血分歧（目前无 Hp 层修饰器内容），卡面以基础血（结算口径）为准。
- ADR-0005 的「不做兜底、不做强制对账」修订为：**表现层**仍不对账、不直读 Core；**Core 发射侧**的完整性从「人工穷举 + 测试期映射表」升级为「运行时自动对账缝」。锚点表、批次 ack、输入门禁等其余结论不变。

## 相关

- [ADR-0005](0005-card-face-beat-commit.md) — 表演锚点提交（本决策修订其发射完整性策略）
- [ADR-0007](0007-unified-presentation-pipeline.md) — 统一表现管线（消费侧不变）
- [ADR-0028](0028-damage-formula-armor-and-reduction.md) — 伤害公式（oracle 与之同口径）
- [ADR-0044](0044-battle-window-deferred-board-motion.md) — 交战窗位移挂起（位移落地时对账缝自动刷新邻接光环卡面，两者天然协同）
