---
status: accepted
---

# 两相 Impact 节拍与触发脉冲链级去重：运动停稳 → 触发反馈 → 飘字

## 决策

**表现锁步批次的 Impact 消费拆为两相，触发脉冲（`TriggerEffect`）从各提前锚点整体后置到「本批盘面运动全部落地之后」，且同一交互连锁内同一持有卡的同一效果只演一次触发反馈；Core 侧为事件链补因果深度语义（`CoreGameEvent.CausalDepth`）。**

1. **两相 Impact（PresentStep 收束）。** `PresentStep` 在表演通道（含 Drain 全部运动步）完成后，先 `FlushImpactOnly(TriggerEffect)` 单独演触发脉冲；若有脉冲派发则停 `TriggerCadenceSec = 0.35s` 节拍，再 `FlushBeats()` 冲其余 Impact（伤害/护甲/血量飘字等）与 Settled。批内可见顺序固定为：**运动停稳 → 触发脉冲 → 飘字/血甲 → Settled**。
2. **提前锚点收窄为 `FlushImpactExcept(TriggerEffect)`。** 攻击/反击命中帧（`FieldBattlePresentationExecutor`）与 Drain 的「首个 Remove 前」锚点（`BoardPresentationPlayer`）只冲非触发类 Impact——命中反馈与「尸体消失前必须可见」的飘字仍在原时点，触发脉冲一律留给两相收束。Drain 无 Remove 时的尾部兜底冲刷删除（每个已开批必经 PresentStep FlushBeats 才 ack，pending 不会滞留）。这是把用牌 / 奖励 Choice 路径已验证的 ADR-0018 保护推广到攻击 / 反击 / 探索 / 敌方行动全部锁步路径。
3. **链级去重（`TriggerPulseChainDedup`）。** Core 对每个含 qualifying 事件的动作都发独立 `EffectTriggered`（如光环 refresh 模板 `OnEvent(CardMoved/CardDealt)` 在挂起位移落地批、补牌批、旋转批各发一条）——这是内核事实，不改。表现侧在 `EffectTriggerPulseBeatHandler` 按 `(CardUid, Message=效果实例 defId)` 做链级去重：同一条输入意图连锁（导演主线从接单到跑空）内只演第一次，其余静默消费。复位点：`PresentationDirector.TrySubmitIntent`（新链）、`Tick` 主线跑空、`HardClearIntents`。
4. **例外：神圣决斗惩罚帧保持全量 Impact。** 决斗者攻击命中帧的 `ReleaseQuarantined + NotifyBeat(Impact)` 不收窄——隔离区的「决斗者脉冲 + 对玩家伤害」本就是刻意对齐决斗 rig 命中帧的编排（该脉冲同样登记进链级去重）。
5. **Core 因果深度（`CausalDepth`）。** `ActionPipelineSystem.ResolveAction` 给 `ActionStarted`/`ActionFinished` 与动作产出事件统一盖章因果嵌套深度（根 0，触发/FollowUp 子动作逐层 +1；`CardFaceReconciliation` 对账缝事件不盖章、保持默认 0）。与 `Sequence` 一起构成事件链的时间/因果语义：Sequence 定全序，CausalDepth 定派生层级，供表现编排与日志审查辨认「谁派生自谁」。

## 为什么

**症状一：机关光环（`trap.attack_totem` / `skill.link_tactics`）一次交互脉冲三连发，且跳位前就缩放。** 根因在 Core 与表现两侧叠加：Core 侧 refresh 模板按「动作」触发，一次击杀交互的挂起位移落地批、补牌批、击杀后旋转批各产一条 `EffectTriggered`（均指向同一持有卡同一效果）；表现侧 `EffectTriggerPulseBeatHandler` 无去重、每条都演，且攻击命中帧 `NotifyBeat(Impact)` 把触发脉冲在盘面运动开始前就消费掉——观感即「跳前两次、跳后一次」。链级去重 + 触发后置后：脉冲恰好一次，且发生在持有卡运动落地之后。

**症状二：伤害/护甲飘字出现在旋转前或旋转中途。** 攻击/反击命中帧冲刷的是**全部** Impact；互动计数批空投影时 `QueuedBoardPresentChannel` 秒完成、`PresentStep` 立即 FlushBeats；`DrainBoardStepsAsync` 又在「首个 Remove 前」全量冲刷——三处都可能把飘字打在同批 Rotate 步之前。两相收束后，非命中帧的飘字统一等运动停稳，且排在触发脉冲之后（效果先可见、再看到它造成的数值变化，与 ADR-0018「触发条件先可见」互为镜像）。

