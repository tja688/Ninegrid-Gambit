# Presentation Code Map（现状）

程序集：`NineGrid.Presentation`  
根目录：`Assets/Scripts/NineGrid.Presentation/`

> 这是 **#28 落地后的真实布局**：单一程序集 + QF Controllers/Commands/Queries/Systems，同时保留历史目录 `Flow/`、`Cards/` 与命名空间 `NineGrid.Flow*` / `NineGrid.Cards*`。  
> **不要**假设已存在顶层 `Architecture/`、`Views/`、`Events/`、`Models/`、`Diagnostics/` 平铺目录——那些是 Spec 目标树，尚未落地。

## 顶层目录（事实）

| 目录 | 约 `.cs` | 命名空间（主） | 放什么 |
|------|----------|----------------|--------|
| `Setup/` | 3 | `NineGrid.Presentation.Setup` | `PresentationSceneRoot`、`PresentationCompositionRoot`、`PresentationSceneBindings` |
| `Platform/` | 1 | `NineGrid.Presentation.Platform` | Windows Player 高回报率鼠标 mitigation（ADR-0006；仅 Standalone Win 非 Editor 生效） |
| `Controllers/` | 18 | `NineGrid.Presentation.Controllers` | QF `PresentationController` 场景入口 |
| `Commands/` | 24 | `NineGrid.Presentation.Commands` | 写意图 |
| `Queries/` | 11 | `NineGrid.Presentation.Queries` | 读裁决（合法性等） |
| `Systems/` | 18 | `NineGrid.Presentation.Systems` | QF System / 窄能力接口 |
| `Flow/` | ~118 | `NineGrid.Flow*` | 导演/时间线/Channel/Scheduler、局内会话、流程壳、诊断、部分 Presenter |
| `Cards/` | ~136 | `NineGrid.Cards*` | 卡视图、场地/手牌/牌库、收敛、特效 SO、静态 Hook |
| `Editor/` | ~22 | `NineGrid.Presentation.Editor` | 编辑器工具；`CardFacePreviewHost` / `VisualEffectPreviewHost`（特效库：左怪物卡参照 + 右精灵表预览；卡组·卡背与卡面页共用 `CardFacePreviewHost`；翻牌预览经 `CardPresentationFlipPreview` 复用 `CardFaceFlipPresenter` 的 Flip.anim 采样） |
| `Tests/` | ~97 | `NineGrid.Presentation.Tests*` | EditMode |

### `Flow/` 子树

| 子目录 | 内容 |
|--------|------|
| `Presentation/` | `PresentationDirector`、`BattleTimeline`、`IPresentChannel` 实现、Intent Script Factory、`BattleBeatScheduler` / `IBattleBeatHandler`（`CardFaceStatHandler`、`PlayerInfoHudBeatHandler`、飘字/FX/金币装饰处理器）、`BattleBeatFlush`、Scheduler 等编排深模块；部分 struct Event |
| `BattleSession/` | 局内会话相关类型 / 接口 |
| `GameFlow/` | 流程壳运行选项等 |
| `Diagnostics/` | Battle/Flow/Perf/Registry Trace Recorder 与 Sink |
| （根下） | `BattleSessionController`、`GameFlowController`、若干 `*ManagerSingleton`（Presenter 壳名）；**指针缝** `WorldPointerUtility`、**命中路由** `PointerHitRouter` / `IPointerHitTarget` / `PointerHitRegistry`（替代 OnMouse*，ADR-0006）；**QuickTest 通道** `QuickTestDeckCatalog` / `QuickTestRunOptions`（主菜单 `\0`–`\9` 技能预设；正式开局不挂怪技能；挂载经 `BattleSessionCheat.TryAttachSkillsToBoardMonsters`：**一怪一技**按格号升序，不够则 Spawn 白板宿主 + 至少一只无技能同伴） |

### `Cards/` 子树

| 子目录 | 内容 |
|--------|------|
| `Ground/` | 场地视图与运动执行 |
| `Battle/` | 交战表现默认资产与策略 |
| `Convergence/` | 场地收敛算法 |
| `Effects/` | 卡面 DOTween / Timeline 特效 SO；默认 Death 为 `CardSpriteSheetBurnExitEffectSO`（脱卡 Burning 帧、落点用视觉世界位含 L3 击退；Staging 尸体才用格锚）；Use 仍为缩小退场 |
| `Slots/` | 卡面槽表 |
| `Presentation/` | 卡面描述合成、`CardFaceFlipPresenter`（FacePivot 采样 Flip.anim Y+Scale；经 `FlipPlaybackCoordinator` 串行；Alpha4 DevKey 全场切换 POC） |
| `Anim/` | `CardSpriteAnimPlayer`、`CardAnimFrameSource`（folder/atlas；Player 走 Resources.LoadAll + 帧名排序，ADR-0008）、`SpriteSheetLoopPlayer`（特效库精灵表循环；Burn 退场复用） |
| （根下） | `FieldBattleView`、手牌/牌库管理器、静态 `*Hook` |

