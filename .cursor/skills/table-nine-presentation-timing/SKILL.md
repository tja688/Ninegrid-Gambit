---
name: table-nine-presentation-timing
description: >-
  Adjust WHEN a battle effect is seen to fire relative to board motion
  (rotation / landing / hit frame), per the user's play-feel verdicts.
  Translate complaints like "滚石在旋转开始前就触发了一下，改成落地停稳之后再触发",
  "飘字在旋转中途蹦出来", "同一效果闪了两三次", "触发了但什么反馈都没有"
  into the batch/anchor vocabulary of ADR-0048 two-phase Impact, locate the
  minimal lever, change and verify. Use when the user reports effect / pulse /
  floater timing that feels wrong or inconsistent, asks to move a trigger to
  after 落地/停稳 or onto the hit frame, or mentions 编排时序 / 表演顺序 / 观感不统一.
---

# 表演时序调整（玩家观感 → 编排杠杆）

用户的工作方式：自己多玩多体验，逐条下达「某效果现在 X 时演，改到 Y 时演」的调整指令，以他的观察和标准为准。本 skill 让你不必全量重学编排系统就能接单：把观感描述翻译成系统词汇 → 取证定位 → 选最小杠杆 → 改动并给出复现路径。

**默认序（ADR-0048 不变量）**：锁步批内可见顺序固定为 **运动停稳 → 触发脉冲 → 飘字/血甲 → Settled**（`PresentStep` 两相冲刷，脉冲后停 0.35s 节拍）。用户新诉求通常是两类：某条路径**没走在**这个默认序上（bug），或想为个别效果**定制**不同时点（需求）。宁可串行拉长演出，也要顺序正确——这是用户已明示的验收标准。

## 接单流程

1. **翻译诉求成三元组**：效果是谁（defId + 模板 id + 触发原子）、现在何时演、期望何时演。
   卡名 → `Assets/StreamingAssets/ContentVisual/cards/*.json`（`effectAssemblies[].templateId`）→ `effect_templates.json` 模板 `body` 的 `trigger` 原子（`OnSelfMove` / `OnMoveToSlot` / `OnInteract` / `OnDeal` / `OnEvent` …）。
   完成标准：三元组写全，defId 与模板 id 落实到文件行。
2. **取证定位，不靠猜**：判断该指令在哪个批产生、被哪个锚点消费（或根本不经 Beat）。证据源按序：
   - corelog `EffectTriggered`（`Sequence` 定全序、`CausalDepth` 定派生层级）+ Rhythm 轨 → 用 `table-nine-battlelog-analysis` skill；
   - DirectorTrace `PresentBegin` / `PresentAck` choreo 段 → 批与通道对应；
   - 需要活体观察 / 边玩边验时 → `live-lab` skill。
   完成标准：能陈述「事件在批 N 产生，被锚点 X 消费」或「走了旁路源 Y」。
3. **对症状表选杠杆**（下表），取杠杆阶梯里**够用的最小一级**。
4. **最小改动**，逐条对照「硬护栏」。
5. **验证与收尾**：`recompile` 后 Console 无本票新增 Error/Exception/Assert；动了 Scheduler / 锚点逻辑则跑（必要时扩）`Tests/PresentationBeatOrderingContractTests.cs`；回复里给用户一句复现路径（QuickTest 通道 `\1`–`\9`（见 `quick-test-effect-channels` skill）或正式局哪一步），请他游玩确认观感；锚点/消费者事实变了同步 `docs/code-map/presentation.md` + `Assets/Docs/Presentation/Flow/05-表演锚点排期与触发脉冲.md`，动了行为不变量则新增/修订 ADR。

## 症状 → 首查 → 典型杠杆

| 玩家观感 | 首查 | 典型杠杆 |
|---|---|---|
| 「旋转/落地前就触发了一下」 | 是否旁路脉冲源；否则查首条 `EffectTriggered` 落在哪个批（早于可见运动的批 → 去重保首条造成"提前"） | 旁路收口；或 Core 侧晚发 / 调整去重取样（阶梯 4–5） |
| 「飘字在旋转中途/之前蹦出来」 | 哪个锚点冲的 Impact：白名单命中帧？秒完成通道 PresentStep？ | 锚点收窄（`FlushImpactExcept` 语义，阶梯 3） |
| 「同一效果闪了两三次」 | `TriggerPulseChainDedup` 是否覆盖该路径；是否双实例模板（defId 不同、去重互不牵连） | 去重键/复位点；合并模板或音频（阶梯 4） |
| 「触发了但没看到反馈」 | 去重静默消费？持有卡不在场（FX 静默丢弃）？机关卡 CoreKind 拦缩放（by design，提示走悬停荧光）？通道被 Reset 成 Null？ | 视情况放行；机关拦截是刻意决策，改前先向用户确认 |
| 「效果结算的批次就不对（先掉血后旋转）」 | Core 侧动作顺序：按 ActionId + CausalDepth 重建因果树 | Core 挂起/动作重排（阶梯 5–6，必须 ADR） |
| 「音效/特效差一点点没对上」 | 绑定延迟 | 阶梯 1，别动锚点 |
| 「脉冲和飘字之间节奏太赶/太拖」 | — | 阶梯 2 |

## 杠杆阶梯（从小到大，够用即止）

