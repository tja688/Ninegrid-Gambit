# LivingUI · 活字与世界 UI

程序集：`NineGrid.LivingUI`（运行时 Core + Unity + Demo）、`NineGrid.LivingUI.Editor`、`NineGrid.LivingUI.Tests`。

命名空间：`NineGrid.LivingUI`（Core）、`NineGrid.LivingUI.Unity`、`NineGrid.LivingUI.Demo`、`NineGrid.LivingUI.Editor`、`NineGrid.LivingUI.Tests`。

## 架构摘要

- **大盘权威**：运行时只驱动名为「大盘」的根下载体 `1`…`12`（`SpriteRenderer`）；七组「大盘构型N-…」为蓝图快照源，Capture 后 `SetActive(false)`。
- **A 类内容**：挂在 `CarrierView.ContentAttach` 下，带 `LivingUiContentMarker`；由 `LivingUiContentDriver` 投影局部位姿。
- **B 类锚点**：`CarrierAnchorRegistry` 按名发布世界位姿；LivingUI 不移动外部玩法物体。
- **转场**：纯数据 `TransitionPlanner` + `LivingUiTransitionPlayer`；内容进退场由 `LivingUiContentPolicy`（Invariant / Scale）决定。

---

## Core（`LivingUI/Core/`）

### `LivingUiContracts.cs`

| 符号 | 职责 |
| --- | --- |
| `LivingUiLayoutId` | 七构型枚举：`MainMenu`、`CharacterChoice`、`Battle`、`RewardChoice`、`DeckPreview`、`Room`、`Route`。 |
| `LivingUiTerminal` | 单载体终态：id、中心位、尺寸、sortingLayerId/Order。 |
| `LivingUiLayout` | 某构型的 `carrierId → Terminal` 字典；缺 id 抛 `KeyNotFoundException`。 |
| `LivingUiCarrierState` | 运行时采样态：位、速度、尺寸、尺寸速度。 |
| `LivingUiTransitionStyle` | 可序列化手感：时长/距离/卡农/出入画延迟/尺寸地板与天花板/缓动。 |
| `ILivingUiTransitionPlanner` | `Plan(liveStates, target, stageBounds, style, envelopeOverrides?)`。 |

`using`：`System`、`System.Collections.Generic`、`UnityEngine`。无 Flow/Cards。

### `LivingUiContentContracts.cs`

| 符号 | 职责 |
| --- | --- |
| `LivingUiContentAnchor` | TopLeft / BottomLeft / BottomRight / TopRight / Center。 |
| `LivingUiContentMotionMode` | `Invariant`（跨构型共存）/ `Scale`（进退场缩放）。 |
| `LivingUiContentLocalPose` | 作者局部位 + 缩放。 |
| `LivingUiContentEnvelope` | 随行包裹尺寸，抬高流动下限。 |
| `LivingUiContentBinding` | 纯数据绑定：contentId、carrierId、pose、anchor、faceLayout?、envelope、authoringSize。 |
| `LivingUiContentPhase` | Hidden / Stable / Entering / Exiting。 |
| `LivingUiContentPolicy` | 静态：`ResolveMotionMode`、`EvaluatePhase`（含单 Face 绑定重载）。 |

### `LivingUiContentNames.cs`

| 符号 | 职责 |
| --- | --- |
| `LivingUiContentNames` | 剥离/附加「（原初元素）」/ `(原初元素)` 后缀；`BaseName` / `BaseNameEquals` / `EnsurePrimordialName`。限字等其它括号保留在身份内。 |

### `LivingUiContentProjector.cs`

| 符号 | 职责 |
| --- | --- |
| `LivingUiContentProjection` | 单帧输出：localPosition、localScale、visible。 |
| `LivingUiContentProjector` | 锚点角解算；Invariant/Scale（及 FromCenter 变体）；进/退场进度与 envelope 聚合；`ResolveCarrierFlowFloor`。 |

常量：`DefaultExitDuration=0.1`、`VisibleScaleEpsilon`、`EnterProgressChannelEpsilon`。

### `LivingUiTransitionPlanner.cs`

| 符号 | 职责 |
| --- | --- |
| `LivingUiMotionProgram` | 单载体运动程序：五次位 + 有界尺寸；`Sample(planTime)`。 |
| `LivingUiTransitionPlan` | generation、targetLayout、programs、makespan。 |
| `TransitionPlanner` | 实现 `ILivingUiTransitionPlanner`：出画/入画判定、卡农 startOffset、envelope 地板、生成 `QuinticMotion`/`BoundedScalarMotion`。 |

