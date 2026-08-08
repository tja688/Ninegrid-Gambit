# 策划试玩反馈三方比对报告：离开机关 / 离开房间流程

- 日期：2026-08-08
- 范围：仓库权威文档（CONTEXT.md + docs/adr/ + docs/code-map/）× 代码实施（NineGrid.Core / NineGrid.Presentation / NineGrid.Content）× 策划设计案（Assets/Docs/九宫格登神）
- 性质：只探查，零改动

---

## 结论摘要

| 条目 | 判定 | 一句话 |
|------|------|--------|
| 1. 离开机关直接对应一种房间类型、攻击后直接进入该类型房间、每次洗入两张 | **设计变更建议（非 bug）**，属「设计冲突 / 需进一步分析」 | 现状三处全不符：离开机关与房间类型**零绑定**；击破后**不直接进入**（必须走图标 + 驻留 1s 选房）；每节点**只洗入 1 张**（幂等）。建议若采纳将与 ADR-0021 / ADR-0026 / CONTEXT「地图节点」词条多处冲突。 |
| 2. 离开后还有个空房间要再点一次离开才进下一关，纯多此一举 | **实现忠实于设计案，但设计案本身多此一举**（设计决策问题，非实现 bug） | 节点 4 是 ADR-0021 明文规定的「非战斗导航缓冲」：空棋盘 + 1 个离开图标（走格 + 驻留 1s）。实现与设计案逐字一致；玩家嫌的是节点 4 这个设计步骤本身。 |

---

## 条目 1：离开机关直接对应一种房间类型 / 击破后直接进入 / 每次洗入两张

### 1.1 设计案原文

- `Assets/Docs/九宫格登神/07-流程/层级流程.md`（节点表）：清关后「两个**战斗**房间选项」「两个**消费**房间选项」「两个**特殊**房间选项」「1 个**离开**图标」「1 个**层主房**图标」——房间族由**节点序号**决定，与离开机关卡无绑定。
- `Assets/Docs/九宫格登神/02-机制/战斗机制.md:41`：击破「离开」技能的离开机关卡为唯一清关条件。
- `Assets/Docs/九宫格登神/04-敌人侧卡牌信息/机关卡.md`：机关表仅 6 常规 + 3 特殊（治疗泉/烈焰/复活石），**离开机关不在表内**——它是 ADR-0026 引入的机制卡，设计案机关表未收录。
- 设计案没有任何「离开机关 ↔ 房间类型」「洗入两张」的表述。

### 1.2 权威决策（ADR / CONTEXT）

- **ADR-0026「离开机关为战斗房唯一清关手段」**：
  - 插入时机（默认战斗房）：击破数达 **⌈N/2⌉** 时，将离开机关卡**洗入战斗卡组随机位置**（单张语义）。
  - 插入时机（层主房例外）：击破**开局编入的层主**后才洗入。
  - 击破后**直接完成本次战斗（清关），无需再点离开图标**——该「直接」只指战斗内不再多一步点击，**不指**免选房直接进下一房。
  - 清关收场：清残留不兑金、道具卡格保留。
- **ADR-0021「跑图进度与节点编排归 Core」**：节点 1/2/5 清关放 2 战斗图标、节点 3 放 2 消费图标、节点 6 放 2 特殊图标、节点 8 放 1 下楼图标；`CompleteNodeIfCleared` 删掉旧「无清关三选一」，清关直接放房间图标（注意：仍保留 **RoomChoice 选房**环节，只是从三选一改图标二选一/导航）。
- **CONTEXT「地图节点」词条**：「节点序号决定『清关后放出哪一类房间选项』」——房间类型归节点编排，不归离开机关。

### 1.3 代码实施（现状事实）

**a. 洗入数量：每节点 1 张，幂等**

- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Systems/DeckSystem.cs:99-142` `ReactToTrueMonsterKillForLeaveTrap`（OnKill Post 触发器）：
  - 非层主房计开局真怪击破、层主房只认开局层主击破（:119-129）；
  - 满足条件后 `MarkLeaveTrapInserted()` + `ShuffleIntoDrawPileAction(LeaveTrapDefId, CardKind.Trap, **1**, false, "leaveTrap.insert")`（:137-141）——**count 硬编码 1**。
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Models/BattleContextModel.cs:129-148` `ShouldInsertLeaveTrap`：`IsLeaveTrapInserted` 短路（:131）→ **每节点至多洗入一次**；非层主房阈值 `(OpeningTrueMonsterCount + 1) / 2` = **⌈N/2⌉**（:146），层主房 = `IsOpeningBossDefeated`（:138）。