## 装配

- **场景根**：`Setup/PresentationSceneRoot`（`IController` → `NineGridArchitecture.Interface`）
- **组合根**：`Setup/PresentationCompositionRoot.Install(bindings)` —— 唯一生产装配入口
- **绑定表**：`Setup/PresentationSceneBindings`（场景 Host 引用）

主线 busy 真相：`PresentationDirector.IsMainlineBusy`（经 `IPresentationRuntimeSystem` / InputState 只读投影）。`BattleBusy` / `FieldBusy` 不作独立输入门禁；`OccupancyDesyncLatched` 仅为诊断断言。

## Controllers（18）

`PresentationController` · `ExploreInputController` · `AttackInputController` · `PickupInputController` · `UseItemInputController` · `GroundFieldGeometryController` · `FieldBattlePresentationController` · `CardEntityLifecycleController` · `ZoneOwnershipQueryController` · `DescriptionOutputController`（**已退役**：动态 HUD 描述 TMP 不再接线） · `DamageNumberOutputController` · `RelicHudController` · `RoomChoiceInputController` · `RewardChoiceInputController` · `GameFlowShellController` · `TriggerPulseOutputController` · `DiagnosticOutputController` · `BattleSessionPresentationController`

典型路径：场景 Host / Hook → Controller → `IntentIntake.Submit`（所有权 × MainlineBusy）→ Director / Core Command。

## Systems

| 类型 | 用途 |
|------|------|
| `IPresentationRuntimeSystem` / `PresentationRuntimeSystem` | Director / Timeline 生命周期；意图提交 |
| `IBattleSessionSystem` / `BattleSessionSystem` | 局内会话与 Present Channel 绑定 |
| `IGroundFieldGeometrySystem` / `GroundFieldGeometrySystem` | 场地几何锚点（不暴露具体 View 类型） |
| `IFieldBattlePresentationSystem` / `FieldBattlePresentationSystem` | 交战表现锚点 |
| `ICardEntityLifecycleSystem` / `CardEntityLifecycleSystem` | 卡实体生命周期 |
| `IGameFlowShellSystem` / `GameFlowShellSystem` | 流程壳相位权威 |
| `IPresentationInputStateSystem` / `PresentationInputStateSystem` | 输入所有权轴只读投影（`CurrentOwner`）+ MainlineBusy |
| `IIntentIntake` / `IntentIntakeSystem` | 唯一意图收口：两轴门禁 + 合法性 + Director 缓冲；忙时 `IAccelerationSink` |
| `BoardSelectionSystem` | 棋盘选择模式 |
| `ChoicePresentationSystem` | 房间/奖励选择表现 |
| `GroundPresentation` | 场地表现辅助 |

## 静态 Hook（17）——装配缝，不是业务 Sink

`CombatHitSink` 已删除。现存 `*Hook` 是 **Cards/Flow 目录代码 ↔ Presentation Controllers** 的窄接线（避免历史环依赖），由 Controller 在 `RuntimeInitializeOnLoad` / `OnBind` 注册委托；卡面锚点报点桥由组合根注入。

| 位置 | 示例 |
|------|------|
| `Cards/` | `AttackInputHook`、`ExploreInputHook`、`PickupInputHook`、`UseItemInputHook`、`FieldBattlePresentationHook`、`GroundFieldGeometryHook`、`CardEntityLifecycleHook`、`CardZoneOwnershipHook`、输出类 Hook… |
| `Flow/` | `GameFlowShellHook`、`RelicHudHook`、`RoomChoiceCoreHook`、`RewardChoiceCoreHook`、`BattleBeatHook`（排期器报点） |

**禁止**新增业务静态 Sink（跨层读写规则状态）。新交互优先走 Command / Query / Event / System。

## 仍名 `*ManagerSingleton` 的壳（8）

这些是 **Presenter / 管理器壳**，不是旧四大巨型宿主（已改名为 `BattleSessionController` / `GroundFieldView` / `FieldBattleView` / `GameFlowController`）：

