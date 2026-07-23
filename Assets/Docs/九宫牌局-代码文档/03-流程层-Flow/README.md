# 03 · 流程层（Flow）

> 权威来源：仅 `Assets/Scripts/NineGrid.Presentation/Flow/**` 等现源码与 asmdef。  
> **弃用说明**：下文「运行时协作事实」等源码镜像段落已与 #43 后结构冲突，仅作历史对照；现行权威以 `PresentationSceneRoot` / `PresentationCompositionRoot` / 各 QF System 为准。

## 一句话职责

**Flow** 是 Core 与 Cards/表现之间的编排壳：主循环与局内生命周期、输入意图 → 表演时间线 → Present 通道 ACK、HUD/面板/选择器、以及四轨诊断导出。

## 与 Core / Cards / UI 的边界

| 邻层 | Flow 做什么 | Flow 不做什么 |
|------|-------------|---------------|
| **Core**（`NineGrid.Core` + QFramework） | 经 Architecture 读 Model/System；经命令发请求；经 `IPresentationSyncSystem` Open/Finish 批次门 | 不实现规则引擎；不把剧本写进 Core |
| **Cards** | 场景 View 提供锚点/HitProxy；几何与交战执行在 Presentation Systems | 不做几何命中裁决；不拥有主线时间线 |
| **UI / 面板** | `UiPanelRouter` 显隐壳；Description / HUD / Selector 等 Presenter | 不承载 Core 状态真相；不替代导演 busy |

### 现行装配（#43 后）

1. `PresentationSceneRoot` 持有场景宿主 SerializeField，显式 Wire Controllers/Hooks  
2. `PresentationCompositionRoot.Install` 装配唯一生产 `PresentationRuntimeSystem` + Director  
3. 局内会话权威在 `IBattleSessionSystem`；流程权威在 `IGameFlowShellSystem`  
4. 门禁只读投影在 `PresentationInputStateSystem`（无 `CombatHitSink`）

## Asmdef

| 程序集 | 路径 | 依赖 |
|--------|------|------|
| `NineGrid.Presentation` | `Assets/Scripts/NineGrid.Presentation/NineGrid.Presentation.asmdef` | 见 asmdef |
| `NineGrid.Presentation.Tests` | `Assets/Scripts/NineGrid.Presentation/Tests/**` | 见测试 asmdef |

根命名空间：`NineGrid.Flow` / `NineGrid.Presentation.*`；子目录用 `NineGrid.Flow.Presentation` / `Diagnostics`。

## 子文档

| 文档 | 内容 |
|------|------|
| [Runtime-Managers/运行时管理器.md](./Runtime-Managers/运行时管理器.md) | 根目录 Manager/Presenter（部分镜像过期） |
| [Presentation/表现时间线.md](./Presentation/表现时间线.md) | Director / Channel / Lockstep / Pulse |
| [Diagnostics/诊断与日志.md](./Diagnostics/诊断与日志.md) | 四轨 Trace 与导出 |
| [Editor/Flow编辑器工具.md](./Editor/Flow编辑器工具.md) | Editor 菜单与 LivingUi 工具 |

## 完整文件路由表（轻量 Code Map）

### 根目录（Runtime 壳）

| 文件 | 类型 | 一句话 |
|------|------|--------|
| `BattleSessionController.cs` | MonoBehaviour 场景壳 | 局内会话 View / 生命周期；业务在 BattleSessionSystem |
| `GameFlowController.cs` | MonoBehaviour 场景壳 | 主菜单输入 + Notice/Panel 投影；权威在 GameFlowShellSystem |
| `RelicManagerSingleton.cs` | MonoBehaviour | 遗物图标槽同步 |
| `SelectorManagerSingleton.cs` | MonoBehaviour | Bounce/房间二选一会话门面 |
| `DescriptionManagerSingleton.cs` | MonoBehaviour | 卡牌/Notice 描述 TMP |
| `DamageNumberManagerSingleton.cs` | MonoBehaviour | DamageNumbersPro 飘字 Spawn |
| `GoldGainFxManagerSingleton.cs` | MonoBehaviour | 金币飞入与 HUD 数字表演 |
| `UiPanelRouter.cs` | MonoBehaviour | 主菜单/局内/奖励/房间等面板显隐 |
| `PlayerInfoHudPresenter.cs` | MonoBehaviour | 玩家信息血槽/护甲/金币从 Core 同步 |
| `BounceFanChoicePresenter.cs` | MonoBehaviour | Bounce 扇形多选一卡表演 |
| `RoomChoicePresenter.cs` | MonoBehaviour | 房间左右二选一进出场 |
| `BoardPresentationStepProjector.cs` | static | EventLog → BoardPresentationStep / Legacy Moves |
| `CoreCardPresentationMapper.cs` | static | Core 卡属性 → ManagedCard 表现 |
| `ContentIconSlotBinder.cs` | internal static | 遗物/技能槽 Transform 与 Sprite 装配 |
| `ContentIconSlotHitProxy.cs` | MonoBehaviour | 槽位 hover 命中代理（写 defId） |
| `HelpCardBoardSelectResolver.cs` | static | 帮助卡选目标数量/提示（读 Content） |
| `ShuffleIntoDeckPresentationScanner.cs` | static | EventLog 扫洗回牌堆条目 |
| `SkeletonFusionPresentationScanner.cs` | static | EventLog 扫骷髅融合条目 |
| `QuickTestDeckCatalog.cs` | static | DevTest 选关码→牌组 |
| `QuickTestRunOptions.cs` | 类型 | 快速测试选项 DTO |
| `QuickTestRunPlanner.cs` | internal static | 快速测试节点队列规划 |
| `WorldPointerUtility.cs` | static | 世界点选 / Collider2D |
| `UITestBootstrap.cs` | MonoBehaviour | 面板切换测试键入口 |
| `IUITestKeyConsumer.cs` | interface | UITestBootstrap 可选消费者 |
| `NineGrid.Flow.asmdef` | asmdef | 运行时程序集定义 |

### Presentation/

见 [表现时间线.md](./Presentation/表现时间线.md) 路由表（38 个 `.cs`）。

### Diagnostics/

见 [诊断与日志.md](./Diagnostics/诊断与日志.md) 路由表（20 个 `.cs`）。

### Editor/

见 [Flow编辑器工具.md](./Editor/Flow编辑器工具.md) 路由表（7 个 `.cs`）。

### Tests/Editor/（仅清单）

| 文件 |
|------|
| `AttackVerticalSliceTests.cs` |
| `BoardIntentLegalityTests.cs` |
| `DrainVerticalSliceTests.cs` |
| `ExploreVerticalSliceTests.cs` |
| `FusionVerticalSliceTests.cs` |
| `OccupancyForceSyncGuardTests.cs` |
| `PresentationDirectorTests.cs` |
| `ShuffleBurstGrouperTests.cs` |
| `ShuffleVerticalSliceTests.cs` |
| `TauntRedirectPresentTargetingTests.cs` |
| `TriggerPulseAndDiagnosticsTests.cs` |
| `UseItemVerticalSliceTests.cs` |
| `NineGrid.Flow.Tests.asmdef` |

测试细节留给测试代理。

## 命名空间速查

- `NineGrid.Flow` — 根目录 Manager/Presenter/Scanner
- `NineGrid.Flow.Presentation` — 导演与时间线
- `NineGrid.Flow.Diagnostics` — Trace
- `NineGrid.Flow.Editor` / `NineGrid.Flow.Editor.LivingUi` — 编辑器工具
- `NineGrid.Flow.Tests` — EditMode 测试
