# 指针命中、HUD 与检视 —— PointerHitRouter · 玩家信息 HUD · 遗物栏 · 卡牌详述 · 战前预览

> 权威代码：根下指针/输入/面板 13 个、HUD/遗物/检视 14 个、`Flow/BattleInfoPreview/`（6），共 33 个文件。
> 关联 ADR：ADR-0006（轮询式指针命中）、ADR-0023（命中面身份仲裁）、ADR-0027（遗物栏命中与拖拽）、ADR-0028（有效护甲）、ADR-0035（倒计时静态投影）、ADR-0037（内联图标术语悬停）

## 职责综述

三块相对独立的表现基建：

- **指针命中仲裁**：替代 Unity `OnMouse*` 的轮询体系——所有可点对象实现 `IPointerHitTarget` 注册进 `PointerHitRegistry`，`PointerHitRouter` 每帧按 sort→type 双键仲裁出唯一胜者并合成 Enter/Exit/Down；`WorldPointerUtility` 是世界坐标指针唯一读口（New Input System）。
- **局内 HUD**：玩家血/甲/金（`PlayerInfoHudPresenter` + 金币时间窗家族）、伤害飘字（`DamageNumberManagerSingleton`）、遗物栏（`RelicManagerSingleton` 家族 + 倒计时投影）。
- **检视与预览**：右键卡牌详述覆层（`CardInspectOverlayPresenter`）、战前信息预览硬阻塞面板（`BattleInfoPreview/`）、半黑屏遮罩与 UI 面板路由。

## 关键类型表

### 指针与输入

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `IPointerHitTarget` | `IPointerHitTarget.cs` | 轮询命中目标契约：HitCollider + sort/type 双键 + Enter/Exit/Down 回调 |
| `IMultiColliderPointerHitTarget` | `IMultiColliderPointerHitTarget.cs` | 多 Collider/自定义表面（场地面九格）：TryOverlapScreenPoint + 跨格悬停刷新 |
| `PointerHitRegistry`（静态类） | `PointerHitRegistry.cs` | 命中目标静态登记表（去重 + 清理 Unity 假 null） |
| `PointerHitRouter` | `PointerHitRouter.cs` | 每帧轮询仲裁：sort→type 决胜（同分 LogError）；右键详述/遗物检视、左键拖拽分发 |
| `PointerHitSurfacePriorities`（静态类） | `PointerHitSurfacePriorities.cs` | 表面类型优先级常量：Field=28 > Hand=20 > Overlay=10 |
| `PointerHitSurfacePriorityValidator`（静态类） | `PointerHitSurfacePriorityValidator.cs` | 装配期同分冲突扫描（同分即装配错误） |
| `WorldPointerUtility`（静态类） | `WorldPointerUtility.cs` | 世界指针唯一读口：屏幕/世界/平面坐标 + 键位边沿；可注入 IPointerSource |
| `KeyboardUtility`（静态类） | `KeyboardUtility.cs` | KeyCode → Input System Key 映射读口（Dev 快捷键用） |
| `UiOverlayHitProxy` + `UiOverlayHitAction` | `UiOverlayHitProxy.cs` | UI 叠层命中代理：Swallow / 关详述 / 半黑屏背景 / 关功能菜单 |
| `UiPanelRouter` | `UiPanelRouter.cs` | 主菜单/局内壳/叠层面板互斥路由（支持 inactive 层级查找） |
| `BattleUiDimmerOverlay` | `BattleUiDimmerOverlay.cs` | 局内半黑屏遮罩（引用计数 Acquire/Release），挂 `UI面板/半黑屏BG` |
| `IUITestKeyConsumer` | `IUITestKeyConsumer.cs` | UITest 按键消费者契约（避免 Flow 反向引用测试程序集） |
| `UITestBootstrap` | `UITestBootstrap.cs` | 临时 UI 测试引导（仅 UITestScene，禁用正常流程） |