- Cards：`CardManagerSingleton`、`CardHandManagerSingleton`、`CardDeckManagerSingleton`
- Flow：`DescriptionManagerSingleton`（**已退役**：不再写 Card InfoText / NoticeText；卡面静态描述权威在 `Basic_Description` Commit）、`DamageNumberManagerSingleton`、`GoldGainFxManagerSingleton`、`RelicManagerSingleton`（#69：图标优先一卡一文件 JSON `sprites.mainIcon`；空缺时回退 `RelicVisualCatalog` bootstrap）、`SelectorManagerSingleton`（卡面主视图锚定为祖先变换无关的局部空间计算，见 [ADR-0015](../adr/0015-card-slot-placement-local-space.md)；BounceFan 不得在 `scale=0` 期间提交卡面，`BuildEntries` 保持 `scale=1`，入场归零只在 `PlayEntryAnimation`）

`DescriptionDisplayHook` 现为 no-op；Hover/Drag/BoardSelect 动态描述 TMP 管道已砍。卡牌运行时持续呈现的描述只走卡面槽 `Basic_Description`（可含 `{param}` 装配实参插值与方括号词条图标）。详情文案由 `CardDetailDescriptionComposer` 合成（概括 + 词条展开）写入 `CardPresentationSnapshot.DetailDescription`。卡面 JSON `faceIntro` 经 Mapper 写入 `CardPresentationSnapshot.FaceIntro`，右键详述面板 `CardInspectOverlayPresenter`（场景 `UI面板/右键描述`）消费：敌方 / 常规两态 BG、**占位锚点隐藏成品 mock 后挂真卡面预制体 Commit**、背景/牌组 TMP；**详细效果信息**由 `CardInspectDetailComposer` 展开效果模板 `design_text`（`[场上]`/`[使用时]` 等分门别类），不重复卡面简要 `description`。半黑屏 `BattleUiDimmerOverlay`（`UI面板/半黑屏BG`）引用计数挡 `PointerHitRouter` 射线、**不**改 `CurrentOwner` / 不暂停主线，局内奖励/属性三选一也 Acquire 同遮罩。稀有度经 `SetFrameColor` 驱动卡框色；卡背优先读卡组 JSON，单卡 `sprites.back*` 可覆写，否则模板兜底（ADR-0009 / #71）。

结构护栏见 `Tests/HostContractStructuralTests`（禁回流四大旧名与 `CombatHitSink`）与 `Tests/IntentIntakeStructuralTests`（禁绕过 IntentIntake、门禁/收口禁壁钟）。

## 读写约定

- **写** → `NineGridArchitecture.Interface.SendCommand(...)`
- **读** → `SendQuery` / `GetSystem<T>()` 只读 API
- **下→上** → struct Event（多数在 `Flow/Presentation/`）或 BindableProperty
- **编排** → `PresentationDirector` / `BattleTimeline` / `IPresentChannel`（普通 C# 深模块，由 System 持有）
- **先手还击** → `AttackIntentScriptFactory` 入队前经 `IPhaseSystem.MonsterStrikesFirst`（或 `MonsterStrikesFirstQuery`）裁决；Present 通道按攻方角色选择（Hit=玩家打怪，Counter=怪打玩家），先手还击只交换入队顺序，不改通道语义
- **九宫格互动计数** → `IPhaseSystem.AdvanceInteractionCount` 与补牌/旋转分步；攻击/探索剧本在补牌前推进计数，用道具路径不调用（ADR-0012 / #75）
- **敌方行动阶段** → `RegisterEnemyActionPhase` / `ResolveNextEnemyAction` / `ResolveEnemyActionFinale` 分拍；同步 `Attack` / `ResolvePostKillBoard` 在玩家侧结算后整段跑完；导演由 `EnemyActionPhaseScheduler` 挂在攻击/探索剧本末尾（每怪一拍；单向打击复用 Counter 通道；`ActionCountdownChanged` → Settled → `UpdateActionCount`）（ADR-0012 / #81）
- **行动倒计时上卡面** → Core `ActionCountdownChanged`（`ResultValue`=剩余）经 `PresentationEventMap` Settled → `CardFaceStatHandler` Commit `ActionCount`；禁止 View 队列外直读 Counters（ADR-0005 / #81）。`Action_Icon` 首版用预制体模板默认图兜底（无五套区分素材）

## 效果扩展点

