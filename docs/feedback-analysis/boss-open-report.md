# 策划试玩反馈三方比对分析：层主房（Boss 房）开局发牌

> 比对日期：2026-08-08
> 范围：仅探查，无任何文件改动（本文档除外）。
> 三方来源：① 权威文档（`CONTEXT.md` / `docs/adr/` / `docs/code-map/`）；② 代码实施（`NineGrid.Core` / `NineGrid.Presentation` / `NineGrid.Content`）；③ 策划设计案（`Assets/Docs/九宫格登神/`）。

## 反馈原文

> 「层主开局直接翻面就发他这一张牌，发完后8个空格子点哪个都没用」

拆解为三个症状：

| 症状 | 玩家描述 | 指涉 |
|------|----------|------|
| A | 层主开局「直接翻面」 | 层主房开局，层主卡进场即背面 |
| B | 「就发他这一张牌」 | 开局只发了一张牌，其余 8 格空 |
| C | 「8个空格子点哪个都没用」 | 空格点击无响应，全盘无法推进 |

---

## 结论摘要

| 症状 | 判定类型 | 一句话结论 |
|------|----------|------------|
| A. 层主开局直接翻面 | **部分属实 + 需进一步分析** | 设计上翻面卡组（`deck.skeleton_legion` 层主「潜水鳄」挂刺客领袖）确实令「打出到格子的怪物卡」翻为背面，但实现里 `EventCards` 目标**排除自身**（ADR-0010 形态固定），正常批内翻的是**同批其他怪物**；层主**自身**开局背面只有两条低概率/异常路径：① Flip 动作 targets 为空时的 **Owner 自翻兜底**（`EffectAtomLibrary.cs:3266-3277`）；② 表现镜像分叉被玩家误读。需复现证据 + 与策划确认「领袖自身是否允许入场即翻」。 |
| B. 只发一张 | **与三方均冲突，需要进一步分析** | 设计案（发牌机制 9 格全满）、ADR-0034（稳定化不变量）、Core 实施（`OpeningDealAction` + `FillEmptySlotsAction` + `ResolveUntilStable`）均无「只发一张」路径——Core 开局必然 8 卡 + 玩家卡。症状更可能是**表现层开局表演停摆**（`OpeningPresentationActive` 门禁挂起 → 点击全拒）或既有「Core 有牌、屏幕没牌」类缺陷（nullView / placeDenied 发牌失败回滚清占格）在层主房被开局批内翻面叠加放大。需要复现日志/截图。 |
| C. 点空格没用 | **设计空操作（非独立 bug）＋ 一处静态镜像缺口** | 空格点击在设计与实现里都是**无规则效果的纯反馈**（`交互机制.md` + `ClickEmptySlotAction` 只发 `EmptyClicked`），「没用」是 A/B 的后果而非独立缺陷；但叠加一个真实的静态缺口：**开局批内的 `CardFaceChanged` 无表现消费路径**（`CardFaceGenerationBootstrap` 只消费生成类事件），Core 背面怪视图正面 → 玩家点击「看着正常」的怪被 `Attack` 门禁以 face-down 拒绝——「点哪个都没用」的直接机制之一。 |

**总判定**：反馈三症状中最硬的事实是「开局批内翻面在表现层无镜像消费路径」（可验证的落地缺口）；「只发一张」在 Core 无路径，指向表现层开场停摆或已知 nullView 类缺陷，必须靠复现证据收敛；「层主开局直接翻面」需与策划确认领袖自身入场语义（自翻兜底 vs 只翻他怪）。

---

## 症状 A：层主开局直接翻面

### ① 设计案原文

