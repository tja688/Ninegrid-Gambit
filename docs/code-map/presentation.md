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
| `Controllers/` | ~21 | `NineGrid.Presentation.Controllers` | QF `PresentationController` 场景入口 |
| `Commands/` | 25 | `NineGrid.Presentation.Commands` | 写意图 |
| `Queries/` | 11 | `NineGrid.Presentation.Queries` | 读裁决（合法性等） |
| `Systems/` | 18 | `NineGrid.Presentation.Systems` | QF System / 窄能力接口 |
| `Flow/` | ~118 | `NineGrid.Flow*` | 导演/时间线/Channel/Scheduler、局内会话、流程壳、诊断、部分 Presenter |
| `Cards/` | ~136 | `NineGrid.Cards*` | 卡视图、场地/手牌/牌库、收敛、特效 SO、静态 Hook |
| `Editor/` | ~22 | `NineGrid.Presentation.Editor` | 编辑器工具；`CardFacePreviewHost` / `VisualEffectPreviewHost`（特效库：左怪物卡参照 + 右精灵表预览；卡组·卡背与卡面页共用 `CardFacePreviewHost`；翻牌预览经 `CardPresentationFlipPreview` 复用 `CardFaceFlipPresenter` 的 Flip.anim 采样；**房间图标** `Room` 预览实例化图标预制体，**房间选项** `ChoiceOption` 挂 `房间选项标准模板`） |
| `Cheat/` | 4 | `NineGrid.Presentation.Cheat` | **F12 作弊工具面板**（仅 `UNITY_EDITOR / DEVELOPMENT_BUILD`，正式包不含）：接线 MainScene `UI面板/作弊工具BG`（默认失活；SpriteRenderer 世界 UI）。`CheatToolHotkeyHost`（常驻 + F12）、`CheatToolPanelController`（优先按名找场景预置并接线，缺结构才最小兜底）、`CheatToolPanelButton`（`BoxCollider2D` + `PointerHitRouter`，一级五按钮）、`CheatToolCardSearchIndex`（怪物/机关/道具三类、排除归档卡组；卡名/卡组名优先表现层 JSON `displayName`）。功能：一键清关（对齐 QuickTest `-` → `TryForceNodeVictory`）、战斗加卡（二级 WorldSpace Canvas 搜索；运行时补 `GraphicRaycaster`；仅战斗阶段；`ShuffleIntoDrawPileAction` 顶插入 + `PresentEventLogSlice`）、金币 +999、回复满血。二级关闭清空输入；`PointerHitRouter` 在 EventSystem UI 上时让渡点击 |
| `Tests/` | ~97 | `NineGrid.Presentation.Tests*` | EditMode |

### `Flow/` 子树

| 子目录 | 内容 |
|--------|------|
| `Presentation/` | `PresentationDirector`、`BattleTimeline`、`IPresentChannel` 实现、Intent Script Factory、`BattleBeatScheduler` / `IBattleBeatHandler`（`CardFaceStatHandler`、`PlayerInfoHudBeatHandler`、飘字/FX/金币装饰处理器）、`BattleBeatFlush`、Scheduler 等编排深模块；部分 struct Event |
| `BattleSession/` | 局内会话相关类型 / 接口 |
| `GameFlow/` | 流程壳运行选项等 |
| `RoomIcons/` | 场地图标 Spawn / 占格登记 / 驻留提交 / 进房硬切（#88）；进房经 `RunSceneTransitionService`：同层 Directional、跨层 Round |
| `Transitions/` | 局内节点过场：`RunSceneTransitionService`（Feel `MMFaderRound`/`MMFaderDirectional`）、`DirectionalBiasPicker`、`RunSceneTransitionSettingsSO`（Resources `Transitions/RunSceneTransition`）；表现层配置器「过场」foldout |
| `ShopBoard/` | 商店货架 + 刷新 + 离开（#92）：任意距离点击购买、驻留离开 |
| `TavernBoard/` | 卡店三项服务 + 刷新 + 离开（#93）：就地选项；「道具卡固定」二级选择铺空格候选 |
| `RewardBoard/` | 特殊奖励房真卡 + 离开（#94）：任意距离点击拿走、踩离开放弃 |
| `AttributeBoard/` | 属性房三选二候选真卡 + 离开（#137）：任意距离点击选择两张、踩离开放弃 |
| `InRoomBoard/` | 房内共用：`InRoomGoldPresentation`（非战斗扣金→HUD） |
| `BoardBriefTip/` | 简要解释文字框 + 楼层提示（#89）：文案纯逻辑、悬停命中代理、胜负 Notice 出口 |
| `Diagnostics/` | Battle/Flow/Perf/Registry Trace Recorder 与 Sink |
| （根下） | `BattleSessionController`、`GameFlowController`、若干 `*ManagerSingleton`（Presenter 壳名）；**指针缝** `WorldPointerUtility`、**命中路由** `PointerHitRouter` / `IPointerHitTarget` / `PointerHitRegistry`（替代 OnMouse*，ADR-0006）；**开局选项** `GameFlowRunOptions`（`CreateFormal` 正式无作弊 / `CreateQuickTest` QuickTest；`QuickTestMode` 由载荷推导，`TestMode` 布尔已删，#125）；**QuickTest 通道** `QuickTestDeckCatalog` / `QuickTestRunOptions`（主菜单 `\0`=流程测试 Sequential 空技能；`\1`–`\9`：`skillIds` 一怪一技挂载 + **`trapContentIds` 经 `AddEnemyCard` 注入敌池**；正式开局不挂怪技能/不注机关；挂载经 `BattleSessionCheat.TryAttachSkillsToBoardMonsters`：**一怪一技**按格号升序，不够则 Spawn 白板宿主 + 至少一只无技能同伴）。**批1** `CardKind.Trap` / 双桶 / 五套卡面（ADR-0017）；**批2** 滚石/捕熊/烈焰迁 Trap + QT `\1`/`\6`/`\8`/`\9` 机关注入；**批3** 倒刺/三图腾/治疗泉 + QT `\1`–`\9` 九机关齐全（`help.healing_spring` 已删）；**#111** 离开机关 `trap.leave` + `tpl.trap.door`/`tpl.trap.magic_immunity`/`tpl.trap.leave`（可注入）；**#112** 默认 ⌈N/2⌉ 真怪击破后洗入；层主房改击破开局层主（`DeckSystem` OnKill → `ShuffleIntoDrawPile`，经补牌上场）；**#113** `IsNodeCleared`=`IsLeaveTrapBroken`（真怪清零不清关；击破离开机关清关；清场不兑金；QT 跳关置标志；**清关后禁止 PostKill Fill 补牌**） |

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

### MainScene 装配卫生（#141）