### `LivingUiTransitionPlayer.cs`

| 符号 | 职责 |
| --- | --- |
| `LivingUiTransitionPlayer` | `TryBegin`（仅接受更高 generation）、`Advance`、`Sample`；`IsPlaying`。 |

无 UnityEngine 依赖。

### `LivingUiEasing.cs`

| 符号 | 职责 |
| --- | --- |
| `LivingUiEasingType` | Linear…Bounce + Custom。 |
| `LivingUiEasing` | `GetEasing` / `GetDerivative`；内置缓动与 Custom 数值导数。 |

### `QuinticMotion.cs`

| 符号 | 职责 |
| --- | --- |
| `QuinticMotion` | 带初速的五次多项式，或静止时的缓动插值；位置/速度求值；`StaysWithin` 采样。 |
| `BoundedScalarMotion` | 尺寸通道：直达或制动+到达两段；越界抛异常。 |

---

## Unity（`LivingUI/Unity/`）

执行顺序（源码属性）：SceneLayoutSource −300 → Director −200 → ContentController −190 → ContentDriver −180 → Demo 输入 −150/−100。

### `LivingUiSceneLayoutSource.cs`

- 职责：Awake/`Capture` 抓蓝图终态、绑定大盘载体、收集 `ContentBindings`、关闭蓝图、注入 `LivingUiContentController.RebuildByNamePresence`。
- 默认蓝图根名：`大盘构型0-主菜单` … `大盘构型6-预备待定`；权威根名常量 `LiveRootName = "大盘"`。
- 暴露：`LiveRoot`、`Snapshots`、`Carriers`、`CarrierViews`、`ContentBindings`、`GetLayout`。
- 耦合：仅 LivingUI 内部；场景名约定是与策划场景的契约，非 Flow API。

### `LivingUiDirector.cs`

- 职责：舞台矩形（`Camera` 视口世界 AABB）；`Commit` / `Preview` / `ClearPreview`；规划并播放转场；写载体位姿/尺寸与 sorting。
- 启动：`ApplyLayoutImmediate(MainMenu)`。
- 时间：`Time.unscaledDeltaTime * playbackSpeed`。
- 耦合：依赖 `LivingUiSceneLayoutSource`；内容 envelope 经 `LivingUiContentProjector.AggregateEnvelopeFloors`。无 Flow/Cards。

### `CarrierView.cs`

- 职责：载体视图 seam——`SpriteRenderer` 皮肤、子节点 `ContentAttach`、`CarrierAnchorRegistry`、carrierId（可从物体名解析）。
- `EnsureWired` 运行时自动装配。

### `CarrierAnchorRegistry.cs`

- 职责：B 类命名锚点 → 世界位姿查询 `TryGetWorldPose`。
- 耦合声明（注释）：游戏系统自取位姿；本程序集无调用方。Cards 等若消费，靠场景装配 + 同名字符串，非编译依赖。

### `LivingUiContentMarker.cs`

- 职责：A 类内容声明——contentId、carrierId、anchor、faceLayout、restrictToFace、envelope、authoringSize、原初中心系 authored pose；`ToBinding()`。
- 编辑器/迁移写入：`ApplyAuthored`、`SetAuthoredPose`、`CaptureAuthoredPoseFromTransform`、`EnsureAuthoringSize`、`ConvertAnchorOffsetToCenterLocal`。

### `LivingUiContentController.cs`

- 职责：构型→Marker 映射；蓝图同基础名出现 → 多构型 Invariant；提供 `ResolveMotionMode` / `ResolvePhase` / `ResolveLayoutAnchor`。
- 构建路径：显式 `layoutContents` → 否则 `RebuildByNamePresence` → 否则按 Marker 自动登记。
- 查找内容时只认父名为 `ContentAttach` 或历史 `Anchors`；跳过面板名 `1`…`12`。

### `LivingUiContentDriver.cs`

- 职责：`LateUpdate` 驱动大盘下全部 Marker：Hidden 关 GO；Invariant/Scale 投影；进场跟载体进度、退场短时缩至 0。
- 无 Controller 时退化为单 Face `LivingUiContentPolicy`。

### `LivingUiBlueprintPoseSampler.cs`