**b. 离开机关卡与房间类型：零绑定**

- `Assets/StreamingAssets/ContentVisual/cards/trap_leave.json`：`kind: Trap`，装配仅 `tpl.trap.door` / `tpl.trap.magic_immunity` / `tpl.trap.leave`；无任何 Room 字段（无 `weight`/`openingInjects`/`rewardPoolId` 语义绑定），不进房间表。
- 房间类型权威在 `Assets/StreamingAssets/ContentVisual/cards/{Shop,Tavern,Gold,Treasure,...}.json`（`Room` 投影）与 `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/MapNodeProgression.cs:84-95`（节点编排表）。

**c. 击破后流程：不直接进入，走 RoomChoice 图标**

- 击破触发链：`tpl.trap.leave`（`effect_templates.json:1459-1465`，OnFatalDamage → `MarkLeaveTrapBroken`）→ `BattleContextModel.MarkLeaveTrapBroken`（EffectActions.cs:1615-1622）→ `IDeckSystem.IsNodeCleared`（DeckSystem.cs:93-97）= `IsLeaveTrapBroken`。
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Systems/PhaseSystem.cs:1847-1872` `CompleteNodeIfCleared`：ClearCheck → NodeCompleted → `NodeCompletedAction` → 清场不兑金（`SettleUnusedHelpCards` / `ClearResidualTraps`）→ `ChangePhaseAction(GamePhase.RoomChoice)` → `EnqueuePostClearOffers`（:1874-1885）：导航图标（节点 8 下楼）或 `OfferRoomChoicesAction`（2 个房间图标）。
- `RewardSystem.cs:149-186` `RollPostClearRoomChoices`：BattleRooms / ConsumerRooms / SpecialRooms 各 roll **2** 个（不足 Pad），Boss 1 个。
- 表现侧进房必须「走格 + 驻留」：`RoomIconBoardPresenter.TrySpawnFromPending`（RoomIconBoardPresenter.cs:78-140，导航/房间图标落格，`WalkDestination` 角色）→ Avatar 走格 → 驻留 1s（`RoomIconDwellSession.DefaultDwellSeconds = 1f`，RoomIconDwellSession.cs:9）→ `SelectRoom` + `EnterRoom`（PhaseSystem.cs:1381-1471：`SelectRoomChoiceAction` → RoomEvent → `EnterRoom` → `ResolveRoom` → `AdvanceNodeAction`；消费/特殊房进 `RewardItemChoice` 房内会话）。

### 1.4 三方差距矩阵（建议 vs 现状）

| 建议点 | 现状实现 | 差距 | 涉及权威 |
|--------|----------|------|----------|
| 离开机关**直接对应一种房间类型** | 无绑定：房间族由节点序号决定（`MapNodeProgression.cs:84-95`；CONTEXT「地图节点」词条） | **全差距**（新机制概念） | 与 CONTEXT「地图节点」、ADR-0021 节点编排表冲突 |
| **攻击（击破）后直接进入**该类型房间 | 击破 → `CompleteNodeIfCleared` → RoomChoice 放图标 → 走格 + 驻留 1s → 进房（PhaseSystem.cs:1847-1872 + RoomIconBoardPresenter.cs + RoomIconDwellSession.cs:9） | **全差距**（取消选房/走格驻留） | 与 ADR-0021「清关放图标选房」冲突；节点 1/2/3/5/6 的**二选一**自由度将被取消，节点 3/6 的消费/特殊房二选一如何保留需策划明确 |
| **每次洗入两张** | 每节点洗入 **1** 张且幂等（`DeckSystem.cs:140` count=1；`BattleContextModel.cs:131` 幂等短路） | 数值差距（改动面最小：count 参数 + 幂等逻辑，但影响节奏与 ⌈N/2⌉ 后的软锁概率） | ADR-0026「插入时机」条款 |

### 1.5 附带约束

- 测试护栏会挡：`LeaveTrapInsertContractTests`（⌈N/2⌉、奇数上取整、**幂等**单张）、`FormalRunSmokeContractTests`（#142：正式流程 = SelectRoom+EnterRoom 推进、节点 4/7 非战斗）、`MapNodeProgressionContractTests`。任何一档改动需连带修订这些契约测试（见 `docs/code-map/tests.md`）。
- 若「直接进入」落地，节点 8 清关后的「下楼」、节点 4/7 的导航图标是否需要保留同样要一并定义（否则同一机制两套语义）。

---

## 条目 2：离开后还有个空房间要再点一次离开才进下一关

### 2.1 设计案原文（节点 4 就是那个「空房间」）

- `Assets/Docs/九宫格登神/07-流程/层级流程.md:16`：「进行第4地图节点，为**非战斗导航缓冲**（1 个离开图标；不发牌、无清关判定），由离开图标推进至节点5」；节点表 :32「4 | 非战斗**导航缓冲** | 1 个**离开**图标 | → 节点5随机战斗房」。
- `Assets/Docs/九宫格登神/07-流程/战斗关卡流程.md:16`：「节点4 放 1 个**离开**导航图标推进至节点5」。
- `Assets/Docs/九宫格登神/00-基础设计文档（如果你是AI不要管这里）/循环.md:21`：同。

### 2.2 权威决策

- **ADR-0021** 节点编排表：节点 4 = 前房选定（**非战斗**）→ 清关/结束后放出 **1 个离开图标**；「非战斗节点（4/7）**不进 `InteractionLoop`**：没有发牌、没有怪、没有清关判定。节点 4 靠离开图标推进」；后果：「`GoUp` 上一层图标存在但不接线，跑图为单向向下」。
- CONTEXT「房间」词条：「节点 4（离开）与节点 7（层主房图标战前缓冲）**不进 `InteractionLoop`**，没有发牌与清关判定——节点 4 靠离开图标推进」。

### 2.3 代码实施（节点 4 全链路，与设计案逐字一致）

1. `GameFlowOrchestrator.RunNodeCycleAsync`（GameFlowOrchestrator.cs:175-243）：`MapNodeProgression.EntersInteractionLoop(coreNodeIndex)` 分支（:185）——节点 4 走 `PlayNonCombatNodeAsync`（:209）。
2. `PlayNonCombatNodeAsync`（:400-435）：`phase.StartNode(...)` 非战斗路径——`StartNonCombatNode`（PhaseSystem.cs:178-202）：ResetNode → NodeStarted → RoomChoice → `OfferNavigationAction(NavigationKind.Leave)`（:186-190）。无发牌、无清关判定。
3. `PlayRoomIconChoiceAsync`（:441-542）→ `RoomIconBoardPresenter.TrySpawnFromPending`（RoomIconBoardPresenter.cs:103-107）：Navigation → 1 个图标，`Leave.json` 配置 `iconPrefab: Assets/Prefabs/地形图标/离开图标.prefab`、`boardSlot: 2`（`Assets/StreamingAssets/ContentVisual/cards/Leave.json:91-92`）；角色 `WalkDestination`（:134）。
4. 玩家走格 + 驻留 **1s**（`RoomIconDwellSession.DefaultDwellSeconds`，RoomIconDwellSession.cs:9）→ `SubmitSelectAndEnterAsync`（RoomIconBoardPresenter.cs:284-335）→ `SelectRoom(0)`（PhaseSystem.cs:1389-1399：`SelectNavigationAction` + RoomEvent）→ `EnterRoom()`（:1428-1441：`AdvanceNodeAction`）→ NodeCompleted → 主循环进入节点 5（战斗）。
5. 悬停文案「离开本房」（`BoardBriefTipCopy.cs:12`，与房内商店/卡店/奖励房/属性房离开图标同款文案）。
6. 与「房内离开图标」是**同一套机械**：商店/卡店/特殊房/属性房离开图标（ShopBoardPresenter.cs:505 / TavernBoardPresenter.cs:587 / RewardBoardPresenter.cs:365 / AttributeBoardPresenter.cs:214）→ `SkipHelpChoice` → `AdvanceNode`（PhaseSystem.cs `ResolvePostRewardChoiceFlow` :1918-1932）。

### 2.4 判定

- **实现与设计案一致**：节点 4 的空棋盘 + 离开图标 + 走格驻留，全部是 ADR-0021 / 层级流程.md 明文规定的行为；`FormalRunSmokeContractTests`（#142）还专门断言「节点 4/7 非战斗推进不进 InteractionLoop」。
- **玩家抱怨的正是设计案本身**：清关（或离开消费房）后 → 节点 4 空房 → 再走格 + 驻留点一次「离开本房」→ 才到节点 5。玩家预期「离开后直接进下一关」，现状多出「一个空房间 + 一次离开操作」。
- 差距本质：**预期「离开即进下一节点」 vs 设计「节点 4 单独占一个地图节点做导航缓冲」**。
- 备注：节点 7（层主缓冲）的保留理由在 ADR-0021 中有明确交代（「给玩家心理准备，不再直接开战」）；**节点 4 的缓冲理由 ADR 与设计案均未说明**（`NodesPerFloor` 9→8 重排后遗留，见 ADR-0021），与玩家「纯多此一举」的判断一致，需要策划裁决去留。
- 若取消节点 4（节点 3 消费房/特殊房离开后直接进节点 5 战斗），将修改 ADR-0021 节点编排表、`MapNodeProgression.cs:90`、`node_deck_rules.json` 结构预期及节点 4 相关契约测试——但节点 7 的缓冲语义不受影响（理由独立）。

---

## 两条联动的提示（供策划/后续票参考，非本次结论）

1. 条目 1 的「击破离开机关直接进入该类型房间」若与条目 2 的「取消节点 4 空房」同批评估，会合成一个候选新流程：「战斗清关 = 击破离开机关 → 按节点直接进入下一房（免选房图标、免节点 4 空房）」。但需先回答：节点 1/2/5 的**战斗房二选一**、节点 3/6 的**消费/特殊房二选一**、节点 8 的**下楼**、节点 7 的**层主缓冲**如何与「直接进入」共存——它们目前都依赖 RoomChoice 图标环节（ADR-0021）。
2. 「洗入两张」是三条建议中改动面最小的一条（`DeckSystem.cs:140` count + 幂等位），但它改变 ⌈N/2⌉ 之后的期望出场轮次与软锁概率，属节奏数值决策，宜与「是否保留节点 4」分开决策。

---

## 附：关键证据文件:行索引

| 事实 | 位置 |
|------|------|
| 洗入 count=1、幂等 | `NineGrid.Core/Systems/DeckSystem.cs:137-141`；`NineGrid.Core/Models/BattleContextModel.cs:129-148` |
| ⌈N/2⌉ 阈值 | `BattleContextModel.cs:146` |
| 离开机关卡 JSON（无房间字段） | `Assets/StreamingAssets/ContentVisual/cards/trap_leave.json` |
| 离开技能模板 | `Assets/StreamingAssets/ContentVisual/tables/effect_templates.json:1459-1465` |
| 清关 → RoomChoice → 放图标 | `NineGrid.Core/Systems/PhaseSystem.cs:1847-1885` |
| 房间二选一 roll | `NineGrid.Core/Systems/RewardSystem.cs:149-186` |
| 节点编排表（节点 4/7 非战斗、各节点放什么） | `NineGrid.Core/Domain/MapNodeProgression.cs:84-95` |
| 节点 4 非战斗 StartNode → OfferNavigation(Leave) | `PhaseSystem.cs:178-202` |
| 非战斗节点不进 InteractionLoop（壳） | `NineGrid.Presentation/Flow/GameFlow/GameFlowOrchestrator.cs:175-243, 400-435` |
| 图标走格 + 驻留 1s | `NineGrid.Presentation/Flow/RoomIcons/RoomIconBoardPresenter.cs:233-335`；`RoomIconDwellSession.cs:9` |
| 导航 Select/Enter → AdvanceNode | `PhaseSystem.cs:1381-1441` |
| 离开图标配置 | `Assets/StreamingAssets/ContentVisual/cards/Leave.json:91-92`；`NineGrid.Presentation/Cards/CardChassisPaths.cs:32` |
| 设计案节点 4 缓冲 | `Assets/Docs/九宫格登神/07-流程/层级流程.md:16,32`；`战斗关卡流程.md:16` |
| ADR-0021 节点编排 / 4·7 非战斗 / GoUp 不接线 | `docs/adr/0021-run-progression-in-core.md` |
| ADR-0026 唯一清关 / 插入时机 | `docs/adr/0026-leave-trap-sole-clear-condition.md` |
| 契约测试护栏 | `docs/code-map/tests.md:49,53,80,135`（`LeaveTrapInsertContractTests` / `LeaveTrapClearContractTests` / `FormalRunSmokeContractTests` / `MapNodeProgressionContractTests`） |