- 生产装配入口唯一：MainScene 恰好一个 `PresentationSceneRoot`；`PresentationSceneBindings` 只持存活 Host（`DescriptionManagerSingleton` 已删）。
- 流程壳 View 由 `GameFlowController.Awake` 自绑定 `GameFlowShellSystem`；`PresentationSceneRoot` 不重复 Bind（单入口）。
- `BattleSessionController` 依赖一律经 `BindSceneHosts` 由 SceneRoot 注入；源码禁 `FindObjectOfType` / `FindDeep` / `GameObject.Find` / `Resources.Find`。
- `CardManagerSingleton.cardRoot` 显式绑定场景 `Actors/Cards` 节点（#141 起不再运行时自造）。
- `UiPanelRouter` 无 `inGameInfoText`（目标对象 `TableNine Text Overlay UI/InGameInfoText` 不存在、唯一写入方为 no-op，#141 删除失效查找路径）；玩家数值由 `PlayerInfoHudPresenter` 持续持有，不随面板切换 SetActive。
- `GameFlowController` 无 `noticeText`（旧 NoticeText 通道退役，ADR-0020）；胜负/房间 stub Notice 一律走简要解释文字框。
- MainScene 无 `LivingUiContentMarker`（无 `LivingUiDirector` 的静态主菜单不承载 LivingUI 构型；UITestSence 保留其完整 LivingUI 舞台）。
- `PresentationOutputProjector` 无 `UpdateAvatarDebugText`（no-op 通道 + 隐式 Find 已删；Avatar 血甲 HUD 由 `PlayerInfoHudBeatHandler` 在 Impact 用指令刷新）。
- 护栏：`Tests/MainSceneHygieneStructuralTests`（类型/场景 YAML/源码模式断言，防上述残留回流）。

## Controllers（20）

`PresentationController` · `ExploreInputController` · `BoardWalkInputController` · `AttackInputController` · `PickupInputController` · `UseItemInputController` · `RecycleItemInputController` · `GroundFieldGeometryController` · `FieldBattlePresentationController` · `CardEntityLifecycleController` · `ZoneOwnershipQueryController` · `DescriptionOutputController`（**已退役**：动态 HUD 描述 TMP 不再接线） · `DamageNumberOutputController` · `RelicHudController` · `RoomChoiceInputController` · `RewardChoiceInputController` · `GameFlowShellController` · `TriggerPulseOutputController` · `DiagnosticOutputController` · `BattleSessionPresentationController`

典型路径：场景 Host / Hook → Controller → `IntentIntake.Submit`（所有权 × MainlineBusy）→ Director / Core Command。

### 道具卡格回收（#110 / ADR-0025）

- **Core**：`RecycleItemSlot` / `ApplyRecycleItemSlot` ⇒ 移除 ItemSlots 卡 + `RecycleItemSlotGold`（默认 10）
- **意图**：`InputIntentKinds.RecycleItem` 为 board action；主线 busy → `BufferToDirector`（不拒收）
- **装配**：`RecycleItemIntentScriptFactory`（`ApplyRecycleItemSlotCommand`）经 `PresentationCompositionRoot` 路由
- **拖放**：`CardHandManagerSingleton` 拖起激活 `CardRecycleNotice`（半透明黑底 + 图标 + `标准世界文字 (2)` 价值 TMP）+ `HandcardRecycleZone`；落入回收区优先于 ApplyZone；提交 `SubmitRecycleItemIntentCommand`
- **叠层**：回收 UI 激活期间 `CardDeckManagerSingleton.SetRecycleBackgroundSuppressed(true)` 把卡组卡临时切到 Sorting Layer `BG`，Notice 留在 `Main` 压住卡组；手牌拖拽 / 遗物 ghost 仍在 `Main` 更高 order，压住 Notice。`CardDeckSlotContainer.ApplySortingOrder` **每次**入槽都 `PropagateSortingLayerFromGroup`（不只在 layer 名变化时）；`CardManagerSingleton.ApplyDisplayMode` 对已入槽 `CardDeckMode` 委托 `EnsureDeckSorting`（对齐手牌 `EnsureHandSorting`），避免 `RefreshDisplayMode` 把序打回默认 -30 / 子节点逃出 BG
- **价值预览**：指针悬停回收区时 TMP 显示 `+RecycleItemSlotGold` / 遗物拖时 `+DiscardRelicGold`；拖出或拖结束隐藏
- **退场**：回收成功后走 `PlayDeathAsync` 碎裂（`ShatterCardAfterRecycleAsync`），与使用道具的 `PlayUseAsync`/缩小退场分开

### 开局手牌（ADR-0025 持续持有）

- **持续持有**：`CaptureOpeningHandDeals` 把非本关授予的 ItemSlots 归入 `HandRestores`；`StartBattleNode` 在 `ResetCardPresentationSurface` + `CaptureOpeningPresentationPlan` 之后立刻 `ApplyOpeningHandRestores`（`TryPlaceInHandImmediate`），须早于 Avatar/环发牌，避免空窗闪烁
- **本关授予**：仅 `relic.*` / `skill.*` 的 `CardSpawned` 走 `HandDeals` → `DealCardToHandAsync` 从源锚点飞入（仍在 Opening 末尾）
- **满格**：Core `SpawnCard` / `GrantHelpCardToPlayerSideDeck` 写满即止、多余静默丢弃、不兑金；表现层不会收到被丢弃卡的 spawn
- **非战斗打出**：`RoomChoice` / `RewardItemChoice` 相位对 `usableOutsideBattle=true` 的道具卡放行 `UseItem`（拖到棋盘空位，走既有 ApplyZone 链路；Core `PhaseSystem` 门禁版 `UseItem` + `BoardIntentLegality` + `ValidateHandDragApplyAsync` 三处均按卡级裁决）；`RoomEvent` 与未标注卡维持禁止（拖放回手，v1 无提示）。非战斗使用走**非锁步冲刷**（`BattleBeatFlush.PresentEventLogSlice`）驱动 HUD，不复用战斗锁步 Batch-ack（ADR-0032）

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
| `IAvatarWalkSystem` / `AvatarWalkSystem` | 非战斗跳格门禁 + `AvatarWalkRunner`（BFS 连跳 / 改目标） |
| `BoardSelectionSystem` | 棋盘选择模式 |
| `ChoicePresentationSystem` | 房间/奖励选择表现 |
| `GroundPresentation` | 场地表现辅助 |

## 静态 Hook（18）——装配缝，不是业务 Sink

`CombatHitSink` 已删除。现存 `*Hook` 是 **Cards/Flow 目录代码 ↔ Presentation Controllers** 的窄接线（避免历史环依赖），由 Controller 在 `RuntimeInitializeOnLoad` / `OnBind` 注册委托；卡面锚点报点桥由组合根注入。

| 位置 | 示例 |
|------|------|
| `Cards/` | `AttackInputHook`、`ExploreInputHook`、`BoardWalkInputHook`、`PickupInputHook`、`UseItemInputHook`、`RecycleItemInputHook`、`FieldBattlePresentationHook`、`GroundFieldGeometryHook`、`CardEntityLifecycleHook`、`CardZoneOwnershipHook`、输出类 Hook… |
| `Flow/` | `GameFlowShellHook`、`RelicHudHook`、`RoomChoiceCoreHook`、`RewardChoiceCoreHook`、`BattleBeatHook`（排期器报点） |

**禁止**新增业务静态 Sink（跨层读写规则状态）。新交互优先走 Command / Query / Event / System。

## 仍名 `*ManagerSingleton` 的壳（8）

这些是 **Presenter / 管理器壳**，不是旧四大巨型宿主（已改名为 `BattleSessionController` / `GroundFieldView` / `FieldBattleView` / `GameFlowController`）：