### HUD、遗物与检视

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `PlayerInfoHudPresenter` | `PlayerInfoHudPresenter.cs` | 玩家血/甲/金 HUD：血槽随 MaxHp 伸长、悬停切换显示、金币增益时间窗 |
| `DamageNumberManagerSingleton` | `DamageNumberManagerSingleton.cs` | 伤害飘字统一管理（DamageNumbersPro）：血/甲拆分色、动态缩放 |
| `GoldGainPresentationBinder` | `GoldGainPresentationBinder.cs` | 消费 `GoldGainPresentationRequested`：花费 Snap / 增益飞币 + 数字窗 |
| `GoldHudDomainHost` + `IGoldHudDomainHost` | `GoldHudDomainHost.cs` | 金币 HUD VFX 域宿主（#203）：sink 坐标、coin sprite、吞币 Punch |
| `GoldHudNumberWindow`（静态类） | `GoldHudNumberWindow.cs` | 金币数字时间窗纯数学（#199）：首达前持旧值、首末达间 EaseOutQuad 插值 |
| `RelicManagerSingleton` | `RelicManagerSingleton.cs` | 遗物栏表现单例：SyncFromCore 刷图标、倒计时 Commit、拖拽回收 |
| `RelicHudHook`（静态类） | `RelicHudHook.cs` | 遗物栏静态 Hook 面：同步/丢弃/拖拽/检视/倒计时 Commit（Controller 接线） |
| `RelicIconSlotView` | `RelicIconSlotView.cs` | 遗物栏单槽视图：锚点 HitProxy + 子树标准图标模板 + 计数 TMP |
| `RelicCountdownProjection`（静态类） | `RelicCountdownProjection.cs` | 遗物倒计时投影薄封装（委托 EffectCountdownProjection） |
| `EffectCountdownProjection`（静态类） | `EffectCountdownProjection.cs` | 从卡 JSON 装配解析倒计时投影键 + period（ADR-0035） |
| `CardInspectOverlayPresenter` | `CardInspectOverlayPresenter.cs` | 右键卡牌详述覆层：真卡面 + 术语表 + 图标悬停 + 半黑屏 |
| `CardInspectIconHover` | `CardInspectIconHover.cs` | 详述图标悬停解析：Basic_Description 内联 sprite + 卡面机制图标（ADR-0037） |
| `ContentIconSlotBinder`（静态类） | `ContentIconSlotBinder.cs` | content defId 列表刷到锚点槽 SpriteRenderer（遗物/技能图标） |
| `ContentIconSlotHitProxy` | `ContentIconSlotHitProxy.cs` | 遗物/技能图标槽命中代理（TypePriority=25，拖/检视由 Router 特判） |

### 战斗信息预览

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `BattleInfoPreviewPresenter` | `BattleInfoPreview/BattleInfoPreviewPresenter.cs` | 战前信息预览主控：`ShowAndWaitAsync` 硬阻塞至玩家关闭 |
| `BattleInfoPreviewSlotView` | `BattleInfoPreview/BattleInfoPreviewSlotView.cs` | 单预览槽（`__Art` 图标层 + 高亮） |
| `BattleInfoPreviewIconPlayer` | `BattleInfoPreview/BattleInfoPreviewIconPlayer.cs` | 预览槽图标播放：静态 mainIcon / Idle 多帧轮播 |
| `BattleInfoPreviewHighlight` | `BattleInfoPreview/BattleInfoPreviewHighlight.cs` | 悬停九宫四角框高亮（跟随图标包围盒） |
| `BattleInfoSlotArtFit`（静态类） | `BattleInfoPreview/BattleInfoSlotArtFit.cs` | 槽图标摆放数学（复用卡面 mainVisual 缩放偏移，BottomCenter 锚定） |
| `BattleInfoPreviewCopySO` | `BattleInfoPreview/BattleInfoPreviewCopySO.cs` | 房间信息文案模板 SO（floor/displayNode 四级通配回退） |

## 核心流程

### 1. 指针命中仲裁链（ADR-0006/0023）