- 职责：静态缓存蓝图根；运行时定性采样锚点；`TrySamplePoseForEditor` 供迁移（运行时驱动不应调用位姿采样）。
- `InvalidateCache` 由 Driver OnEnable / Editor 批处理调用。

### `LivingUiStableHitZoneRoot.cs`

- 职责：烘焙好的世界 AABB 命中区（不随载体 pop）；`TryHit` → carrierId。
- 供主菜单 Hover Demo 使用。

---

## Demo（`LivingUI/Demo/`）

同属 `NineGrid.LivingUI` 程序集。

### `LivingUiDemoInput.cs`

- 职责：数字键 1–5 `Commit` 各构型；Battle 下稳定区 Hover → `Preview(DeckPreview)`。
- `using NineGrid.LivingUI.Unity`；无 Flow/Cards。
- 稳定区用 `director.GetTerminalRect(Battle, hoverCarrierId)`（默认载体 12），不跟运动载体。

### `LivingUiMainMenuHover.cs`

- 职责：主菜单且非转场时，稳定命中区 → 单载体位移/放大 pop；标签随 ContentAttach 自然跟随。
- 依赖 `LivingUiStableHitZoneRoot`（可按名 `StableHitZoneRoot` 查找）。

---

## Editor（`LivingUI/Editor/`）

程序集 `NineGrid.LivingUI.Editor`。菜单前缀：`TableNine/LivingUI/…` 或 `NineGrid/Living UI/…`。

### `LivingUiFeelWindow.cs`

- `EditorWindow`；菜单 `NineGrid/Living UI/动效手感调试`。
- 编辑场景中 `LivingUiDirector` 的 `playbackSpeed` 与 `LivingUiTransitionStyle`。

### `LivingUiPrimordialPoseInit.cs`

- 菜单：`Init Primordial Poses From First Blueprint Occurrence`。
- 按构型 0→6 首次出现，烘焙大盘原初中心系位姿并加「（原初元素）」后缀。

### `LivingUiLiveContentPoseResync.cs`

- 已废弃菜单：转发警告并调用 `LivingUiPrimordialPoseInit.Init`。

### `LivingUiMappingFixBatch.cs`

- 修补错误面板映射与构型 4 卡组变体（`MoveRename` 等）；操作场景 Transform/Marker。
- 内容名含 `AttackText`/`HpText`/`ArmorText` 等——**场景字符串**，非 Cards 类型引用。

### `LivingUiContentBindMigrator.cs`

- 菜单：`Migrate Anchors → Live ContentAttach (1-4)`。
- 把蓝图 `Anchors` 顶层内容挂到大盘 `ContentAttach` 并装配 Marker。

### `LivingUiContentAnchorMigrator.cs`

- 菜单：`Migrate Markers → Anchor Architecture`。
- 统一 Center 锚点语义；确保挂有 `LivingUiContentController`。

### `LivingUiAuthorityStageMigrator.cs`

- 菜单：`Migrate Authority Stage`。
- 大盘权威提升、蓝图父子化、同名合并、Marker/Director 布线。
- **内嵌布局元素名表**（战斗等）含：`CardHandAnchors`、`GroundAnchors`、`CardDeckAnchors`、`RelicPanelAnchors`、`DeckCardDeckAnchors`、`DeckRelicPanelAnchors`、`Card Info Text` 等。
- 这是与 Cards 层的**名称级缝合点**：Cards 管理器用同名查找锚点根；Migrator 保证这些节点落在正确面板 ContentAttach 下。无 `using NineGrid.Cards`。

---

## Tests（`LivingUI/Tests/Editor/`）

### `LivingUiContentPolicyTests.cs`

- 覆盖：`LivingUiContentNames`、FromCenter 投影锚点稳定性、Scale、Policy 阶段/运动模式、envelope 聚合等（EditMode / NUnit）。

### `LivingUiTransitionPlannerTests.cs`

- 覆盖：终态命中、generation 丢弃旧计划、中途重定向保速、尺寸地板、出画不变尺寸、卡农重叠等。

---

## 与 Flow / Cards 耦合点汇总