- Cards：`CardManagerSingleton`（底盘 + Kind→卡面五套：`Avatar`/`Monster`/`HelpCard|Item|PlayerCard`/`Relic`/`Trap`，路径见 `CardChassisPaths`；ADR-0002 / ADR-0017；**运行时 Spawn 不含** `Room` / `ChoiceOption`）、`CardHandManagerSingleton`、`CardDeckManagerSingleton`（局内 NewCard 洗入如离开机关经 `CardDeckAddAnchors`；遗物/技能开局赠牌才走 `AddCardAtFromOrigin` 锚点直飞）
- 编辑器扩展壳（非运行时五套）：`CardChassisPaths.RoomOptionFacePrefab`（`房间选项标准模板`）+ `ResolveRoomIconPrefab`（`Assets/Prefabs/地形图标/*图标.prefab`）；卡牌表现编辑器侧栏「卡面」下另分 **房间图标** / **房间选项**
- **怪物遭遇正式配置（#134 起）**：七套正式主题卡组（`ThemeDeckStableMapping`，deckId 为历史不透明主键）在遭遇表 `monster_decks.json` 已切 `Unknown` 正式可选；过渡卡组 `deck.transition` 与池外流浪卡（`deck.wandering_legion` 等）已**归档**——表行 `Reserve` + 成员 `isReserve`，保留 JSON 但不再被 RewardSystem 序列抽卡选中（仍被效果模板 `ShuffleInto` 按 defId 直生，见 ADR-0029）。卡 JSON 保留 `sequence`（1–5）与 `designSlotName`（近战1/远程2…，仅表现、不进 Core）；编辑器可改所属卡组 / 槽位名 / 序列 / 等级。菜单 `NineGrid/Content/校验怪物遭遇配置（过渡期|交付就绪）` 一键校验；交付就绪为正向门禁（ADR-0029）
- Flow：`DamageNumberManagerSingleton`、`GoldGainFxManagerSingleton`、`RelicManagerSingleton`（#69：图标优先一卡一文件 JSON `sprites.mainIcon`；空缺时回退 `RelicVisualCatalog` bootstrap；**#98 / ADR-0027**：装备栏上限 12；**左键拖**遗物图标（抬高排序 + 半透明，槽图标隐藏不改布局）共用道具卡格回收区 → `DiscardRelic` +20 金；**右键** `CardInspectOverlayPresenter.TryOpenByDefId` 详述；满栏 Bounce 拒收「遗物格子已满」且拖弃可穿透半黑屏；宝箱跳过走 `SkipRelicChoiceGold`）、`SelectorManagerSingleton`（卡面主视图锚定为祖先变换无关的局部空间计算，见 [ADR-0015](../adr/0015-card-slot-placement-local-space.md)；BounceFan 不得在 `scale=0` 期间提交卡面，`BuildEntries` 保持 `scale=1`，入场归零只在 `PlayEntryAnimation`；选择命中为容器本地固定 AABB，ChoiceOverlay 下合法悬停可开右键详述且详述打开期间屏蔽点选）

`DescriptionManagerSingleton`（**已删 #141**：动态 HUD 描述 TMP 退役后不再留场景/绑定表序列化兼容，`PresentationSceneRoot` / `PresentationSceneBindings` 同步移除；禁复活）。

`DescriptionDisplayHook` 现为 no-op；Hover/Drag/BoardSelect 动态描述 TMP 管道已砍。**简要解释**（非战斗悬停一句话 + 胜负 Notice）走干净通路 `Flow/BoardBriefTip/` → 场景 `Panels/简要解释文字框`（ADR-0020 / #89），**不得**复活 `DescriptionManagerSingleton`。卡牌运行时持续呈现的描述只走卡面槽 `Basic_Description`（可含 `{param}` 装配实参插值与方括号词条图标；插值语法：`{value}` 简单式取首个含键装配、`{装配id.value}` / `{模板id.value}` 精确限定、`{卡defId.value}` 前缀限定——命中装配取值一致才填，异值歧义保留字面量，见 `CardFaceDescriptionParamFiller`）。详情文案由 `CardDetailDescriptionComposer` 合成（概括 + 词条展开）写入 `CardPresentationSnapshot.DetailDescription`。卡面 JSON `faceIntro` 经 Mapper 写入 `CardPresentationSnapshot.FaceIntro`，右键详述面板 `CardInspectOverlayPresenter`（场景 `UI面板/右键描述`）消费：敌方 / 常规两态 BG、**占位锚点隐藏成品 mock 后挂真卡面预制体 Commit**、背景/牌组 TMP；**详细效果信息**由 `CardInspectDetailComposer` 展开效果模板 `design_text`（`[场上]`/`[使用时]` 等分门别类），不重复卡面简要 `description`。**技能列表以局内 `IEffectSystem` 已激活挂载为准**（`EffectOwner.SourceDefId`，含 QuickTest 动态注入与未来运行时加技），按技能装配 `argsJson` 填 `design_text` 数值；无 live 挂载时才回退卡 JSON `skillIds` / `effectAssemblies`。半黑屏 `BattleUiDimmerOverlay`（`UI面板/半黑屏BG`）引用计数挡 `PointerHitRouter` 射线、**不**改 `CurrentOwner` / 不暂停主线，局内奖励 Bounce 也 Acquire 同遮罩。稀有度经 `SetFrameColor` 驱动卡框色；卡背优先读卡组 JSON，单卡 `sprites.back*` 可覆写，否则模板兜底（ADR-0009 / #71）。

结构护栏见 `Tests/HostContractStructuralTests`（禁回流四大旧名与 `CombatHitSink`）与 `Tests/IntentIntakeStructuralTests`（禁绕过 IntentIntake、门禁/收口禁壁钟）。

## 读写约定

- **写** → `NineGridArchitecture.Interface.SendCommand(...)`
- **读** → `SendQuery` / `GetSystem<T>()` 只读 API
- **下→上** → struct Event（多数在 `Flow/Presentation/`）或 BindableProperty
- **编排** → `PresentationDirector` / `BattleTimeline` / `IPresentChannel`（普通 C# 深模块，由 System 持有）
- **先手还击** → `AttackIntentScriptFactory` 入队前经 `IPhaseSystem.MonsterStrikesFirst`（或 `MonsterStrikesFirstQuery`）裁决；Present 通道按攻方角色选择（Hit=玩家打怪，Counter=怪打玩家），先手还击只交换入队顺序，不改通道语义。**反击批入队前另查 `RuleId.CounterAttackBanned`**（远程武器：不先手也不反击，反击动作整批跳过；齐射走 `EnemyActionPhaseScheduler` 不受影响）
- **命中批盘面 delta** → `FieldBattlePresentationExecutor` 在 lunge 后、Vacate 前必须 `Drain` 同批 OnBattle 步骤（如逃避 `Swap`）；主目标尸体 Remove 经 `BoardPresentationMerge.ForHitPresentDrain` 剥离，仍走 Vacate/FinalizeLethal。Fill/Rotate 仍属击杀后剧本。漏 Drain 会导致 Core/表现占格分叉（`OccupancyDesyncLatched`）
- **九宫格互动计数** → `IPhaseSystem.AdvanceInteractionCount` 与补牌/旋转分步；攻击/探索剧本在补牌前推进计数，用道具路径不调用（ADR-0012 / #75）
- **敌方行动阶段** → `RegisterEnemyActionPhase` / `ResolveNextEnemyAction` / `ResolveEnemyActionFinale` 分拍；同步 `Attack` / `ResolvePostKillBoard` 在玩家侧结算后整段跑完；导演由 `EnemyActionPhaseScheduler` 挂在攻击/探索剧本末尾（每怪一拍；单向打击复用 Counter 通道；`ActionCountdownChanged` → Settled → `UpdateActionCount`）（ADR-0012 / #81）
- **行动倒计时上卡面** → Core `ActionCountdownChanged`（`ResultValue`=剩余）经 `PresentationEventMap` Settled → `CardFaceStatHandler` Commit `ActionCount`；禁止 View 队列外直读 Counters（ADR-0005 / #81）。`Action_Icon` 首版用预制体模板默认图兜底（无五套区分素材）