```text
各 HitProxy / FieldSurface  OnEnable → PointerHitRegistry.Register
PointerHitRouter（RuntimeInitializeOnLoad 自举，DontDestroyOnLoad）每帧 Tick：
  1. WorldPointerUtility 取屏幕坐标；EventSystem.IsPointerOverGameObject → 让位 uGUI 并 ClearHover
  2. 遍历 Registry：多 collider 走 IMultiColliderPointerHitTarget.TryOverlapScreenPoint，
     单 collider 按其平面 Z Overlap
  3. 仲裁：HitSortOrder 高者胜 → 平局比 HitTypePriority（Field 28 > ContentIcon 25 > Hand 20 > Overlay 10）
     → 仍同分 = 装配错误 LogError（Validator 可装配期预检）
  4. hover 变化派发 Enter/Exit；同表面跨格调 RefreshPointerHover（Router 只认表面身份，跨格不重入）
  5. 右键：优先 RelicHudHook.TryInspectRelic → 否则 TryOpenCardInspect（场卡经 GroundFieldHitSurface 取 ManagedCard）
     左键：半黑屏/详述/预览开时特殊 Down（关详述不 Dismiss 战斗预览）→ 遗物拖 → 手牌拖 → HandlePointerDown
```

要点：落格对象已去 collider，场地面（`GroundFieldHitSurface`，Cards 区）是棋盘唯一命中面；手牌 hover 带优先于 Overlap；半黑屏期间覆层不靠排序抢几何。

### 2. 金币增益时间窗（#199/#203）

`GoldGainPresentationRequested` → `GoldGainPresentationBinder`：花费直接 `SnapGold`；增益先 `TriggerPulseHub.PulseVfx(economy.gold_flight)` 拿 `VfxPresentationPlan`（首达/末达延迟）→ 有效则 `PlayerInfoHudPresenter.PresentGoldGainWindow` 开协程，每帧 `GoldHudNumberWindow.SampleDisplayed`（首达前持旧值、首末达间 EaseOutQuad、末达精确终值）；并行批次合并抬目标、扩展末达；VFX 失败立即 Snap 收敛，不占主线 ack。飞币途中 `GoldHudDomainHost` 提供 sink 坐标/排序/coin sprite，每吞一枚 `PunchIcon`。

### 3. 遗物栏与倒计时投影（ADR-0027/0035）

- 同步：`RelicHudSyncRequestedEvent` → `RelicHudController`（Systems/）→ `RelicHudHook` → `RelicManagerSingleton.SyncFromCore/ApplyDefIds` 刷 `RelicIconSlotView`（锚点留 `ContentIconSlotHitProxy`，显示在子树标准模板——命中与显示分离，ADR-0027）。
- 显示壳 prefab：`Assets/Resources/Prefabs/标准遗物图标模板.prefab`（`CardChassisPaths.RelicHudIconPrefab`）——**必须在 Resources 下**：Player 端 `LoadGameObject` 只认含 `/Resources/` 的路径（ADR-0008），否则打包后遗物栏静默全空（2026-08 修复：原在 `Assets/Prefabs/` 下导致仅编辑器可见）；加载失败现会打 Error 日志。
- 倒计时：JSON `effectAssemblies` 经 `EffectCountdownProjection` 解析 `projectKey`+period（≤1 跳过）→ 栏位显示首个 period>1 键；Settled 后经 `RelicHudHook.CommitCountdownRemaining` 提交剩余值刷计数 TMP，未提交显示装配初值——**禁止 View 直读 Core 计数器**。
- 交互（Router 特判）：左键 `TryBeginDragUnderPointer`（ghost + 回收区，拖动中栏位隐藏不改布局）；右键 `TryInspectRelic` 开详述。

### 4. 卡牌详述覆层（CardInspectOverlayPresenter）