- `02-机制/翻面机制.md`：「所有卡牌默认**正面朝上**打出到九宫格（**除非效果另有说明，如刺客领袖**）」；「背面默认**双向惰性**：背面卡不可被伤害、不推进/不开火，被动与常规技能默认**不生效**」（ADR-0016 已写入策划案）。
- `04-敌人侧卡牌信息/怪物技能.md:67-68`：**刺客领袖** = 「[场上]若本卡处于正面，**所有打出到格子上的怪物卡以正面朝下**」。
- `04-敌人侧卡牌信息/怪物卡.md:46`：翻面怪物卡组（第 4 套）层主 `boss4`（30 血 3 攻）技能 = 普通近战 / **刺客领袖** / 起来；序列 5 固定为该卡组层主（`:5`）。
- 设计案**没有**「层主房开局只发一张」或「层主入场即背面」的专门条款——层主房走标准战斗流程（`07-流程/战斗关卡流程.md`），开局发牌按 `发牌机制.md` 九格全满。

### ② 权威决策（ADR / CONTEXT）

- **ADR-0016（牌面朝向权威与背面惰性）**：朝向权威在 Core（`CardInstance.FaceUp` 默认正面）；背面双向惰性；「主动翻开」= 交战中场上**正交相邻**的背面卡可消耗互动翻开（`RevealFace`）；翻牌编排须串行门控（`FlipPlaybackCoordinator.IsIdle`）。
- **ADR-0010（自管效果责任）**：`EventCards` 形态固定——**排除 Self**（`EffectAtomLibrary.cs:1147-1149` 注释「排除 Self / trap.* / 已背面」；`:1186-1189` 实现）。
- **ADR-0026（离开机关唯一清关）**：层主房 = 「击破本节点**开局编入**的层主后才洗入」离开机关（`:13`）——层主是开局编入对象（Boss Counter），是全房流程的前提。
- **CONTEXT.md:253-254**：同 ADR-0026；Avoid「局中新生层主冒充开局层主」。
- **ADR-0029（七套正式卡组槽位契约）**：`deck.skeleton_legion` 是正式卡组之一，其序列 5 = 带刺客领袖的层主。

### ③ 代码实施路径

- **内容挂载**：`Assets/Arts/ContentVisual/cards/monster_orc_commander.json`（显示名「潜水鳄」，`deckId=deck.skeleton_legion`，`sequence=5`，`isBoss=true`）挂 `skill.assassin_leader.rule` + `skill.rise_up.damage`——**七套正式卡组中唯一带刺客领袖的层主**。
- **效果模板**：`Assets/StreamingAssets/ContentVisual/tables/effect_templates.json` `tpl.skill.assassin_leader.rule` = `Triggered`（trigger `OnDeal`）+ 条件 `IsFaceUp(Self)` + `EventFilter(CardDealt, Monster)` → 目标 `EventCards(CardDealt, Monster)` → 动作 `Flip`。
- **触发一次 / 批**：`TriggerSystem.Dispatch`（`TriggerSystem.cs:128-161`）对**每个动作每个触发点调用一次**，携带该动作整批事件——开局 `FillEmptySlotsAction` 一次补 5 张（5 个 `CardDealt` 事件）即一次派发。
- **目标解析排除自身**：`EventCardsTarget.Resolve`（`EffectAtomLibrary.cs:1150-1218`）——跳过 `CardUid == OwnerUid`（`:1186-1189`）、非盘面格、非 Monster、`trap.*` 前缀、已背面。→ **正常路径翻的是「同批除领袖外的其他正面怪物」**。
- **自翻兜底（关键实现细节）**：`FlipEffectAction.BuildActions`（`EffectAtomLibrary.cs:3259-3289`）——targets 为空时**翻转 Owner 自身**（`:3271-3274`）。即：当开局补牌批内**除层主外没有其他可翻怪物**（其余 4 张全是机关/帮助卡）时，层主会**自翻入场背面**。概率低（node 8 池 15 怪 + 3 机关 + 玩家侧帮助卡，批内 5 张恰为「层主 + 4 张非怪」≈ 0.3%），但这是代码中**唯一能让层主自身开局背面的 Core 路径**。
- **契约测试**：`NineGrid.Core.Tests/Batch3HardSkillsRegressionTests.cs:64-74` 锁定了「正面领袖在场时新怪经 OnDeal→Flip 翻到背面」「机关卡不应被刺客领袖强制翻面」（`:166`）——但**没有**「批内无其他怪物时领袖自翻」的用例（该兜底分支无测试覆盖）。