## 效果扩展点

1. `Commands/` / `Queries/` / Event  
2. `ITimelineStep` / `IPresentChannel`（`Flow/Presentation/`）  
3. `BattleBeatScheduler` / `IBattleBeatHandler`（多处理器唯一分发；`CardFaceStatHandler` 为卡面数值；新事件须在 `PresentationEventMap` 声明 Beat）  
4. 卡面视觉 SO（`Cards/Effects/`）；默认 Death 为 Burning 精灵表退场（`CardSpriteSheetBurnExitEffectSO`，脱卡 FX，落点=视觉世界位）；Use 缩小退场；Lethal 交战不播受击回原段，碎亡留在击退终点
5. 翻牌：`CardFaceFlipPresenter` 采样 Flip.anim；Core `FaceUp` 经 `CardFaceChanged`→`UpdateFaceUp`→`CardFaceFlipBeatHandler` Commit，再经 `FlipPlaybackCoordinator` 全局串行播翻（ADR-0016）；`PresentStep` 默认通道 Begin 前 `FlushUpdateFaceUp` + 等 Idle，ack 前再等 Idle；**攻击 / 反击 Present** 设 `flushFaceUpBeforeBegin: false`，当批 FaceUp 留到通道后 `FlushBeats`（命中后再翻）；配置编辑器 `CardPresentationFlipPreview` 同采样同挂点，禁止再写线性假翻牌
6. 主动翻开：`InputIntentKinds.RevealFace` + `RevealFaceIntentScriptFactory`；邻接背面卡点击分流（AttackInputController）

不要接回静态业务 Sink，也不要在 View 上直接改 Core 规则状态，也不要旁路直读 Core 写卡面数值。

## 房间表现三分法（场地图标选房已落地 · ADR-0020）

局内「选下一房 / 进房选项 / 商店货架」表现资产分三类，**权威配置**在卡牌表现 JSON + 表现层编辑器（`NineGrid.Content.Editor`）：

| 类 | `kind` | 形态 | 壳 / 预制体 | Catalog 投影 |
|----|--------|------|-------------|--------------|
| **A. 场地图标** | `Room` | 下一步房间 **或** 导航（离开/上楼/下楼）：落九宫格，走上去驻留 1s 提交 | `iconPrefab` + `boardSlot`；空 prefab 回退 `CardChassisPaths.ResolveRoomIconPrefab` | 仅 `contentId`≡合法 `RoomKind` 时投影 `RoomDefinition`；`Leave`/`GoUp`/`GoDown` 为导航图标，**不**进 Catalog |
| **B. 特殊选项卡** | `ChoiceOption` | 进房后就地点选生效（主要是**卡店服务**；`Attack`/`Armor`/`Hp` 内容条目仍在，UI 入口已退役） | `房间选项标准模板.prefab` | **不**进玩法 Catalog |
| **C. 复用真卡** | `HelpCard` 等 | 商店货架 / 战斗房开局塞进卡组的道具与宝箱等 | 既有道具卡模版 | 照常投影 |

### 场地图标选房（#88 · ADR-0020）

- Spawn：`RoomIconBoardPresenter` 读 `PendingChoice`（Room 双选 / Navigation 单选），按 `boardSlot` 落格（撞格回退 1/3/2）；**不**进 `CardKind` 五套 Spawn、**不** `PlaceCard`
- 登记：`RoomIconOccupancy`（表现侧）供寻路软占与驻留；角色分 `WalkDestination`（离开/导航，可落格）与 `SoftBlockOnly`（货架/选项/刷新，禁落格、任意距离点击）
- 驻留：踩上图标 → 表现侧 1s（`RoomIconDwellSession`）→ IntentIntake `SelectRoom` + `EnterRoom`；跳走取消；只提交一次；计时器不进门禁
- 进房硬切：图标退场 + Avatar `MoveAvatarAction` 至格 5
- 编辑器：Room 条目「格位」字段；JSON `boardSlot`
- 选房：`RoomIconBoardPresenter` + 驻留提交（#88）；旧浮层 `RoomChoicePresenter` / `RoomChoisePanel` 接线已退役（#90）
- 悬停：Spawn 时挂 `BoardBriefTipHitProxy`（#89）；战斗真卡不挂
- 软占变更后 `RoomIconOccupancySlotHits.Refresh` 仅 Ensure 九框恒开（#102 已退役按软占关框）
- 认领：图标 / 货架 / 选项经 `BoardBriefTipHitProxy` / `ShopBoardHitProxy` 等向格位登记认领者；悬停文案与点击由 `GroundFieldHitSurface` 查认领同源派发

### 简要解释文字框与楼层提示（#89 · ADR-0020）

- 文案：`BoardBriefTipCopy`（房间 DisplayName+注入摘要 / 导航固定文案 / 楼层提示「楼层·Ⅱ」+ 房间类型「战斗房间」等；另备 `ForOptionOrShelf` 供房内货架·就地选项与候选——商店/卡店/特殊奖励房/属性房主循环已接线 #92/#93/#94/#137，非 M2 待落地）
- 会话：`BoardBriefTipSession`（悬停与 Notice；Notice 盖悬停；代数清；`HardClear` 双路硬清）
- 场景：`BoardBriefTipPresenter`（**#141 起场景序列化**于 `Panels/简要解释文字框`，`panelRoot` 显式指向面板自身；运行时 AddComponent 兜底仅存于未序列化的开发场景）→ 简要解释文字框（`EnsureExists` 优先绑定命名面板，sceneLoaded 再绑，避免无 TMP 孤儿）；`FloorHintPresenter` → 楼层提示
- 命中：场地图标 `BoardBriefTipHitProxy`；商店 `ShopBoardHitProxy`；卡店 `TavernBoardHitProxy`；特殊房 `RewardBoardHitProxy`；属性房候选 `AttributeBoardHitProxy`；离开图标仍 `BoardBriefTipHitProxy` + 驻留——均**认领格位**、不自建命中盒、不注册 Router（#102）
- 悬停/点击同源：`GroundFieldHitSurface` 读当前格认领者的 `BriefTipText` / `Activate`
- 显示：有文案时激活面板并打开底板 `SpriteRenderer`；无悬停/Notice 即隐藏
- 清理：场地板 `DespawnAll` 与进战 `StartBattleNode` / `PlayRealBattle` 调用 `HardClear`，避免房内 Notice（如「金币不足」）或悬停粘连进战斗；胜负 Notice 仍由 `HideNotice` 按时收起
- 胜负 / 房间 stub Notice：`GameFlowController.ShowNotice` 改走简要解释文字框，旧 `NoticeText` 不再写出
- **禁**：复活 `DescriptionManagerSingleton` / `DescriptionDisplayHook`；战斗真卡悬停写简要解释（右键详述另责）