右键触发 `TryOpen/TryOpenByDefId`：挂真卡面 `__InspectLiveFace` + `CardFacePresentationBinder`（敌方/常规两态）→ Acquire 半黑屏 → 术语表（`CardGlossaryTerms` + `CardInspectDetailComposer`）→ `CardInspectIconHover` 解析悬停词条（ADR-0037）。关闭经 `UiOverlayHitProxy`（Swallow 时详述开着只关详述，DimmerBackground 按详述→预览→功能菜单优先级链）。检视描述恒为静态 Inspect 投影（ADR-0035）；根 SortingGroup order=100 压过 BounceFan；ChoiceOverlay 下 Router 禁开。

### 4b. 预览卡面图标悬停解释（ADR-0037）

词条栏首行常驻一条可复用 hover 槽（`CardInspectGlossaryListView.hoverRow`，运行时名「词条Hover槽」），空态显示提示串 `inspect.hover_hint`。`CardInspectIconHover`（只挂预览卡面，战斗内场上卡面不挂）每帧按两级优先命中：

1. **描述内联 sprite**：`TMP_TextUtilities` 命中 `Basic_Description` 的 sprite 字符 → 从富文本源串回溯 `<sprite name="code">` 取代号 → 词条表 `TryGetByCode`。
2. **卡面机制图标**：靶由 `CardFaceIconGlossaryTargets.Collect` 在 Configure 时收一次，命中按**世界 AABB 包含指针**判定、多个相交取面积最小者 → 词条表 `TryGetByDisplayName`。

卡面图标靶与词条路由（`CardFaceIconGlossaryTargets`，Cards 区）：

| 卡面节点 | 槽来源 | 词条名 |
|---|---|---|
| 攻击 / 护甲 / 血量 | `TryFindCompanionIcon`（数值伴随图标） | 攻击 / 护甲 / 血量 |
| 行动计数 | 按节点名（**不进槽代号表**：语义随节奏源切换） | 卡 JSON `rhythmSource` 为移动 → `CardRhythmRules.TokenMove`，否则 `TokenAction` |
| 攻击模式 | `Action_Icon` | `AttackPatternRules.Token*`（普通/斜角/全向近战）；模式为「无」而有同步技能时该槽升格 → 技能同步触发 |
| 是否有技能同步触发 | `Sync_Rhythm_Icon` | 技能同步触发 |

要点：

- 图标**不长命中体**，不进 `PointerHitRegistry`——AABB 纯计算，与棋盘格位命中框、命中仲裁互不相干。
- 显隐随投影变化的槽（攻击模式 / 同步子图标）照常入靶表，命中时再按 `renderer.enabled` + `activeInHierarchy` 复核：**图标看得见才解释**。
- 词条名匹配键是 `displayNameZh`；攻击模式与节奏源直接复用 Core token 常量，代码里不另抄一份中文。
- 五套卡面实测靶数：怪物 6、机关 2、玩家 2、道具/遗物 0（后两者的机制解释走描述 `[code]` 与 `[[词条]]`）。

### 5. 战前信息预览（BattleInfoPreview）

正式 Run 进战前 `ShowAndWaitAsync(NodeDeckOptions, ct)` **硬阻塞**（[主编排篇](01-GameFlow主编排与流程壳.md) §3）：Acquire 半黑屏 → 填槽（敌方怪物/机关去重、玩家立绘 avatarDefId、玩家道具优先展示开局可知 defId）→ 房间文案 `BattleInfoPreviewCopySO.TryResolveTemplate`（精确 → floor 通配 → node 通配 → 全通配四级回退，占位符 `{room}{floor}{progress}…`）→ 玩家关闭 `RequestDismiss`（详述嵌套开着时拒绝，防误进战）。槽图标经 `BattleInfoPreviewIconPlayer`（Idle 多帧只换 sprite 不重算摆放）+ `BattleInfoSlotArtFit`（复用卡面 mainVisual 参数，BottomCenter 锚定）。场景根 `UI面板/战斗信息展示BG`。

## 对外通信面

