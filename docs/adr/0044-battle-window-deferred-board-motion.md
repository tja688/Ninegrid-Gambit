---
status: accepted
---

# 结算窗口内的效果位移挂起：先打再转

## 决策

**交战窗与敌方行动阶段是「位移锁定窗口」：窗口内由效果 DSL 触发的盘面位移动作一律挂起，延至窗口收尾锚点统一落地。**

1. **锁定窗口两个。** 交战窗（`BeginPlayerMonsterEngagement` … `EndBattleScopeCleanup`，`IsEngagementActive`）与敌方行动阶段（报名 … 收尾，新增 `IsEnemyActionPhaseActive`）。任一窗口打开时位移挂起生效。
2. **挂起对象是动作类型，不是内容清单。** `RotateBoardClockwiseAction` / `SwapBoardSlotsAction` / `MoveCardAction` 三类盘面位移动作，凡经 `ExecuteEffectAction`（全部 Triggered 效果反应的唯一漏斗）产出且窗口打开，即进入 `BattleContextModel` 的挂起队列，不即时结算。伤害 / 护甲 / 属性 / 翻面等其余效果动作不受影响，照常插在结算链原位。
3. **落地锚点三处。** ① 互动计数推进（`AdvanceInteractionCountInternal` 开头，先落位移再 +1 计数；整拍 `Attack` 与导演 `AdvanceInteractionCountCommand` 共用）；② 敌方行动收尾（`ResolveEnemyActionFinaleInternal`，先关窗、再落位移、再通关检查）；③ 道具链收尾（`ExecuteUseItem` 主管线跑完后，覆盖 ForceBattle 类道具交战）。锚点 flush 为**循环排水**：落地引发的连锁若再开窗口再挂起（如 `OnMoveToSlot → ForceBattle`），排水循环继续，带迭代上限护栏。
4. **落地时就地复验，失效即弃。** 挂起条目携带占位快照：Swap 复验两格占位 uid 未变；MoveCard 复验卡仍在盘面且目标格可用；Rotate 恒有效。复验不过静默丢弃该条（触发已发生、后果失效）。终局相位（Defeat / Victory）与节点重置直接清空队列，不落地。
5. **交互旋转不经此路。** 击杀后旋转（`ResolvePostKillRotateInternal`）、补牌等 Core 内建盘面重构不是效果产物，从不在窗口内执行，不挂起。
6. **不碰救人窗口。** `OnFatalDamage` 先于 `KillIfDead` 的 FollowUp 序保持原样；挂起只作用于位移动作，不改变触发派发时机——效果的触发与 `EffectTriggered` 脉冲仍在原位，仅盘面后果延迟。

## 为什么

**反击不验几何是交战模型的既定语义，位移插队破坏了它的前提。** 交战的正交相邻合法性只在玩家下指令时校验一次（`PhaseSystem.Attack` 入口），反击（`ConditionalDealDamageIfAliveAction` / 导演路径 `ApplyCombatHit`）只判存活——这是「一次交战视为一次完整交换」的原子性假设。`OnBattle` 触发的旋转（`relic.beyond_dimension`、`skill.space_mastery`）经反应栈插在「玩家命中」与「怪物反击」之间，把怪转到斜角后反击照打，玩家读到的是「正交怪从斜角打我」（预发布清单 #31）。补几何校验（转走就不反击）会把旋转类内容变成反击免疫器、并让交战语义漂移；挂起位移让交换按玩家点击时看到的盘面完整结算，与 [ADR-0012](0012-enemy-action-phase-volley.md)「一次打击与它引发的一切反应视为同时发生」同构。

**敌方行动阶段的盘面冻结承诺同样需要这道墙。** ADR-0012 立了「阶段内盘面冻结」，但开火同拍技能（`tpl.skill.tide_surge_fire.fire`）的旋转在阶段中途真实转格：后续名单怪逐条复核位置后整窗作废——合法但破坏「齐射全程盘面不变、谁打到我可以从画面对上」的可读性承诺，且未落地的击杀（`KillIfDead` FollowUp 前）尸体会跟环转一格（#207）。挂起到收尾使冻结承诺成立。