### 商店房就地货架（#92 / #109 · ADR-0020 / ADR-0022 / ADR-0025）

- Core：进 `Shop` → `OfferShopSession` 固定 4 货架（宝箱 / 随机属性道具 / 恢复药水 / 食品）+ 容量未满时追加 `ExpandItemSlots`（道具牌格升级，50 金）+ 本次进店刷新价初值 10；`SelectReward` 扣 `Price`、直写道具卡格（满则拒）、**留店**；升级选项扣 50 金写 `ItemSlotsCapacity+1`（不发卡、不改 `ItemDeckCapacity`），满 5 后选项移除/不再出现；`RefreshShop` 扣刷新价并翻倍；`SkipHelpChoice` 出店（不加 skip 金）
- 刷新价作用域：**本次进店**（离开清零；再进店重新从 10 起）
- 表现：`ShopBoardPresenter` 落格 1/3/7/9 货架、4 升级选项（未满级）、2 刷新、8 离开；Avatar 硬切格 5；货架/刷新/升级任意距离点击；离开驻留 1s；金币不足写简要解释 Notice
- 货架真卡 `GroundCardMode`（预制体原生尺寸，与战斗卡同尺度）；升级/刷新就地选项 / 离开图标同按预制体根缩放（#104 / ADR-0024，已删 `RoomIconVisualFit`）；货架·刷新·升级登记 `SoftBlockOnly`，离开 `WalkDestination`
  - 落格只写世界位置、不 SetParent 到 `GroundAnchors/slotN`（`BoardSlotWorldPlacement`）；避免继承锚点 ×2 缩放
- 扣金后经 `InRoomGoldPresentation` 推 EventLog→HUD（非战斗无 GoldGainBeat）；**房内会话不持 ChoiceOverlay**（场地=ProtectedField，否则 BoardWalk ownerMismatch 全点不动）；Presenter 内勿嵌套 Set/清门；局内宝箱 Bounce 仍短暂持 overlay
- **购领入手牌**：货架为 `SpawnPresentationOnly`；`SelectReward` 接受后经 `InRoomItemAcquirePresentation` 读 EventLog `CardSpawned`→SpawnView(Core uid)→`PullFromGroundAsync` 接入手牌（失败则 `TryPlaceInHandImmediate`）；禁止只 Release 货架导致「Core 有牌、手牌看不见」
- 离开监视：先 `mActive=true` 再 `StartAvatarWatch`；失败驻留须重开计时
- 货架挂 `ShopBoardHitProxy` 时禁用同 GO `GroundCardHitProxy`，避免误入 Pickup
- `GameFlowOrchestrator.PlayRoomIconChoiceAsync`：图标驻留 Select+Enter 后，若进消费/特殊房会话则 `PresentInRoomSessionAfterEnterAsync` 刷商店场地板（不再壳层二次 EnterRoom）

### 卡店房就地服务（#93 · ADR-0020 / ADR-0022）

- Core：进 `Tavern` → `OfferTavernSession` 三项服务（`UpgradeItemStats` / `FixItem` / `ExpandItemCapacity`，各 50 金）+ 本次进店刷新价初值 10；扩容写 `ItemDeckCapacity+1`；强化写 `ItemStatBonus+3`（跨节点应用属 #97）；`RefreshShop` 同商店规则；`SkipHelpChoice` 出店
- **唯一嵌套选择**：「道具卡固定」→ 池切 `tavern.fixItem`，候选来自 `ItemSourcePoolDefIds`；确认后 `AddFixedItemCard` + 扣费回主面；`SkipHelpChoice` 在子池取消回主面（不扣费、不离店）
- 表现：`TavernBoardPresenter` 落格 1/3/7 服务选项（`房间选项标准模板`）、2 刷新、8 离开；二级选择时服务/刷新退场，候选真卡铺格 1/3/4/6/7/9；离开 tip 改「取消选择」
- 服务/刷新选项与候选真卡均按预制体原生尺寸（#104，不 Fit）；服务·刷新·候选 `SoftBlockOnly`，离开 `WalkDestination`；扣金同商店走 `InRoomGoldPresentation`；离开监视与 **不持 ChoiceOverlay** 约定同商店；候选真卡禁用 `GroundCardHitProxy`
- `GameFlowOrchestrator.PresentInRoomSessionAfterEnterAsync`：`IsTavernPool` / `IsTavernFixItemPool` 走卡店场地板

### 特殊奖励房（#94 · ADR-0020 / ADR-0022）

- Core：进 `TreasureReward` → 1 宝箱 + 3 随机道具（`ItemSourcePoolDefIds`）；进 `ItemReward` → 2 属性道具（40/40/20）+ 3 随机道具；pool=`reward.treasure` / `reward.item`；`SelectReward` **免费**直写道具卡格（满则拒）并留房；`SkipHelpChoice` 离开放弃剩余（不加 skip 金）
- 表现：`RewardBoardPresenter` 落格 1/2/3/7/9 真卡、8 离开；Avatar 硬切格 5；任意距离点击拿走；离开驻留 1s；悬停 tip=卡名+效果（无价格）
- 真卡 `GroundCardMode`（预制体原生尺寸，#104）；货架 `SoftBlockOnly`，离开 `WalkDestination`；离开监视约定同商店（免费拿无需推金；**不持 ChoiceOverlay**；禁用 `GroundCardHitProxy`）
- **领取入手牌**：同商店，经 `InRoomItemAcquirePresentation` 把 Core ItemSlots 新卡从货架位接入手牌（ADR-0025）
- `GameFlowOrchestrator.PresentInRoomSessionAfterEnterAsync`：`IsSpecialRewardPool` 走特殊房场地板；道具奖励房选房图标 #138 已补齐（`道具奖励图标.prefab`，JSON `iconPrefab` 经编辑器双写）

### 属性房三选二会话（#136/#137 · ADR-0031）

- Core：进 `Attribute` → `OfferAttributePickSession` 生成 3 个加权候选（`attribute.pick` 池，`Kind=AttributePick`，策划权重 40/40/20，允许同种重复）；`SelectReward` 按候选实例记录选择并移除该实例（不可重复点同实例）；选满 2 张提交 `RunModel.AttributePickDefIds` 并推进节点；`SkipHelpChoice` 放弃未选完的候选并推进节点（不加 skip 金）
- 选择结果 **不写道具卡格**：下一战斗节点 `BuildNodeDeckOptions` 消费 `AttributePickDefIds` 注入玩家侧卡组（消费后清空；无选择结果不注入）
- 表现（#137）：`AttributeBoardPresenter` 落格 1/3/7 三张候选真卡、8 离开；Avatar 硬切格 5；任意距离点击选择（`AttributeBoardHitProxy`），首次选择金框驻留视觉确认、继续等第二次；选满两张经简要解释 Notice 播报完成并清理候选（房间推进由 Core/过场接续）；离开驻留 1s 放弃未选完候选（`SkipHelpChoice`，不加 skip 金）
- 候选真卡 `GroundCardMode`（预制体原生尺寸，不 Fit / 不 parent 到锚点，#104 / ADR-0024）；候选 `SoftBlockOnly`，离开 `WalkDestination`；离开监视约定同商店（**不持 ChoiceOverlay**；禁用 `GroundCardHitProxy`）；候选卡面显示当前名称 + 描述 tip（`CardPresentationConfigCatalog`，无价格）
- 点击经 `RewardChoiceCoreHook.SelectReward`（`RewardChoiceInputController` → IntentIntake → Core `SelectReward`）；视觉候选 → 当前 Pending 索引经 `AttributePickIndexResolver`（Core 移除已选实例后索引前移）；会话变更（Generation/离开）经 `ResyncFromPending` 收尾
- `GameFlowOrchestrator.PresentInRoomSessionAfterEnterAsync`：`IsAttributePickPool` 走 `PresentAttributeBoardAsync` 属性房场地板；`PhaseSystem` 在 `RewardItemChoice` 为属性房池加 `MoveAvatar`（离开图标需 BoardWalk）