1. `Commands/` / `Queries/` / Event  
2. `ITimelineStep` / `IPresentChannel`（`Flow/Presentation/`）  
3. `BattleBeatScheduler` / `IBattleBeatHandler`（多处理器唯一分发；`CardFaceStatHandler` 为卡面数值；新事件须在 `PresentationEventMap` 声明 Beat）  
4. 卡面视觉 SO（`Cards/Effects/`）；默认 Death 为 Burning 精灵表退场（`CardSpriteSheetBurnExitEffectSO`，脱卡 FX，落点=视觉世界位）；Use 缩小退场；Lethal 交战不播受击回原段，碎亡留在击退终点
5. 翻牌：`CardFaceFlipPresenter` 采样 Flip.anim；Core `FaceUp` 经 `CardFaceChanged`→`UpdateFaceUp`→`CardFaceFlipBeatHandler` Commit，再经 `FlipPlaybackCoordinator` 全局串行播翻（ADR-0016）；`PresentStep` 通道 Begin 前 `FlushUpdateFaceUp` + 等 Idle，ack 前再等 Idle；配置编辑器 `CardPresentationFlipPreview` 同采样同挂点，禁止再写线性假翻牌
6. 主动翻开：`InputIntentKinds.RevealFace` + `RevealFaceIntentScriptFactory`；邻接背面卡点击分流（AttackInputController）

不要接回静态业务 Sink，也不要在 View 上直接改 Core 规则状态，也不要旁路直读 Core 写卡面数值。

## ADR 不变量（摘要）

- Batch/ack：表演未就位前 Core 不推进下一批（ADR-0001）  
- 逻辑占格唯一归 Core `BoardModel`  
- 卡面可见值经投影 Commit（ADR-0002）；禁止队列外正式 Setter 通路  
- 输入唯一收口 IntentIntake + 两轴门禁（ADR-0004）  
- 卡面**数值**只经结算指令在表演锚点提交，不直读 Core（ADR-0005）；装饰消费者经同一排期器多处理器分发（ADR-0007）  
- Windows Player 高回报率鼠标：`RIDEV_NOLEGACY` + 轮询注入 Input System；命中走 `PointerHitRouter`，禁 `OnMouse*`（ADR-0006）

## 指针与命中（ADR-0006）

- **读口**：`Flow/WorldPointerUtility` —— 屏幕/世界/主键边沿；优先 New Input `Mouse.current`，可 `SetOverrideSource`（测试 / 未来平台）；右键边沿 `WasSecondaryPressedThisFrame`
- **键盘**：`Flow/KeyboardUtility` —— New Input only 下替代 `Input.GetKey*`（DevTest 热键 / UITestBootstrap / Escape Esc 跳过）
- **命中**：`Flow/PointerHitRouter`（`RuntimeInitializeOnLoad` 自举；`-40`）轮询 `PointerHitRegistry`；**手牌按下优先** `CardHandManagerSingleton.TryBeginDragFromHoveredCard`（Hand `-50` 先刷 hover 槽位带）；**右键**开 `CardInspectOverlayPresenter`（再右键或关闭钮关）
- **局内 UI 叠层**：`BattleUiDimmerOverlay` + `UiOverlayHitProxy`（半黑屏吞点 / 关闭钮）；`PresentationInputGates.BattleUiOverlayActive` 只读投影
- **代理**：`GroundCardHitProxy` / `GroundSlotHitProxy` / `HandCardHitProxy` / `BoardSelectParkedCardHitProxy` 实现 `IPointerHitTarget`，**无** `OnMouse*`；命中按目标平面 Z 做 Overlap，避免与手牌深度不一致漏检
- **Win Player mitigation**：`Platform/WindowsHighPollingMouseMitigation`（`#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`）
- **工程设置**：`activeInputHandler = 1`（New Input System only）
- **专项回归**：125 / 1000 / 4000+ Hz × 窗口/无边框/全屏；hover、空槽、点怪、手牌拖放、BoardSelect、BounceFan、右键详述
## Core 表演契约与统一表现管线（#54–#62）