### 判定

**部分属实 + 需要进一步分析。**

1. 「开局直接翻面」在**设计意图内**（翻面卡组身份：领袖正面在场 → 打出到格的怪物卡背面）；实现方向一致，但**只翻同批其他怪**（排除自身）。
2. 「层主**自身**开局背面」无明确设计条款，代码中仅 `Flip` 兜底自翻分支可达（低概率、无测试覆盖、未经策划确认）。需策划明确：刺客领袖入场时自身是否也算「打出到格子上的怪物卡」（设计文「所有…怪物卡以正面朝下」字面可含自身）。
3. 若玩家实际看到的是「**其他怪物**开局翻面」，则该现象是**设计内行为**（翻面卡组的核心体验），需在报告里与「层主自身翻面」区分。

---

## 症状 B：只发一张（8 空格）

### ① 设计案原文

- `02-机制/发牌机制.md:4-11`：开局发牌四步——玩家侧 3 张直摆 → 怪物侧 3 张（**存在层主则必定抽出**）→ 双方洗入 + 3 张常规机关成战斗卡组 → 抽到无空格。「**开局场上共8张卡 + 玩家卡 = 9格全满**」（`:10`）。
- `02-机制/发牌机制.md:12-14`：空格补牌自动（有牌必补）；`:17-19`：仅当战斗卡组耗尽才允许永久空格。
- 设计案**不存在**「层主房开局只发一张」的任何条款。

### ② 权威决策（ADR / CONTEXT）

- **ADR-0034（盘面稳定化）**：`BoardStabilizationSystem` 以 Core 盘面/牌堆判断欠补位，统一覆盖**开局**、击杀、非击杀移除等路径；每切片至多一轮 Fill，稳定化串行到「无空格或牌堆空」。`PhaseSystem.StartNode` 在开局 Fill 后显式 `ResolveUntilStable()`（`PhaseSystem.cs:174`）。
- **ADR-0026 / CONTEXT:252-254**：层主房唯一特殊点是「离开机关插入时机」（击破开局层主后洗入），**不改变开局发牌数量**。
- 补牌问题历史结论（`Assets/Notes/补牌问题全方面研究探查.md:13`）：18 次 StartNode 的 boardOccupantCount 17 次 = 8（仅节点 3 曾因捕熊/一圈语义 = 7，已被 ADR-0034 稳定化收口）。

### ③ 代码实施路径

- **装填**：`RewardSystem.BuildNodeDeckOptions`（`RewardSystem.cs:279-328`）node 8 → `PlayerOpeningCount=3 / EnemyOpeningCount=3 / RequireElite=true`（`:310-316`，`seq5=1` 时强制开局抽选含层主）；怪卡按节点表 4/4/3/3/1（`node_deck_rules.json` node 8），常规机关随机 3 张（`RegularTrapPool.cs:16-17`）。
- **开局发牌**：`OpeningDealAction`（`BoardDeckActions.cs:196-226`）——玩家侧 3 张 `PlacePlayerCardsDirectly` 随机直摆（`:204,:233-271`）→ `SelectCards` 必选精英/层主 + 补足 3 张（`:208,:273-303`）→ 全部洗入抽牌堆 → `FillEmptySlotsAction`（`:365-423`）按序补满。
- **稳定化收尾**：`PhaseSystem.StartNode`（`PhaseSystem.cs:140-176`）——`OpeningDealAction`（`:167`）→ `FillEmptySlotsAction`（`:168`）→ `ResolveUntilStable`（`:174`）。**Core 侧不存在「只发一张」的实现路径**：批内效果腾出的空位也会被稳定化下一片补齐。
- **QuickTest 通道**：`\8` = 刺客领袖通道（`QuickTestDeckCatalog.cs:84-87`），乱序节点队列含节点 8（`QuickTestRunPlanner.cs:47-69`）——`\8` 第一战**可能直接是层主房**（1/6），技能在 `StartBattleNodeAsync` **之后**才挂载（`GameFlowOrchestrator.cs:295-303`），不改变开局发牌数量。