**为什么锚点不在 `EndBattleScopeCleanup`。** 现代导演路径把交战拆成命中批 / 反击批两个 Core 批次，各自开合自己的交战作用域（`ApplyCombatHit`）；若在作用域关闭时落地，旋转仍会落在反击批之前——等于没修。互动计数推进是两条路径交战收尾后的第一个公共锚点，且导演的互动批本就投影并经盘面通道呈现（`EnqueueInteractionAdvanceBatch`），表现层零改动即得到「打 → 挨反击 → 盘面转」的可见因果（[ADR-0018](0018-trigger-visible-causality.md)）。

**为什么按动作类型而不是逐内容打补丁。** 全仓 21 个 `OnBattle` 模板中位置类 4 个（`beyond_dimension` / `space_mastery` / `echo_bell` / `evade`），且「战斗 × 位移」是正常内容设计轴（AI 拓展已自发长出 3 个）。逐内容特办要求每个新内容记得换触发点，忘了就复现；按动作类型挂起对现有与未来内容自动生效。

## 考虑过的替代

- **反击前加几何复核（转走就打不到）**：否决——外圈转一格必然正交↔对角互换，「攻击后旋转」类遗物变成永久反击免疫、`space_mastery` 层主被自己技能废掉反击；且「贴脸打了它、它走位后你就当没挨打」语义怪异。
- **逐内容特事特办（归档 / 换触发点）**：否决——见上；归档砍设计空间，换触发点的管线工作量与本案相当却只救打了标记的内容。
- **表现层重排（核先转、画面先打）**：否决——画面造假，违反核真相与 ADR-0018。
- **挂起全部 OnBattle 反应（不限位移）**：否决——荆棘皮 / 战斗硬化 / 掉甲加攻等 17 个模板刻意依赖「命中与反击之间」的插入语义，全部延后是行为大迁移。
- **锚点选 `EndBattleScopeCleanup`**：否决——见上「为什么锚点不在」。
- **齐射侧维持现状（中途转、后续怪作废）**：否决（用户裁决 2026-08-12）——合法但不可读，且尸体跟转问题残留。

## 后果

- **交战内旋转类内容的观感变为「打 → 挨反击 → 盘面转」**；`skill.evade` 的换位延到交战后，与其设计文本「[战斗后]」一致；`monster.tide_heart` 的开火同拍旋转延到齐射收尾，名单后续怪不再被中途旋转作废（挨打次数可能上升，符合冻结原意，属可观测平衡变化）。
- **`relic.beyond_dimension` 复活**：`deckId` 由 `deck.relic_archive` 切回 `deck.relic`（预发布清单 #31 缓解措施撤销）。
- **#207 的交战 / 齐射路径尸体跟转随本案消除**（位移落地时击杀已落地、尸体已离场）；「死卡技能静音」等剩余面另票。
- **窗口关闭且相位非终局时挂起队列必空**是新的不变量；漏网锚点（如未来新增的交战入口）最坏表现为位移延至下一锚点落地，不会丢失、不会插回窗内。
- **`relic.rpm_engine` 链式旋转**在锚点落地后窗外触发，行为不变仅时机后移。
- **诊断**：corelog 中 `BoardRotated` / `CardSwapped`（效果源）恒出现在交战反击 `DamageDealt` 与 `CardKilled` 之后、`InteractionCount` 推进之前（或齐射收尾批内），日志审查可据此断言。

## 相关

- [ADR-0012](0012-enemy-action-phase-volley.md) — 敌方行动阶段与盘面冻结：本案把「冻结」从约定升级为机制
- [ADR-0013](0013-action-countdown-unified.md) — 一次性开火窗口：位置复核语义不变
- [ADR-0018](0018-trigger-visible-causality.md) — 触发可见因果：挂起后果仍晚于可见触发
- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — 批次锁步：落地批复用既有投影呈现
- Issue #207 — 已死未移除卡参与旋转 / 换位：本案采纳其方案 A（交战原子性）
- `Assets/Docs/04-预发布可疑问题总清单.md` #31 — 本案的问题存案