### Avatar 跳格（已落地 · ADR-0019 / #88 放宽）

- 意图 `InputIntentKinds.BoardWalk` → IntentIntake → `BoardWalkIntentScriptFactory` → `IAvatarWalkSystem.SetDestination`
- Core：`MoveAvatar`（单邻格；RoomChoice/RoomEvent 允许踩非空以配合软占回退）+ `AvatarWalkPathfinder` 两阶段 BFS：途经优先完全空置（绕开软占/真卡），无空路再允许踩软占；终点为「完全空且非软占」或 `WalkDestination` 图标格（货架/选项 SoftBlockOnly 不可落格）
- 表现：`HopAvatarToSlotAsync` 复用旋转 hop；半空改目标等落地后重规划
- `\0` QuickTest：流程测试通道（空 skillIds / 空 trap / Sequential 节点序，内容同正式开局 + HP99/ATK5），经 `GameFlowRunOptions.CreateQuickTest` 走 QuickTest 镜像；正式「开始游戏」走 `GameFlowController.BeginFormalRun` → `GameFlowRunOptions.CreateFormal`（无 QuickTest 标志/作弊/动态装配，#125）；战斗内 **KeypadMinus** 跳过战斗（仅 QuickTest，`TryForceNodeVictory` → `MarkLeaveTrapBroken` + `TryCompleteClearedNode` 后 `ClearResidualCombatFieldViews` 收口机关/帮助/漏网怪视图，避免盖住房间图标；卡组抽牌堆残留由 `TryEnterNodeSettlement` → `RaiseSettlementReady` → `ClearResidualBattleDeckViews` 同步卸掉）
- 空槽 / 未认领格：九框恒开；点击一律提交 BoardWalk 或 Explore，合法性交 IntentIntake（已退役 `BoardWalkSlotHitPolicy` / 软占关框 / Avatar `int.MinValue` 穿透）
- Avatar 朝向：`AvatarBoardFacingController` 按卡面图标当前世界 X 相对指针，不锁死格5
- **战斗 `InteractionLoop` 禁走**；进下一房 Avatar 硬切格 5

### 节点循环（#88 · ADR-0021）

- `GameFlowOrchestrator.RunNodeCycleAsync` 按 `MapNodeProgression.EntersInteractionLoop` 分支：节点 4/7 走 `PlayNonCombatNodeAsync`（跳过战斗；4=离开导航、7=层主房图标战前缓冲），其余战斗 → 奖励 → `PlayRoomIconChoiceAsync`（驻留进房后接 `PresentInRoomSessionAfterEnterAsync`；旧独立 `PlayRoomEventAsync` 已并入此路径）

### 设计房间名 ↔ `RoomKind`（现状）

| 策划名 | `RoomKind` / JSON `contentId` | 默认图标预制体 | 默认格位 |
|--------|-------------------------------|----------------|----------|
| 困难房 | `Elite` | `困难战斗图标` | 1 |
| 层主房 | `Boss` | `地形图标/Boss房图标` | 3 |
| 金币房 | `Gold` | `钱袋图标` | 3 |
| 宝箱房 | `Treasure` | `宝箱图标` | 3 |
| 恢复房 / 温泉 | `Fountain` | `温泉图标` | 1 |
| 属性房 | `Attribute` | `属性提升图标` | 1 |
| 商店 | `Shop` | `商店图标` | 1 |
| 卡店 | `Tavern`（显示名「卡店」） | `牌店图标` | 3 |
| 宝箱奖励房 | `TreasureReward` | `宝箱图标`（暂） | 1 |
| 道具奖励房 | `ItemReward` | `道具奖励图标` | 1 |
| 离开 | `Leave`（非 `RoomKind`） | `离开图标` | 2 |
| 上楼 | `GoUp` | `上楼图标` | 2 |
| 下楼 | `GoDown` | `下楼图标` | 2 |

**已删**：`Battle`（随机战斗房从具体战斗房类型抽）、`Event`（由 `Attribute` 承接）。

**开局注入（ADR-0022 / #95 / #136）**：`RoomDefinition.OpeningInjects` 声明往玩家侧/怪物侧塞哪些卡；`RewardSystem.BuildNodeDeckOptions` 按 `RunModel.Room` 执行（玩家侧在固定卡之后；怪物侧在节点序列抽卡之后）。**属性房例外**（#136 / ADR-0031）：不自动抽取，只注入玩家三选二会话提交的 `RunModel.AttributePickDefIds`（消费后清空）。携带卡包开局倒空已退役（ADR-0025 / #108）。节点 1/5 `RandomBattle` 开局若尚未是战斗房则现场抽一种（不含困难房）。

**勿与选项卡混淆（设计案对照）**

| 表象 | 设计案实际 | 配置落点 |
|------|------------|----------|
| 「回满血」 | **恢复房**开局塞 **食品卡**（HelpCard）；非就地选项卡 | 不建 `HealFull` ChoiceOption |
| 「血量+2 / 攻击+1 / 护甲+1」 | **属性房**进房生成 3 个加权候选（血量/加甲/加攻 40/40/20，可重复），玩家**三选二**加入本关玩家侧卡组（#136 / ADR-0031，选择结果注入见上「开局注入」段）；旧局内 BounceFan `Attack`/`Armor`/`Hp` 三选一 UI 已退役（#90） | HelpCard 真卡；ChoiceOption 内容条目仍在（属性房真实三选二接线见本 Map #136/#137） |
| 「给钱」 | **金币房**开局塞 **金币卡** | HelpCard，不建 `GainGold` ChoiceOption |
| 卡店三项 + 刷新 | 策划消费房明文服务 | `UpgradeItemStats` / `FixItem` / `ExpandItemCapacity` / `RefreshShop` |
| 商店道具牌格升级 | 商店就地扩容道具卡格（3…5） | `ExpandItemSlots`（勿与卡店 `ExpandItemCapacity` 混淆） |

特殊选项卡种子（`ChoiceOption`）：卡店服务 `UpgradeItemStats`/`FixItem`/`ExpandItemCapacity`/`RefreshShop`；商店 `ExpandItemSlots`；`Attack`/`Armor`/`Hp` 内容条目仍在（UI 入口 #90 已退役，属性房真实三选二接线见本 Map #136/#137）。