- **读 Core**：`PlayerModel`（血/甲/金/遗物/固定道具）、`RunModel`（楼层/节点/房型）、`IContentSystem`（JSON 目录）、`NodeDeckOptions`（预览数据源）。
- **事件订阅**：`DamageNumberRequested`（飘字）、`GoldGainPresentationRequested`（金币）、`RelicHudSyncRequestedEvent`。
- **静态 Hook**：`RelicHudHook`（Presentation `RelicHudController` 注册实现，Flow 不反向依赖具体类型）；`CardEntityLifecycleHook.Hand`（手牌拖拽入口）。
- **战中驱动**：`PlayerInfoHudBeatHandler` / `GoldGainBeatHandler`（[排期篇](05-表演锚点排期与触发脉冲.md)）在锚点刷 HUD；`GoldHudDomainHost` 由 `EnsureBindings` 安装。
- **消费方**：`PointerHitRouter` 被全体 HitProxy 依赖；半黑屏被详述/预览/Bounce Acquire。

## 关联 ADR

ADR-0006（轮询命中替代 OnMouse）、ADR-0023（表面身份仲裁 / 任意距离点击 / z 平面 Overlap）、ADR-0027（遗物锚点命中 + 子树显示 + 左拖右检）、ADR-0028（HUD 甲=有效护甲）、ADR-0035（倒计时只消费 Settled 提交值；检视静态投影）、ADR-0037（内联图标术语悬停）。

## 不变量与坑

- **同分即装配错误**：仲裁不得静默按注册顺序决胜；新 HitProxy 上线跑一次 Validator。
- **Unity 假 null**：Registry `PruneDestroyedTargets`、HUD/Dimmer 绑定判空必须 `== null`（吞 Destroyed）；`BattleUiDimmerOverlay.TryGetLiveInstance` 清静态残留。
- 金币增益独占时间窗：窗口内其他增益合并进当前窗，不并行开窗。
- 遗物图标只显示**首个** period>1 投影键；卸下遗物 `PruneCommittedToDisplayed` 清脏值。
- hover 解释槽必须留在词条栏**首行**：列表可滚动，落末尾时词条一多就滚出视野，等于功能不存在。
- 词条行跨词条复用同一个 TMP：未着色的词条须退回模板默认色（`CardInspectGlossaryRowView` 记住首次的 `body.color`），否则 hover 槽会留着上一条的颜色。
- 预览槽右键详述已断开（待动态框选系统）；`BattleInfoPreviewSlotView` 暂不接入 `PointerHitRegistry`。
- 玩家立绘 offset/scale 须先 Capture 基准再套用，防反复打开双重叠加。
- `UITestBootstrap` 会禁用 `GameFlowController` 阻断正常流程——正式场景勿挂载。

## 本篇文件清单（33）

指针/输入/面板：`IPointerHitTarget.cs`、`IMultiColliderPointerHitTarget.cs`、`PointerHitRegistry.cs`、`PointerHitRouter.cs`、`PointerHitSurfacePriorities.cs`、`PointerHitSurfacePriorityValidator.cs`、`WorldPointerUtility.cs`、`KeyboardUtility.cs`、`UiOverlayHitProxy.cs`、`UiPanelRouter.cs`、`BattleUiDimmerOverlay.cs`、`IUITestKeyConsumer.cs`、`UITestBootstrap.cs`；HUD/遗物/检视：`PlayerInfoHudPresenter.cs`、`DamageNumberManagerSingleton.cs`、`GoldGainPresentationBinder.cs`、`GoldHudDomainHost.cs`、`GoldHudNumberWindow.cs`、`RelicManagerSingleton.cs`、`RelicHudHook.cs`、`RelicIconSlotView.cs`、`RelicCountdownProjection.cs`、`EffectCountdownProjection.cs`、`CardInspectOverlayPresenter.cs`、`CardInspectIconHover.cs`、`ContentIconSlotBinder.cs`、`ContentIconSlotHitProxy.cs`；`BattleInfoPreview/`：`BattleInfoPreviewPresenter.cs`、`BattleInfoPreviewSlotView.cs`、`BattleInfoPreviewIconPlayer.cs`、`BattleInfoPreviewHighlight.cs`、`BattleInfoSlotArtFit.cs`、`BattleInfoPreviewCopySO.cs`。