| 位置 | 形式 | 说明 |
| --- | --- | --- |
| 全 LivingUI `using` | 无 | 无 Flow/Cards 命名空间。 |
| `LivingUiLayoutId` | 语义枚举 | 阶段名与游戏流程概念对齐，无事件总线/状态机调用。 |
| `LivingUiAuthorityStageMigrator` 元素表 | 场景物体名 | 与 Cards 锚点查找名一致（CardHand/Deck/Ground/Relic…）。 |
| `CarrierAnchorRegistry` | 扩展缝 | 设计上供外部取位；本层无实现侧引用。 |
| Demo 输入 | 本地测试 | `Commit`/`Preview` 不接 Flow 状态。 |
| 其它程序集 asmdef | 无引用 | Flow/Cards 未引用 LivingUI；无法在编译期直接调 `LivingUiDirector`（除非改 asmdef 或走默认程序集反射——源码中未见）。 |

---

## 完整 `.cs` 路由清单（LivingUI）

| # | 路由 |
| --- | --- |
| 1 | `Assets/Scripts/UI/LivingUI/Core/LivingUiContracts.cs` |
| 2 | `Assets/Scripts/UI/LivingUI/Core/LivingUiContentContracts.cs` |
| 3 | `Assets/Scripts/UI/LivingUI/Core/LivingUiContentNames.cs` |
| 4 | `Assets/Scripts/UI/LivingUI/Core/LivingUiContentProjector.cs` |
| 5 | `Assets/Scripts/UI/LivingUI/Core/LivingUiTransitionPlanner.cs` |
| 6 | `Assets/Scripts/UI/LivingUI/Core/LivingUiTransitionPlayer.cs` |
| 7 | `Assets/Scripts/UI/LivingUI/Core/LivingUiEasing.cs` |
| 8 | `Assets/Scripts/UI/LivingUI/Core/QuinticMotion.cs` |
| 9 | `Assets/Scripts/UI/LivingUI/Unity/LivingUiDirector.cs` |
| 10 | `Assets/Scripts/UI/LivingUI/Unity/LivingUiSceneLayoutSource.cs` |
| 11 | `Assets/Scripts/UI/LivingUI/Unity/CarrierView.cs` |
| 12 | `Assets/Scripts/UI/LivingUI/Unity/CarrierAnchorRegistry.cs` |
| 13 | `Assets/Scripts/UI/LivingUI/Unity/LivingUiContentMarker.cs` |
| 14 | `Assets/Scripts/UI/LivingUI/Unity/LivingUiContentController.cs` |
| 15 | `Assets/Scripts/UI/LivingUI/Unity/LivingUiContentDriver.cs` |
| 16 | `Assets/Scripts/UI/LivingUI/Unity/LivingUiBlueprintPoseSampler.cs` |
| 17 | `Assets/Scripts/UI/LivingUI/Unity/LivingUiStableHitZoneRoot.cs` |
| 18 | `Assets/Scripts/UI/LivingUI/Demo/LivingUiDemoInput.cs` |
| 19 | `Assets/Scripts/UI/LivingUI/Demo/LivingUiMainMenuHover.cs` |
| 20 | `Assets/Scripts/UI/LivingUI/Editor/LivingUiFeelWindow.cs` |
| 21 | `Assets/Scripts/UI/LivingUI/Editor/LivingUiPrimordialPoseInit.cs` |
| 22 | `Assets/Scripts/UI/LivingUI/Editor/LivingUiLiveContentPoseResync.cs` |
| 23 | `Assets/Scripts/UI/LivingUI/Editor/LivingUiMappingFixBatch.cs` |
| 24 | `Assets/Scripts/UI/LivingUI/Editor/LivingUiContentBindMigrator.cs` |
| 25 | `Assets/Scripts/UI/LivingUI/Editor/LivingUiContentAnchorMigrator.cs` |
| 26 | `Assets/Scripts/UI/LivingUI/Editor/LivingUiAuthorityStageMigrator.cs` |
| 27 | `Assets/Scripts/UI/LivingUI/Tests/Editor/LivingUiContentPolicyTests.cs` |
| 28 | `Assets/Scripts/UI/LivingUI/Tests/Editor/LivingUiTransitionPlannerTests.cs` |

asmdef 路由：

| 路由 |
| --- |
| `Assets/Scripts/UI/LivingUI/NineGrid.LivingUI.asmdef` |
| `Assets/Scripts/UI/LivingUI/Editor/NineGrid.LivingUI.Editor.asmdef` |
| `Assets/Scripts/UI/LivingUI/Tests/Editor/NineGrid.LivingUI.Tests.asmdef` |