- `NineGrid.Core.PresentationBeat`：`Impact` / `Settled` / `None`（**表演消费归属**，非仅卡面；升级路径注释在枚举旁）
- `PresentationEventMapEntry.Beat` + `NoneReason`：每个 `CoreEventType` 显式锚点归属；`None` 必须写「为何无表演消费」
- 数值类指令绝对值：血甲用既有 `RemainingHp`/`RemainingArmor`；`BaseStatModified` / 生成类事件攻用 `ResultValue`（`Amount` 仍为 StatId，`Delta` 仍为增量）；`CardKilled` 携带 `RemainingHp`；`RewardOffered` Message 携带候选项攻/甲/血（`RewardOfferFaceEncoding`）
- 穷尽性：`NineGrid.Core.Tests.PresentationEventMapBeatExhaustivenessTests`
- **排期器** `Flow/Presentation/BattleBeatScheduler`：批次开启装载非 `None` 指令；`ReportBeat` 交给第一个 `IBattleBeatHandler.TryApply` 成功者；Settled 后未消费只报不改；支持 `PresentStandalone`（非锁步旁路冲刷，恢复当批 pending）
- **处理器** `IBattleBeatHandler`：`CardFaceStatHandler` 只从指令赋值 → `ManagedCard.CommitPresentation`（含 `OfferReward` 按 DefId 匹配 Bounce 负 uid 卡）；`PlayerInfoHudBeatHandler` 对 Avatar 血甲旁路写 HUD（return false 留给卡面认领）；装饰 `DamageFloaterBeatHandler` / `EffectTriggerPulseBeatHandler` / `GoldGainBeatHandler` 分别在 Impact / Settled 消费飘字、FX、金币，不占主线 ack；数值 Commit 后 `CardFacePresentationBinder` 可对配对图标做非阻塞缩放装饰（不占 ack）
- **统一冲刷** `BattleBeatFlush.FlushBeats`（Impact→Settled）：`PresentStep` 就位回执前调用；非锁步（房间/选择/拾取）走 `PresentEventLogSlice`；Bounce spawn 后走 `PresentLatestEventOfType(RewardOffered)`（单条 PresentStandalone）
- **翻牌门控** `FlipPlaybackCoordinator`：Handler 入队串行 `PlayFlipAsync`；`BattleBeatScheduler.FlushUpdateFaceUp` / `BattleBeatFlush.FlushUpdateFaceUp` 供 `PresentStep` 在 `channel.Begin` 前只刷 FaceUp；Idle 门控在 Begin 与 ack 两侧（ADR-0016）
- **生成绝对值** `CardFaceEventValues.WithFaceAbsolutes`：`CardSpawned` / 带 uid 的 `CardDealt` / `AvatarAppeared` 写入造卡/发牌时攻甲血
- **奖励候选项** `RewardEntry` 投影绝对值 + `RewardOffered` Settled；Bounce spawn 后 `PresentLatestEventOfType` 二次提交；禁 `clearCombatStats` 数值旁路
- **开局引导** `Flow/Presentation/CardFaceGenerationBootstrap`：非锁步 Opening 从事件日志重放生成类指令；BoardSelect 视图重 Spawn 用 `ApplyFaceHistoryForUid` 重放该 uid 的生成+后续数值指令（与 Settled 同一 Handler）
- **报点**：攻击/反击命中帧 → `Impact`；用道具 Present 在 Vacate 前报 `Impact`；盘面 Drain 开头再冲刷 `Impact`（探索等）；`PresentStep`：FaceUp 先刷 → 通道 → `FlushBeats` → 等翻牌 Idle → `TryAcknowledge`
- **组合根**：`PresentationCompositionRoot` 注册排期器（`PlayerInfoHudBeatHandler` + `CardFaceStatHandler` + `CardFaceFlipBeatHandler` + 飘字/FX/金币装饰处理器），接线 `FlushUpdateFaceUp`，并订阅 `Evt_PresentationBatchOpened`
- **读写约定补则**：卡面数值只经排期器/生成引导，禁止 Mapper 首次 `TryRead` 写数值；JSON `stats` 仅 Catalog 造卡用；用道具 Present 只 `RefreshVisualsPreservingCommittedStatsOnAllSpawned`；探索/用道具批次投影不写卡面数值；`MarkFieldDead` 只标死亡态不改血量；底盘数值 Setter 非公开；禁 `PresentEffectTriggersFromEventLog` / `SpawnDamagePopups` / `PresentGoldGainsFromEventLog` EventLog 旁路；战中 PlayerInfo 不经 `SyncFromCore`（开局/作弊白名单除外）；Bounce 不得靠 DefId 清战斗数值上数
- **ADR**：[ADR-0005](../adr/0005-card-face-beat-commit.md)、[ADR-0007](../adr/0007-unified-presentation-pipeline.md)（与 0001/0002/0004 交叉引用）；指针/高回报率见 [ADR-0006](../adr/0006-windows-high-polling-mouse-mitigation.md)