# 03 · 流程层（Flow）

> 权威来源：仅 `Assets/Scripts/Flow/**` 源码与 asmdef。不含 Docs/Notes/规划推断。

## 一句话职责

**Flow** 是 Core 与 Cards/表现之间的编排壳：主循环与局内生命周期、输入意图 → 表演时间线 → Present 通道 ACK、HUD/面板/选择器、以及四轨诊断导出。

## 与 Core / Cards / UI 的边界

| 邻层 | Flow 做什么 | Flow 不做什么 |
|------|-------------|---------------|
| **Core**（`NineGrid.Core` + QFramework） | 经 `NineGridArchitecture.Current` 读 Model/System；经 `CoreCommandDispatcher` 发命令；经 `IPresentationSyncSystem` Open/Finish 批次门 | 不实现规则引擎；不手改 `.unity`；不把剧本写进 Core |
| **Cards**（`NineGrid.Cards`） | 调用 `CardManager`/`Deck`/`GroundField` 做盘面 drain、命中观感、洗回牌堆；`CombatHitSink` 等桥接由 InBattle 注册 | 不做几何命中裁决（idle 合法性在 Flow 的 `BoardIntentLegality`）；不拥有主线时间线 |
| **UI / 面板** | `UiPanelRouter` 显隐壳；`Description`/`PlayerInfoHud`/`Selector`/`RoomChoice`/`BounceFan` 等 Presenter | 不承载 Core 状态真相；不替代导演 busy |

### 运行时协作事实（InBattle 装配）

1. Cards 命中空格/怪/用牌 → `InBattleManagerSingleton.TrySubmit*IntentFromCards`
2. `BoardIntentLegality` 读 Core Phase/Board 裁决 → `PresentationDirector.TrySubmitIntent`
3. `RoutingIntentScriptFactory` → Explore/Attack/UseItem 工厂编主线 Step
4. `ResolveBatchStep` 经 `PresentationSyncBatchGate` 调 Core 命令并 OpenBatch
5. 投影回调 `On*BatchProjected` → 对应 `IPresentChannel.Enqueue`
6. `PresentStep` → `channel.Begin` → Cards 异步 drain → `IsComplete` → `FinishBatch` ACK
7. `Update` 中 `_presentationDirector.Tick`；busy 同步到 `CombatHitSink.DirectorMainlineBusy`

## Asmdef

| 程序集 | 路径 | 依赖 |
|--------|------|------|
| `NineGrid.Flow` | `Assets/Scripts/Flow/NineGrid.Flow.asmdef` | DamageNumbersPro, NineGrid.Cards, NineGrid.Content, NineGrid.Core, QFramework, UniTask, Unity.TextMeshPro；预编译 DOTween.dll |
| `NineGrid.Flow.Editor` | `Assets/Scripts/Flow/Editor/NineGrid.Flow.Editor.asmdef` | NineGrid.Flow, DamageNumbersPro；平台 Editor |
| `NineGrid.Flow.Tests` | `Assets/Scripts/Flow/Tests/Editor/NineGrid.Flow.Tests.asmdef` | NineGrid.Flow/Core/Cards/Content, QFramework, TestRunner；nunit；`autoReferenced: false` |

根命名空间：`NineGrid.Flow`；子目录用 `NineGrid.Flow.Presentation` / `Diagnostics` / `Editor`。

## 子文档

| 文档 | 内容 |
|------|------|
| [Runtime-Managers/运行时管理器.md](./Runtime-Managers/运行时管理器.md) | 根目录 Manager/Presenter/Scanner/Utility |
| [Presentation/表现时间线.md](./Presentation/表现时间线.md) | Director / Channel / Lockstep / Pulse 全类型与链路 |
| [Diagnostics/诊断与日志.md](./Diagnostics/诊断与日志.md) | 四轨 Trace 与导出 |
| [Editor/Flow编辑器工具.md](./Editor/Flow编辑器工具.md) | Editor 菜单与 LivingUi 工具 |

## 完整文件路由表

### 根目录（Runtime）

| 文件 | 类型 | 一句话 |
|------|------|--------|
| `InBattleManagerSingleton.cs` | MonoBehaviour 单例 | 局内唯一导演宿主 + Present 薄适配 + Core 桥 |
| `MainGameLoopManagerSingleton.cs` | MonoBehaviour 单例 | 主菜单→战斗→奖励→房间→胜负壳状态机 |
| `RelicManagerSingleton.cs` | MonoBehaviour 单例 | 遗物图标槽同步 `PlayerModel.RelicDefIds`（含原玩家技能） |
| `SelectorManagerSingleton.cs` | MonoBehaviour 单例 | Bounce/房间二选一会话门面 |
| `DescriptionManagerSingleton.cs` | MonoBehaviour 单例 | 卡牌/Notice 描述 TMP；接 DescriptionHoverSink |
| `DamageNumberManagerSingleton.cs` | MonoBehaviour 单例 | DamageNumbersPro 飘字 Spawn |
| `GoldGainFxManagerSingleton.cs` | MonoBehaviour 单例 | 金币飞入与 HUD 数字表演 |
| `UiPanelRouter.cs` | MonoBehaviour | 主菜单/局内/奖励/房间等面板显隐 |
| `PlayerInfoHudPresenter.cs` | MonoBehaviour | 血/攻/甲/金/名从 Core 同步 |
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