局内选房走场地图标 + 驻留提交；`RoomChoicePresenter` 与 `RoomChoisePanel` 接线已删（#90）。`SelectorManagerSingleton` 只剩 Bounce；扇形 `BounceFanChoicePresenter` 仅服务宝箱开遗物。通关三选一已退役（ADR-0021）：清关进 `RoomChoice` 后由 `NodeSettlementReadiness` 唤醒主循环刷图标；`TryForceNodeVictory` 同路走 `TryCompleteClearedNode`。图标落格只对齐世界位置、保留预制体根缩放（#100 校准进目标盒约 `2.55 × 3.7`；#104 已删 `RoomIconVisualFit` / `slotHitBoxSize`）；`BoardBriefTipHitProxy` 配置 Walk 槽后单击转发 BoardWalk。房内货架/就地选项卡点选已接线（商店 #92、卡店 #93、特殊奖励房 #94；入口见上文各节），不再属 M2 待落地。

## ADR 不变量（摘要）

- Batch/ack：表演未就位前 Core 不推进下一批（ADR-0001）  
- 逻辑占格唯一归 Core `BoardModel`  
- 卡面可见值经投影 Commit（ADR-0002）；禁止队列外正式 Setter 通路  
- 输入唯一收口 IntentIntake + 两轴门禁（ADR-0004）  
- 非战斗 Avatar 正交跳格（ADR-0019）；战斗相位禁走  
- 卡面**数值**只经结算指令在表演锚点提交，不直读 Core（ADR-0005）；装饰消费者经同一排期器多处理器分发（ADR-0007）  
- **触发可见因果** / **基础触发表现**：无命中帧、靠运动落地才成立的触发，Impact 须在条件可见之后；Triggered 卡牌触发须 `EffectTriggered` + 在场持有者 v1 缩放（ADR-0018）  
- Windows Player 高回报率鼠标：`RIDEV_NOLEGACY` + 轮询注入 Input System；命中走 `PointerHitRouter`，禁 `OnMouse*`（ADR-0006）
- 格位命中框为九宫格唯一命中权威、一格一认领者、命中恒成功、禁用启停 collider 表达规则（ADR-0023 / #101+#102）
- 落格对象不 SetParent 到格位锚点、尺寸权威在预制体、运行时不写 `localScale` 绝对值修正；mode/hover/hop 倍率为相对预制体基准且可还原（ADR-0024 / #104）

## 指针与命中（ADR-0006 / ADR-0023）

- **读口**：`Flow/WorldPointerUtility` —— 屏幕/世界/主键边沿；优先 New Input `Mouse.current`，可 `SetOverrideSource`（测试 / 未来平台）；右键边沿 `WasSecondaryPressedThisFrame`
- **键盘**：`Flow/KeyboardUtility` —— New Input only 下替代 `Input.GetKey*`（DevTest 热键 / UITestBootstrap / Escape Esc 跳过）
- **命中**：`Flow/PointerHitRouter`（`RuntimeInitializeOnLoad` 自举；`-40`）轮询 `PointerHitRegistry`；**手牌按下优先** `CardHandManagerSingleton.TryBeginDragFromHoveredCard`（Hand `-50` 先刷 hover 槽位带）；**右键**开 `CardInspectOverlayPresenter`（场卡经 `GroundFieldHitSurface.TryResolveInspectCard` 取认领卡，手牌仍走 collider/`TryPeekHoveredCardForInspect`；再右键或关闭钮关）
- **局内 UI 叠层**：`BattleUiDimmerOverlay` + `UiOverlayHitProxy`（半黑屏吞点 / 关闭钮）；`PresentationInputGates.BattleUiOverlayActive` 只读投影
- **代理**：`GroundFieldHitSurface`（场地面单一注册，解格号 → 查 `SlotClaimRegistry`）/ `HandCardHitProxy` / `BoardSelectParkedCardHitProxy` 实现 `IPointerHitTarget`；落格 `GroundCardHitProxy` / `BoardBriefTipHitProxy` / 商店·卡店·奖励 Board HitProxy 改为**认领登记**（无自建命中盒、不注册 Router）；`GroundSlotHitProxy` 遗留壳
- **Win Player mitigation**：`Platform/WindowsHighPollingMouseMitigation`（`#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`）
- **工程设置**：`activeInputHandler = 1`（New Input System only）
- **专项回归**：125 / 1000 / 4000+ Hz × 窗口/无边框/全屏；hover、空槽、点怪、手牌拖放、BoardSelect、BounceFan、右键详述

### 命中权威盘点（现状）

| 权威 | 机制 | 服务 |
|------|------|------|
| `PointerHitRouter` + `PointerHitRegistry` | 按目标平面 `ScreenToWorld` + Overlap（场地面为九框）；`HitSortOrder` → `HitTypePriority`；**同分报装配错误** | 棋盘 / 手牌 / 覆层主路径 |
| `PointerHitSurfacePriorities` | 场地面=28 / 手牌带=20 / 覆层=10，显式互异；覆层不再用 TypePriority=100 + HitSort=100000 抢几何 | 表面仲裁 |
| `GroundFieldHitSurface` + `SlotClaimRegistry` | 单一场地面；场景格位框恒开；查认领者派发悬停/点击；**同表面跨格时 Router 调 `RefreshPointerHover`**；移格/旋转/换位 **起飞卸认领、落地再登记**（ADR-0023） | 棋盘命中与认领 |
| `CardHandManagerSingleton` 布局带 | `handHitBoxSize` AABB 数学，不用 collider；`DefaultExecutionOrder(-50)` | 手牌 hover + 起拖 |
| `CardHandManagerSingleton` 拖拽落点 | 交棒 `GroundFieldView.TryResolveSlotAtWorld`（场地面九框） | 拖拽落格 |
| `WorldPointerUtility.TryOverlapColliderOnPlane` | 与 Router 同平面换算；已退役 z=0 `TryPickCollider` | 主菜单 StartRun / Quit；HUD 血槽悬停；StartRunHoverScale |
| `BounceFanChoicePresenter` | 容器本地固定 AABB（`BaseLocalPosition` + `hitBoxSize`），倒序遍历；**不**跟悬停 tween、**不**启用卡面 collider；ChoiceOverlay 下合法悬停可开右键详述，详述打开期间屏蔽点选 | 战斗内扇形三选一（宝箱遗物） |
| `Physics2D.GetRayIntersection` | — | `Arts/` demo，不属表现层 |

板面参照：底板 Sliced `1.9 × 2.45` × 2 = 世界 `3.8 × 4.9`；格距 `5 × 5.5`；`GroundAnchors/slotN` 自带 `BoxCollider2D` 本地 `1.625 × 2.0625`（已启用）。

### 收敛方向（ADR-0023 / ADR-0024）

**已落地（#101–#105）**：格位命中框场景权威 + 场地面单一注册 + 表面优先级互异/同分告警 + 一格一认领（`SlotClaimRegistry`）+ 落格对象去 collider/Router + 退役 `BoardWalkSlotHitPolicy` / 软占关框 / Avatar 穿透 + 运行时停写格位 size/offset + 手牌拖拽落点交棒场地面 + 悬停简要解释与点击同源（认领者）+ 退役野生拾取（`TryPickCollider` / legacy `OnMouse*` / HUD 私有 z=0 换算）+ 落格不 SetParent / 删 Fit / 删 `slotHitBoxSize` / 动效相对预制体基准 + **结构护栏全集**（生产禁 parent 到 `GroundAnchors`、禁 Fit 回流、落格路径禁写 `localScale`；见 `docs/code-map/tests.md`）。
## Core 表演契约与统一表现管线（#54–#62）

