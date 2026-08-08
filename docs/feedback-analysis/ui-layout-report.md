# 策划试玩反馈三方比对报告：奖励房宝箱卡与道具卡格布局 / 手牌点击使用

- 比对日期：2026-08-08
- 范围：只探查，零改动。
- 三方来源：① 权威文档（`CONTEXT.md` / `docs/adr/` / `docs/code-map/`）；② 代码实施（`NineGrid.Core` / `NineGrid.Presentation` / `NineGrid.Content`）；③ 策划设计案（`Assets/Docs/九宫格登神/`）。
- 现场取证：Editor（MainScene，Unity 6000.3.10f1）内用 `execute_code` 实测了货架卡渲染包围盒、手牌锚点、Bounce 扇形位置与屏幕坐标换算。

---

## 结论摘要

| 条目 | 判定 | 一句话 |
|------|------|--------|
| 1a. 奖励房宝箱卡与道具卡格（手牌）重叠、拖动难受、应分开更直观 | **表现层布局问题（部分属实）＋ 与设计案布局偏离** | 实测确有几何重叠：**道具奖励房**第 5 张货架卡落格 9，与右下角手牌区（世界 10.5..12.5, -6）屏幕区域相交（1920×1080 下重叠带约 180×262 px）；手牌位置偏离设计案「屏幕正中下方」落到右下角，且手牌 5 格间距 0.5 世界单位、卡面宽 3.32——手牌自身互相压盖约 85%，宝箱卡夹在堆叠中难以一眼识别；而「宝箱卡三选项」（Bounce 扇形，屏幕中央偏右 ~(1066,572)）与手牌实测**不重叠**。 |
| 1b. 无目标道具卡改点击使用（如宝箱卡） | **落地理解偏差：设计案明文「点击使用」，实现为拖拽-only** | 设计案三处原文（`08-UI/玩家交互操作.md:4`、`02-机制/交互机制.md:13`、`03-玩家侧卡牌信息/道具卡.md:4`）均为「点击道具卡格里的道具卡使用」；实现中主键按下手牌卡恒为**起拖**（`PointerHitRouter.cs:115` → `TryBeginDragFromHoveredCard`），必须拖到 ApplyZone 释放才触发使用（`ValidateHandDragApplyAsync`），无点击即用路径；ADR-0032 以「与战斗内一致」把拖拽写成了非战斗手势。反馈实质是让实现回到设计案，且宝箱卡（三张卡 JSON `usableOutsideBattle=false`）与恢复药水/钱袋子等已接线卡都受益。 |

---

## 条目 1a：奖励房货架卡与道具卡格（手牌）的空间关系

### ① 设计案原文

- `08-UI/界面布局.md:10`：**「道具卡格 | 屏幕正中下方 | 存放已拾取道具卡的区域，初始 3 格」**；`:8` 九宫格「屏幕正中上方」；`:11` 技能栏「屏幕右下方」。
- `01-核心概念/房间.md:28-29`：**宝箱奖励房 = 1 张宝箱卡 + 3 张随机道具卡**；道具奖励房 = 2 张属性道具卡 + 3 张随机道具卡。
- `07-流程/层级流程.md:18`：节点 6 清关后放出两个特殊房选项（宝箱奖励房 / 道具奖励房）。
- 设计案**没有**给奖励房货架卡规定屏幕位置，也没有给「宝箱三选项」规定位置——两者都是实现层决策。

### ② 权威决策（ADR / CONTEXT）

- **ADR-0020 / #94**：特殊奖励房 = 真卡货架落格 + 离开图标；货架卡**任意距离点击拿走**；`RewardBoardSlotResolver`（`RewardBoardSlotResolver.cs:9`）`ShelfSlots = {1,2,3,7,9}`、离开格 8、Avatar 硬切格 5。
- **ADR-0024**：落格卡用预制体原生尺寸，不 Fit、不 SetParent 到格位锚点——货架卡以完整卡面尺寸（实测包围盒 3.32 × 4.62 世界单位）直接摆在棋盘格上。
- **ADR-0025**：道具卡格跨节点持续持有、奖励房领取直写；手牌在奖励房会话内**保持在场**（购领流程 `InRoomItemAcquirePresentation` 直接飞入手牌）。
- CONTEXT「道具卡格」「道具卡格满拒入」「非战斗可用」：无布局条款。

### ③ 代码实施（现状事实，含实测）