1. **绑定延迟**：`Assets/Resources/audio/audio_bindings.json` 绑定 delay、`Assets/Resources/VFX/vfx_bindings.json` `startOffsetSeconds`——几百毫秒内的音画对齐。弹道命中对齐用 `ProjectileVfxCues.PulseFromTo` 回传的 `PresentationPlan`。
2. **节拍参数**：`PresentStep.TriggerCadenceSec`（0.35s，脉冲→飘字间隔）。
3. **批内锚点归属**：Core `Presentation/PresentationEventMap.cs` 的 Beat 声明（Impact/Settled）；各锚点 `FlushImpactExcept` / `FlushImpactOnly` 的 Kind 分工。只能在**同一批内**挪时点。
4. **去重与白名单语义**：`TriggerPulseChainDedup` 的键 `(CardUid, 效果 defId)` 与复位点；给某效果定制「命中帧就演」= 改白名单 = 修订 ADR-0048，不是加一行 Flush。
5. **跨批后置**：pending 是批级的（`OnBatchOpened` 换代），表现侧挪不过批。先例二选一：Core 侧挂起（`BattleScopeSystem.TryDeferBoardMotion`，ADR-0044）或表现隔离区（`QuarantineImpactWhere`，神圣决斗先例）。必须写 ADR。
6. **Core 规则时序**（触发原子/动作顺序本身）：走 `table-nine-effect-landing` skill + ADR。

## 缓存的坑（代码不自明处）

- **链级去重保第一条**：同链同卡同效果多批重复触发时，观感时点 = 首条所在批的两相收束。首条落在无可见运动的批 → 即使两相正确，观感仍像"提前"。
- **两条旁路脉冲源不经 Beat/去重**（ADR-0048 已知遗留）：嘲讽重定向直调 `tauntVictim.PlayEffectTriggerPulse()`（`CardAttackBasicAdapter`）与 Effect SO 回调（`CardEffectManager.DispatchCallback`）。不受两相控制的脉冲先怀疑这里。
- **Handler 链顺序是契约**：飘字必须在卡面 Commit 前（装配处 `PresentationCompositionRoot.InstallBattleBeatScheduler`），调序即无声坏。
- **效果模板双份同步**：Editor 加载 `Assets/Arts/ContentVisual/tables/effect_templates.json`，Player 加载 `Assets/StreamingAssets/ContentVisual/tables/`（`ContentCatalogTableLoader`）；编辑器保存双写。改模板须两份都落，只改一份 = Editor/Player 行为分叉。

## 硬护栏（对照后再动手）

- 白名单以外**禁新增提前 Impact 冲刷点**。现有仅 3 个：攻击/反击命中帧（`FieldBattlePresentationExecutor`）、Drain 首个 Remove 前（`BoardPresentationPlayer`）、神圣决斗惩罚帧（全量 + 隔离区）。
- Core 对每个 qualifying 动作发 `EffectTriggered` 是规则事实（光环 refresh 必须逐批重算）——收敛观感在表现层做，砍 Core 事件伤规则与日志。
- `CausalDepth` 由 `ActionPipelineSystem` 独占盖章，业务动作不得自写。
- 卡面数值只经指令绝对值在锚点 Commit（ADR-0005），时序调整不得顺手直读 Core 写卡面。

## 文件地图

| 责任 | 文件 |
|---|---|
| 批内两相收束（可见顺序权威）+ 节拍常量 | `Flow/Presentation/BatchLockstepSteps.cs`（`PresentStep`） |
| pending 队列 / 分发 / 隔离区 / 未消费诊断 | `Flow/Presentation/BattleBeatScheduler.cs`（门面 `BattleBeatFlush`、静态桥 `BattleBeatHook`） |
| 白名单锚点①② 命中帧 / ③ 决斗惩罚帧 | `Cards/Battle/FieldBattlePresentationExecutor.cs` |
| 白名单锚点 Drain 首个 Remove 前 | `Flow/BattleSession/BoardPresentationPlayer.cs` |
| 触发脉冲消费 + 音/VFX 路由 | `Flow/Presentation/EffectTriggerPulseBeatHandler.cs` |
| 链级去重（键与登记） | `Flow/Presentation/TriggerPulseChainDedup.cs`；复位点在 `PresentationDirector.cs` |
| 事件 → 锚点（Impact/Settled）映射 | Core `Presentation/PresentationEventMap.cs` |
| Core 效果触发 / `EffectTriggered` 产生 | Core `Effects/EffectSystem.cs`；盖章 `Systems/ActionPipelineSystem.cs` |
| 交战窗盘面运动挂起（跨批先例） | Core `Systems/BattleScopeSystem.cs`（ADR-0044） |
| 时序契约测试 | `NineGrid.Presentation/Tests/PresentationBeatOrderingContractTests.cs` |

（表现层路径均相对 `Assets/Scripts/NineGrid.Presentation/`，Core 相对 `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/`。）

## 权威文档（按需展开，勿在此复述）

- `Assets/Docs/Presentation/Flow/05-表演锚点排期与触发脉冲.md` —— 锚点/Handler 链/脉冲通道全量事实
- `Assets/Docs/Presentation/Flow/03-表演导演时间线与编排调度.md` —— Director/Timeline/Step/通道/调度骨架
- `docs/adr/0048-two-phase-impact-and-trigger-chain-dedup.md`（两相 + 去重 + CausalDepth）、`0044`（交战窗挂起）、`0018`（触发可见因果）、`0001`（Batch-ack）