- `NineGrid.Core.PresentationBeat`：`Impact` / `Settled` / `None`（**表演消费归属**，非仅卡面；升级路径注释在枚举旁）
- `PresentationEventMapEntry.Beat` + `NoneReason`：每个 `CoreEventType` 显式锚点归属；`None` 必须写「为何无表演消费」
- 数值类指令绝对值：血甲用既有 `RemainingHp`/`RemainingArmor`；`BaseStatModified` / 生成类事件攻用 `ResultValue`（`Amount` 仍为 StatId，`Delta` 仍为增量）；`CardKilled` 携带 `RemainingHp`；`RewardOffered` Message 携带候选项攻/甲/血（`RewardOfferFaceEncoding`）
- 穷尽性：`NineGrid.Core.Tests.PresentationEventMapBeatExhaustivenessTests`
- **排期器** `Flow/Presentation/BattleBeatScheduler`：批次开启装载非 `None` 指令；`ReportBeat` 交给第一个 `IBattleBeatHandler.TryApply` 成功者；Settled 后未消费只报不改；支持 `PresentStandalone`（非锁步旁路冲刷，恢复当批 pending）；`FlushImpactExcept` 按 Kind 跳过（ADR-0018）
- **处理器** `IBattleBeatHandler`：`CardFaceStatHandler` 只从指令赋值 → `ManagedCard.CommitPresentation`（含 `OfferReward` 按 DefId 匹配 Bounce 负 uid 卡；`MaxHp` 取 `RemainingHp` 作当前血）；`PlayerInfoHudBeatHandler` 对 Avatar 血甲旁路写 HUD（`MaxHp` 走 `ApplyHpAndMaxHp(RemainingHp, ResultValue)`；return false 留给卡面认领）；装饰 `DamageFloaterBeatHandler` / `EffectTriggerPulseBeatHandler` / `GoldGainBeatHandler` 分别在 Impact / Settled 消费飘字、FX、金币，不占主线 ack；数值 Commit 后 `CardFacePresentationBinder` 可对配对图标做非阻塞缩放装饰（不占 ack）
  - **护甲显示**：卡面甲 = 当前护甲；玩家信息 HUD 甲 = 有效护甲；UI 文案用「护甲」不写「防御」（「防御」仅 `ContentRole` 投放粗轴）。标准伤害公式 / 伤害减免 / 无视护甲见 [ADR-0028](../adr/0028-damage-formula-armor-and-reduction.md)
- **统一冲刷** `BattleBeatFlush.FlushBeats`（Impact→Settled）：`PresentStep` 就位回执前调用；非锁步（房间/选择/拾取）走 `PresentEventLogSlice`；Bounce spawn 后走 `PresentLatestEventOfType(RewardOffered)`（单条 PresentStandalone）
- **翻牌门控** `FlipPlaybackCoordinator`：Handler 入队串行 `PlayFlipAsync`；`BattleBeatScheduler.FlushUpdateFaceUp` / `BattleBeatFlush.FlushUpdateFaceUp` 供 `PresentStep` 在 hop 通道 `channel.Begin` 前只刷 FaceUp；战斗通道可跳过前置刷、把 FaceUp 留到 `FlushBeats`；Idle 门控在 Begin 与 ack 两侧（ADR-0016）
- **生成绝对值** `CardFaceEventValues.WithFaceAbsolutes`：`CardSpawned` / 带 uid 的 `CardDealt` / `AvatarAppeared` 写入造卡/发牌时攻甲血
- **Permanent 有效攻旁路**（ADR-0005 细化，非对账）：Conditional/常驻光环改有效攻时，Core 发 `BaseStatModified(ResultValue=GetEffectiveInt(Attack))`——`AddStatModifier`/`CommitPermanentAttackFace` Apply、以及 Swap/Rotate/Remove/Kill/`DeactivateOwnerEffects` 对盘面「带条件的 Permanent Attack」补扫；**`ModifyBaseStat` 模板原子（加攻卡/属性房加攻/献身类技能）的 Attack 分支同样提交有效攻**（与 `AppendPermanentAttackFaceCommit` 同构，禁止发基础值——否则带遗物/光环时显示落后于伤害结算）；Temporary 交战加成仍不上卡面；表现层不对账、不直读 Core
- **奖励候选项** `RewardEntry` 投影绝对值 + `RewardOffered` Settled；Bounce spawn 后 `PresentLatestEventOfType` 二次提交；禁 `clearCombatStats` 数值旁路
- **开局引导** `Flow/Presentation/CardFaceGenerationBootstrap`：非锁步 Opening 从事件日志重放生成类指令；BoardSelect 视图重 Spawn 用 `ApplyFaceHistoryForUid` 重放该 uid 的生成+后续数值指令（与 Settled 同一 Handler）
- **报点**：攻击/反击命中帧 → `Impact`；用道具 Present：Vacate 前 `FlushImpactExcept(TriggerEffect)`（保飘字坐标，不提前消费触发脉冲）；`TriggerEffect` 由后续 Drain 运动落地 Impact（无盘面 delta 则由 `PresentStep` `FlushBeats`）消费。奖励 Choice：有盘面 Drain 时先 `PresentEventLogSliceExcluding(TriggerEffect)`，Drain 后再 `PresentEventLogSliceOnly(TriggerEffect)`；空 delta 仍整批 `PresentEventLogSlice`。盘面 Drain：全部运动步（及同批前置 Deal）播完后、**首个 Remove 前**冲刷 `Impact`（无 Remove 则 Drain 尾冲刷）。禁止在首个 Deal 前抢跑——同批 `[Deal, Rotate]` 时否则脉冲/扣血会早于旋转（ADR-0018）；`PresentStep`：FaceUp 先刷 → 通道 → `FlushBeats` → 等翻牌 Idle → `TryAcknowledge`
- **组合根**：`PresentationCompositionRoot` 注册排期器（`PlayerInfoHudBeatHandler` + `CardFaceStatHandler` + `CardFaceFlipBeatHandler` + 飘字/FX/金币装饰处理器），接线 `FlushUpdateFaceUp` / `FlushImpactExcept`，并订阅 `Evt_PresentationBatchOpened`
- **读写约定补则**：卡面数值只经排期器/生成引导，禁止 Mapper 首次 `TryRead` 写数值；JSON `stats` 仅 Catalog 造卡用；用道具 Present 只 `RefreshVisualsPreservingCommittedStatsOnAllSpawned`；探索/用道具批次投影不写卡面数值；`MarkFieldDead` 只标死亡态不改血量；底盘数值 Setter 非公开；禁 `PresentEffectTriggersFromEventLog` / `SpawnDamagePopups` / `PresentGoldGainsFromEventLog` EventLog 旁路；战中 PlayerInfo 不经 `SyncFromCore`（开局/作弊白名单除外）；Bounce 不得靠 DefId 清战斗数值上数
- **ADR**：[ADR-0005](../adr/0005-card-face-beat-commit.md)、[ADR-0007](../adr/0007-unified-presentation-pipeline.md)、[ADR-0018](../adr/0018-trigger-visible-causality.md)（与 0001/0002/0004 交叉引用）；指针/高回报率见 [ADR-0006](../adr/0006-windows-high-polling-mouse-mitigation.md)