### 判定

**与三方均冲突（设计案 / ADR / Core 实施都保证 9 格全满），需要进一步分析。**

Core 无「只发一张」路径，症状 B 只能来自表现层或复现环境差异，候选机制按可能性排序：

1. **表现层开局表演停摆**：`PresentOpeningAsync`（`BattleSessionExecutor.Opening.cs:635-881`）全程持有 `PresentationInputGates.SetOpening(true)`（`:639`，`finally` 才解除 `:875`）——若发牌飞行 `await` 卡死（`WaitDealFlightsSettledAsync` / 占格 ghost / 翻牌编排门控 ADR-0016 §9），已落格只有先发的几张，且**之后所有点击被门禁拒收**——恰好合成「只发一张 + 点哪个都没用」。
2. **既有「Core 有牌、屏幕没牌」缺陷类**：`补牌问题全方面研究探查.md:27` 记录的 nullView / placeDenied 发牌失败（`DealCardByUidWithFlightAsync` 失败 → 回滚清占格），在层主房因**开局批内多卡翻面**与多卡批叠加而更易触发；`ChoreoTraceSink.SafeEmitAnomaly("DealPlaceDeniedSticky"...)`（`BoardPresentationPlayer.cs:532-538`）会留下痕迹。
3. **复现环境差异**（如 QuickTest `\8` 通道第一战即节点 8 + 翻面卡组组合）。

**取证建议（不修不验不能定案）**：一次复现的核心日志（`corelog-*.json` 的 `StartNode` 后 `boardOccupantCount` / `SlotsFilled`）、`registrylog` 的 `FieldVisualGap` / `DealPlaceDeniedSticky` / `DealGhostVacate` 记录、以及截图。

---

## 症状 C：空格点击无响应

### ① 设计案原文

- `02-机制/交互机制.md:6-10`：三种九宫格互动——点击怪物交战 / 点击道具拾取 / **点击相邻空格**（仅正交相邻可用）。「点击空格」在设计中是**合法但无规则效果**的互动（无移格、无旋转、无计数含义）。

### ② 权威决策（ADR / CONTEXT）

- **ADR-0004（两轴门禁）**：所有点击唯一经 IntentIntake 收口；时序互斥（`MainlineBusy`）+ 输入所有权裁决；忙时「缓冲最新一条 / 拒收」。
- **ADR-0023（格位命中框与认领）**：命中框恒开，合法性交 IntentIntake；「点了没反应」从几何黑洞变成**显式拒绝记录**（`:58`）——空格命中合法、语义为空。
- **ADR-0016 §4**：背面怪**不可被伤害**——玩家 `Attack` 命中背面怪被拒（`PhaseSystem.cs:240-243`「Attack target is face-down」）；邻接背面怪应走主动翻开（`RevealFace`，`PhaseSystem.cs:647-686`，`IntentIntake` 分流见 `AttackInputController.cs:37-72`）。

### ③ 代码实施路径

- `ClickEmptySlotAction`（`PhaseActions.cs:445-461`）：只发 `EmptyClicked` 事件，**无任何规则效果**——「点空格没用」与设计一致，不是独立缺陷。
- **静态镜像缺口（本症状真正值得修的落地缺陷）**：开局批内的 `CardFaceChanged`（刺客领袖翻面）在开局表现路径**没有消费方**——`PresentOpeningAsync` 只做发牌飞行 + `CommitAllSpawnedCards` + `CardFaceGenerationBootstrap.ApplyFromEventLog`（`BattleSessionExecutor.Opening.cs:873-880`），而 `CardFaceGenerationBootstrap.IsGenerationFaceEvent` 只认 `CardSpawned/CardDealt/AvatarAppeared/倒计时`（`CardFaceGenerationBootstrap.cs:79-92`），**不含 `CardFaceChanged`**；`PresentationEventMap` 中 `CardFaceChanged → UpdateFaceUp → Settled`（`PresentationEventMap.cs:93`）只在常规 Beat 管线消费。→ **Core 背面 ↔ 视图正面镜像分叉在开局批内静态可达**：玩家看到「正面」怪物，点击被 Attack 门禁以 face-down 拒绝；若层主在非邻接位又不可翻开，全场无可交互对象——「点哪个都没用」的完整闭环。