**为什么宁可拉长演出。** 用户验收标准明确「宁可串行拉长演出，也要顺序正确」。0.35s 节拍只在本批确有触发脉冲时插入；无触发的批次零开销。

**为什么去重放表现层而不改 Core / DSL。** refresh 每次重算是规则事实（`replaceSameSource` 的邻接光环必须随每次位移重算），砍掉 Core 事件会伤害规则正确性与日志审查；DSL 加 `feedback:false` 会把「落地后那一次该演的脉冲」也吞掉。表现层链级去重恰好收敛为「一次交互一次反馈」，且对 QuickTest / 正式局所有路径一致。

## 考虑过的替代

- **给 `EffectTriggered` 加 `FeedbackSuppressed` 标记 + 模板 `feedback:false`**：否决——refresh 与「落地后应演的那次」无法在 Core 侧静态区分，会把正当反馈一并吞掉。
- **按 Sequence 顺序单相冲刷（触发与飘字按日志序交错）**：否决——命中帧已提前消费掉命中飘字后，剩余指令的日志序在跨批场景下不保证「脉冲在飘字前」的观感；两相 + 节拍的可读性收益明确。
- **Core 批次内建动作树 / 相位屏障事件**：否决——`PresentationBatch` 扁平指令 + Beat 已够表达本票需求；CausalDepth 以最小代价补齐因果语义，动作树留待真实需求出现。
- **Drain 尾部兜底冲刷改 Except 而非删除**：否决——留着会让无 Remove 批的飘字先于触发脉冲（违反目标顺序）；锁步不变量保证删除无 pending 滞留风险。

## 后果

- **行为变化**：攻击/反击/探索/敌方行动路径中，触发脉冲与非命中帧飘字整体后移到运动停稳之后；有触发脉冲的批次演出加长约 0.35s；同一交互链内重复触发不再重复演出（音频同样收敛，`sfx.effect|skill|trap|relic.trigger` 不再连发）。
- **新不变量**：① 锁步批内可见顺序 = 运动 → 触发脉冲 → 其余 Impact → Settled；② 同链同卡同效果至多一次触发反馈；③ 除命中帧/Remove 前/决斗惩罚帧三个白名单锚点外，禁止新增提前 Impact 冲刷点；④ `CausalDepth` 由管线独占盖章，业务动作不得自写。
- **回归**：`PresentationBeatOrderingContractTests`——两相顺序、Except/Only 分工、链级去重复位、CausalDepth 盖章（根 0 / 触发子动作 ≥1）。
- **已知遗留（有意不改）**：① 两条**旁路脉冲源**不经 Beat/去重——嘲讽重定向直调 `tauntVictim.PlayEffectTriggerPulse()`（`CardAttackBasicAdapter`）与 Effect SO 回调（`CardEffectManager.DispatchCallback`）；它们是独立编排的受击/回调反馈，保持原状，若未来出现与链级脉冲叠帧的观感问题再收口。② **双实例模板**（如 `trap.attack_totem` 的 `refresh` + `refresh_player`）defId 不同、去重互不牵连，同一 qualifying 动作会同帧派发两次脉冲——缩放 tween Kill 重播后视觉上合为一次，但音频脉冲会同帧叠发；如可闻则考虑按 `(CardUid, 帧)` 合并音频或合并模板。
- **日志审查**：corelog 导出事件 `ToString` 新增 `depth=` 字段；battlelog 分析可按 ActionId+CausalDepth 重建因果树。

## 相关

- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — Batch-ack：两相冲刷仍在同一 PresentStep 内完成，不改批边界
- [ADR-0005](0005-card-face-beat-commit.md) / [ADR-0007](0007-unified-presentation-pipeline.md) — 卡面 Commit 与统一表现管线：第二相仍走 FlushBeats 同构出口
- [ADR-0018](0018-trigger-visible-causality.md) — 触发可见因果：本票把其保护从用牌/Choice 推广到全部锁步路径
- [ADR-0044](0044-battle-window-deferred-board-motion.md) — 挂起位移落地批正是 refresh 脉冲的主要来源批
- [ADR-0047](0047-pipeline-fault-containment.md) — CausalDepth 与熔断共享「深度反映真实因果嵌套」语义
