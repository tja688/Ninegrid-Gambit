# Presentation Code Map

程序集：`NineGrid.Presentation`  
根目录：`Assets/Scripts/NineGrid.Presentation/`  
命名空间：`NineGrid.Presentation.{Controllers|Commands|Queries|Events|Systems|Presentation|Views|Diagnostics|Setup|…}`

旧层边界 `NineGrid.Flow` / `NineGrid.Cards` 已废除；目录按 **角色** 平铺，不再复刻 Flow/Cards。

## 角色目录

| 目录 | 命名空间后缀 | 放什么 |
|------|--------------|--------|
| `Architecture/` | — | 占位说明（无独立类型） |
| `Controllers/` | `.Controllers` | QF `PresentationController` 场景入口 |
| `Commands/` | `.Commands` | 写意图 |
| `Queries/` | `.Queries` | 读裁决 + 门禁门面 |
| `Events/` | `.Events` | 向上广播的 struct Event |
| `Models/` | — | 预留表现 Model（可空） |
| `Systems/` | `.Systems` | QF System / 窄能力接口 |
| `Presentation/` | `.Presentation` | Director / Timeline / Channel / Scheduler / Executor / Convergence |
| `Views/` | `.Views` | MonoBehaviour 宿主、Presenter、HitProxy、卡面/特效资产 |
| `Setup/` | `.Setup` | SceneRoot / CompositionRoot / Bindings |
| `Diagnostics/` | `.Diagnostics` | Trace Recorder / Sink |
| `Editor/` `Tests/` | `.Editor` / `.Tests*` | 工具与 EditMode |

## 装配

- **场景根**：`Setup/PresentationSceneRoot`（`IController`）
- **组合根**：`Setup/PresentationCompositionRoot.Install`
- **绑定表**：`Setup/PresentationSceneBindings`

## Controllers（18）

`PresentationController` · `ExploreInputController` · `AttackInputController` · `PickupInputController` · `UseItemInputController` · `GroundFieldGeometryController` · `FieldBattlePresentationController` · `CardEntityLifecycleController` · `ZoneOwnershipQueryController` · `DescriptionOutputController` · `DamageNumberOutputController` · `RelicHudController` · `RoomChoiceInputController` · `RewardChoiceInputController` · `GameFlowShellController` · `TriggerPulseOutputController` · `DiagnosticOutputController` · `BattleSessionPresentationController`

## Systems（接口优先）

| 接口 | 用途 |
|------|------|
| `IPresentationRuntimeSystem` | Director / Timeline 生命周期 |
| `IBattleSessionSystem` | 局内会话 |
| `IGroundFieldGeometrySystem` | 场地几何锚点 |
| `IFieldBattlePresentationSystem` | 交战表现锚点 |
| `ICardEntityLifecycleSystem` | 卡实体生命周期 |
| `IGameFlowShellSystem` | 流程壳相位 |
| `IPresentationInputStateSystem` | busy / opening / external-hold 投影 |
| `IBoardSelectionSystem` | 棋盘选择模式 |
| `IChoicePresentationSystem` | 房间/奖励选择表现 |

## 读写约定

- **写** → `NineGridArchitecture.Interface.SendCommand(...)`
- **读** → `SendQuery` / `GetSystem<T>()` 只读 API
- **下→上** → `Events/` 下 struct Event
- **禁止** 新增业务静态 `*Hook` / `*ManagerSingleton`（见 `HostContractStructuralTests`）

## 效果扩展点

新增效果优先落在：

1. Command / Query / Event
2. `ITimelineStep` / `IPresentChannel`（`Presentation/`）
3. 卡面视觉 SO（`Views/Effects/`）

不要接回静态 Sink 或跨层 Hook。