### 判定

**设计空操作（非独立 bug）＋ 一处可验证的静态镜像缺口。**

1. 空格点击无效果 = 设计语义（三方一致），不构成缺陷。
2. 「点哪个都没用」的真凶更可能是：开局批内翻面无表现消费路径（Core/视图镜像分叉）+ 症状 B 的表演停摆（门禁挂起）。前者是**可静态验证的缺口**（开局路径对 `CardFaceChanged` 无处理，与 ADR-0016「表现层只 Commit 镜像」的权威要求冲突），建议先修：开局收束时对批内 `CardFaceChanged` 走一次 `UpdateFaceUp` 镜像提交（复用 `CardFaceFlipBeatHandler` 或 `FlipPlaybackCoordinator` 串行播放），再谈复现。

---

## 附：相关但独立的发现（本报告不断言）

### 附 1：层主「必定抽出」与「必定上场」的语义差

- 设计 `发牌机制.md:6`：「怪物侧卡组抽出的**三张卡**里，有一张为层主」——层主**必在开局三张怪卡中上场**。
- 实现：`SelectCards`（`BoardDeckActions.cs:273-303`）在 `RequireElite` 时**必选**层主，但选中的 3 张**追加进抽牌堆后整堆洗牌**（`:213,:217-220` + `ShuffleDrawPile :324-336`）——层主只保证**进抽牌堆**，不保证**开局出场**（约 24% 概率出现在开局补牌的前 5 张里，其余时候中后期补出）。
- ADR-0026 措辞「开局**编入**的层主」（Boss Counter 登记在 `SetupNodeDeckAction`，`BoardDeckActions.cs:56-60`）与设计案「必定抽出上场」存在**口径差**。若策划预期「层主房开局必须见到层主」，这是需要立项的装填顺序调整（选中敌卡置顶/直摆，与玩家侧直摆对称）。

### 附 2：QuickTest `\8` 通道与正式层主房的重合风险

- `\8`（刺客领袖通道）乱序节点序可能**首战即节点 8 层主房**（`QuickTestRunPlanner.cs:16-26`）；若该层主题恰为 `deck.skeleton_legion`，场上会同时存在「原生刺客领袖（层主）」+「通道后挂载的刺客领袖」，翻面行为重叠放大，最易复现症状 A/B/C 的组合。取证时优先记录 `QuickTest` runTag 与首战内容节点。

---

## 涉及文档清单

- 设计案：`02-机制/发牌机制.md`、`02-机制/翻面机制.md`、`02-机制/交互机制.md`、`04-敌人侧卡牌信息/怪物技能.md`、`04-敌人侧卡牌信息/怪物卡.md`、`07-流程/卡组生成流程.md`、`07-流程/战斗关卡流程.md`
- ADR：0004（两轴门禁）、0010（自管效果责任）、0016（牌面朝向权威）、0023（格位命中框与认领）、0026（离开机关唯一清关）、0029（主题牌组槽位契约）、0034（盘面稳定化）
- 代码：`RewardSystem.cs`、`BoardDeckActions.cs`、`PhaseSystem.cs`、`PhaseActions.cs`、`EffectAtomLibrary.cs`、`TriggerSystem.cs`、`CardFaceGenerationBootstrap.cs`、`BattleSessionExecutor.Opening.cs`、`PresentationEventMap.cs`、`BoardPresentationPlayer.cs`、`QuickTestDeckCatalog.cs`、`QuickTestRunPlanner.cs`、`monster_orc_commander.json`、`effect_templates.json`