**货架落格与领取：**
- 宝箱奖励房货架顺序 `[宝箱卡, 道具×3]`（`RewardSystem.cs:436-444`）→ 落格 1/2/3/7；道具奖励房 `[属性×2, 道具×3]`（`:447-456`）→ 落格 1/2/3/7/**9**。
- 货架真卡：`RewardBoardPresenter.SpawnShelves`（`RewardBoardPresenter.cs:306-354`）`SpawnPresentationOnly(..., GroundCardMode)`，原生尺寸对齐格位（ADR-0024）；领取经 `InRoomItemAcquirePresentation.TryAcquireShelfHelpCardToHand`（`:22-117`）→ `PullIntoHandAsync` → `hand.PullFromGroundAsync(skipBusyGuard:true)`（`CardHandManagerSingleton.cs:366-455`）飞入手牌。

**实测几何（MainScene 实取坐标；相机正交 size 8.4375、1920×1080，1 世界单位 ≈ 64 px）：**

| 对象 | 世界中心 | 屏幕中心 | 渲染跨度（包围盒 ±1.66 × ±2.31） |
|------|----------|----------|----------------------------------|
| 手牌格 1（`Anchors/CardHandAnchors/handcard1`） | (10.5, -6) | (1632, 156) | x 1526..1738，y 8..304 |
| 手牌格 5 | (12.5, -6) | (1760, 156) | x 1654..1866（5 格时右缘距屏幕边仅 ~54 px） |
| 货架格 7 | (-5, -5.465) | (640, 188) | x 534..746，y 40..336 |
| 货架格 9（道具奖励房第 5 张） | (5, -5.465) | (1600, 190) | x 1494..1706，y 42..338 |
| 离开图标格 8 | (0, -5.465) | (960, 188) | — |
| Bounce 三选项（宝箱开遗物） | (0.55/1.65/2.75, 0.5) | (995/1066/1136, 572) | x 889..1242，y 452..692 |

- **实测重叠**：道具奖励房格 9 货架卡（x 1494..1706）与手牌格 1（x 1526..1738、y 8..304）在 x 1526..1706 × y 42..304 区域**重叠**（约 180×262 px）——手牌有持卡时，第 5 张货架卡（或其拿到手前）盖住/紧贴手牌第一张，视觉拥挤，且格 9 卡是「真卡大尺寸」落在手牌正上方。
- 宝箱奖励房（4 卡，无格 9）货架与手牌**不重叠**（格 7 距手牌 >500 px）；宝箱卡本身在货架 1 号格（左上角 (640,845)）。
- **手牌自身重度堆叠**：5 格锚点间距仅 **0.5 世界单位**（卡面宽 3.32）——手牌相邻卡互相压盖约 85% 宽；宝箱卡领到手后夹在手牌堆叠里，只露一条边，配合 hover 才抬升露出，正是「宝箱卡和道具卡格重叠起来看起来太难受、拖动起来也难受」的直接观感来源（拖出时先与邻卡叠着，再从右下角长拖过整个战场到 ApplyZone）。
- **手牌位置偏离设计案**：设计案「屏幕正中下方」（世界约 (0, -6.6)），实现为「右下角」(10.5..12.5, -6)。该偏离有客观原因：正中下方即格 8（离开图标 (960,188)）与底排货架正上方，放正中会直接压住棋盘底排（这也解释了实现为何选右下）；代价是与右下角奖励房货架（格 9）、回收区（(11.47,-0.03)）、简要解释文字框（(11.47,0)）、卡组区（(11.06..11.25,0)）挤在同一右带。
- **宝箱卡三选项（Bounce 扇形）与手牌不重叠**：`Directors/SelectorManager` 世界 (1.25,0)，容器偏移 (0.4,0.5)、间距 1.1（`MainScene.unity` 序列化字段；`BounceFanChoicePresenter.cs:27-36`）→ 三选项中心 y=0.5、底边 y≈-1.8，手牌顶边 y≈-3.7，间隙约 1.9 世界单位（≈120 px）。扇形覆盖的是**战场中部偏右**（格 4/5/6 一带，UI 排序层盖在棋盘上）——「三个选项看不到/被挡」若不成立，观感拥挤的来源更可能是「扇形 + 手牌 + 战场」三块同时在场、且扇形出现点与玩家松手点（ApplyZone 内任意位置）无关（`ValidateHandDragApplyAsync` 放行后扇形固定开在中央，不跟落点）。

### 1a 判定

**表现层布局问题（部分属实，实测可复现）＋ 与设计案布局偏离（非 ADR 约束）。**

1. 「道具奖励房格 9 货架卡 × 手牌」重叠**实测成立**；宝箱奖励房货架与手牌不重叠。
2. 「宝箱卡（三选项）与道具卡格重叠」在现场景**不成立**（实测间隔 >120 px）；更贴合事实的是：手牌自身 85% 压盖堆叠 + 手牌被挤到右下与货架/回收/解释/卡组共挤一条右带，整体观感拥挤；「一眼看到三个选项」诉求对应的是扇形位置/大小/遮挡的观感问题（覆盖战场中部，且不跟落点）。
3. 改动面：均为表现层（场景锚点 / `RewardBoardSlotResolver.ShelfSlots` / 扇形宿主位置或落点跟随），ADR 未锁死任何一处的具体坐标；但「手牌放回正中下方」会压住格 8 离开图标与底排货架，需与货架格位方案一并重排（棋盘底排、手牌、简要解释、回收区是同一块屏幕空间的四家）。

---

## 条目 1b：无目标道具卡点击使用（而非拖拽）

### ① 设计案原文（三处一致）

- `08-UI/玩家交互操作.md:4`：「点击[[道具卡格]]里的[[道具卡]]使用，[[道具卡]]不受玩家互动距离影响，可以对整个[[九宫格]]使用」。
- `02-机制/交互机制.md:13`：「点击道具牌格中的帮助卡使用其效果，**不受正交相邻距离限制**，可瞄准九宫格任意怪物」。
- `03-玩家侧卡牌信息/道具卡.md:3-4`：「点击九宫格上的道具卡后，将其放入道具卡格；点击道具卡格里的道具卡使用」。
- 宝箱卡本体（`:73-86`）：三张宝箱卡均为「[使用时] 从三个遗物中选一个获得」——**无棋盘目标**。

### ② 权威决策（ADR / CONTEXT）

- **ADR-0032（非战斗可用）**：「手势：与战斗内一致——把手牌（道具卡格）拖到棋盘空位，经既有 ApplyZone → IntentIntake → Director → Core 链路」——**ADR 把「拖拽」当作既成手势写进非战斗路径**；且非战斗只放行免目标类（None），首批恢复药水 / 生日蛋糕 / 钱袋子。
- **ADR-0004（两轴门禁）**：意图必须经 IntentIntake 收口；点击路径若新增，仍须走同一收口（`SubmitUseItemIntentCommand` 现成）。
- **CONTEXT「非战斗可用」「九宫格互动」**：道具使用不是九宫格互动；无「必须拖拽」的权威表述。

### ③ 代码实施（现状事实）

**点击 = 起拖，无「点击即用」路径：**
- 主键按下手牌卡：`PointerHitRouter.cs:113-118` 优先 `hand.TryBeginDragFromHoveredCard()`（`CardHandManagerSingleton.cs:499-512`）→ 进入拖拽循环（`RunDragLoopAsync` `:1221-1296`）——**任何一次主键点击都是拖拽的开始**，没有按下即判断「无目标卡 → 直接用」的分支。
- 右键手牌卡：仅开详述（`PointerHitRouter.cs:151-180` `TryOpenCardInspect`；`CardInspectOverlayPresenter`）。
- 使用判定发生在**拖拽释放**后：`CompleteDragApplyInternalAsync`（`CardHandManagerSingleton.cs:1298-1347`）→ 释放点在 ApplyZone（15×15 Collider，场景 `HandcardApplyZone` @ (0,0)）→ `ValidateHandDragApplyAsync`（`BattleSessionExecutor.BoardSelect.cs:16-90`）：
  - `HelpCardBoardSelectResolver.TryGetPlayKind`（`HelpCardBoardSelectResolver.cs:55-74`）：无 SelectedCards → `PlayKind.None` → **直接** `UseItemInputHook.TrySubmitUseItem`（`:78-87`）；单目标 → 按释放点格位解析目标；多选（交换卡等）→ 进入 `BoardCardSelectModeController` 棋盘选目标。
- 宝箱卡：`help_common_chest_card.json:97` `usableOutsideBattle=false`（战斗限定）；效果 `tpl.shared.3.help_blue_chest_card_use`（`OfferRewardChoice` poolId `relic.common_chest`）无 SelectedCards → `PlayKind.None` → 战斗内拖拽释放即触发 → `OfferRewardChoice` → `SelectorManagerSingleton.BeginBounceChoice` 扇形三选一（`BattleSessionExecutor.Choice.cs:24-301`）。
- 非战斗相位（ADR-0032）：`ValidateHandDragApplyAsync:36-54` 卡级裁决——`usableOutsideBattle=true` 且 `PlayKind.None` 才放行；**同样必须拖**（奖励房/选房内从右下手牌长拖到棋盘）。

### 1b 判定

**落地理解偏差：设计案明文「点击使用」，实现为拖拽-only，ADR-0032 以「与战斗内一致」沿用拖拽手势。**

1. 设计案三处原文（玩家交互操作.md / 交互机制.md / 道具卡.md）统一为「点击道具卡格里的道具卡使用」，且「可瞄准九宫格任意怪物」——目标选择是点击后的**下一步**（现有 `BoardSelect` 多选 / 单目标解析机制可复用）。
2. 实现从早期起就是「拖到 ApplyZone 释放」，点击恒为起拖；ADR-0032（相对较新）把拖拽写成了非战斗手势前提，与设计案存在未决张力——试玩反馈实质上站回设计案一侧。
3. 落地改动涉及面（评估，非结论）：
   - 表现层：`PointerHitRouter`/`CardHandManagerSingleton` 增加「按下即判定 PlayKind.None 直接提交（或进入目标选择）」的点击分支，须与拖拽判定区分（防误触可参考驻留/阈值）；提交仍走 `UseItemInputHook.TrySubmitUseItem`（IntentIntake 收口，ADR-0004 合规）。
   - Core：`PhaseSystem` 门禁版 `UseItem` 与 `BoardIntentLegality`（`ValidateHandDragApplyAsync` 三处裁决）已有卡级逻辑，点击路径复用即可，无 Core 改动。
   - 对 `PlayKind.None` 卡（宝箱、恢复药水、钱袋子、旋转轮、暴力卡、爆弹等）「点击即用」；对单目标/多选卡（飞刀、交换卡等）点击后进目标选择或维持拖拽。
   - ADR-0032「手势」条款需随策划确认修订或增补新 ADR；相关护栏（`ItemSlotsRecycleContractTests` 等非战斗相位测试）语义不变。
4. 与 1a 联动：点击使用会显著缓解「右下手牌长拖过整个战场」的拖动难受问题（这也是反馈把两条写在一起的逻辑）。

---

## 附：关键证据文件:行索引

| 事实 | 位置 |
|------|------|
| 设计案「点击道具卡格里的道具卡使用」 | `Assets/Docs/九宫格登神/08-UI/玩家交互操作.md:4`；`02-机制/交互机制.md:13`；`03-玩家侧卡牌信息/道具卡.md:3-4` |
| 设计案「道具卡格 屏幕正中下方」 | `08-UI/界面布局.md:10`（九宫格 `:8`、技能栏 `:11`） |
| 宝箱卡「从三个遗物中选一个获得」（无目标） | `03-玩家侧卡牌信息/道具卡.md:73-86` |
| 宝箱/道具奖励房组成 | `01-核心概念/房间.md:28-29`；`RewardSystem.cs:436-456` |
| 奖励房货架落格 1/2/3/7/9、离开 8、Avatar 5 | `NineGrid.Presentation/Flow/RewardBoard/RewardBoardSlotResolver.cs:9-13`（`RewardBoardPresenter.cs:306-354` 真卡落格） |
| 商店货架同样占用底排（1,3,7,9,4） | `NineGrid.Presentation/Flow/ShopBoard/ShopBoardSlotResolver.cs:8` |
| 领取直写 → 飞入手牌 | `Flow/InRoomBoard/InRoomItemAcquirePresentation.cs:22-117,158-184`；`Cards/CardHandManagerSingleton.cs:366-455` |
| 主键按下手牌 = 起拖（无点击即用） | `Flow/PointerHitRouter.cs:113-118`；`Cards/CardHandManagerSingleton.cs:499-512,1221-1347` |
| 拖放使用裁决（PlayKind.None 直接 UseItem） | `Flow/BattleSession/BattleSessionExecutor.BoardSelect.cs:16-90`；`Flow/HelpCardBoardSelectResolver.cs:55-74` |
| 宝箱卡战斗限定 + 无 SelectedCards | `Assets/StreamingAssets/ContentVisual/cards/help_common_chest_card.json:79-97` |
| 宝箱开遗物 Bounce 扇形（仅此用途） | `Flow/BounceFanChoicePresenter.cs:20-36,191-225`；`SelectorManagerSingleton.cs:43-98`；`RoomChoiceRetirementStructuralTests.cs:97-101` |
| 扇形宿主/容器实测位置（世界 (1.25,0) + 偏移 (0.4,0.5)） | `MainScene.unity`（`Directors/SelectorManager`）；本报告 ③ 实测表 |
| 手牌锚点/ApplyZone/回收区实测位置 | `MainScene.unity`（`Anchors/CardHandAnchors` handcard1..5 @ (10.5..12.5,-6)；`HandcardApplyZone` @ (0,0) 15×15；`HandcardRecycleZone` @ (11.47,-0.03)）；CardHandLayoutSettings.cs（间距/命中盒） |
| 货架卡渲染包围盒 3.32×4.62（实测量得 extents 1.66×2.31） | `Assets/Prefabs/老Standard Card.prefab` + Editor 实测 |
| ADR-0032 拖拽手势条款 | `docs/adr/0032-item-cards-usable-outside-battle.md:12` |
| 特殊奖励房表现契约（presentation 地图） | `docs/code-map/presentation.md:231-237` |
| 手牌跨节点持续持有 / 满拒入 | `docs/adr/0025-item-slots-run-persistent-hold.md`；CONTEXT「道具卡格」 |
