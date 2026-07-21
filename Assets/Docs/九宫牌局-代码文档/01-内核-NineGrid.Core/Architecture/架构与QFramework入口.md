# Architecture — 架构与 QFramework 入口

源码：`Architecture/NineGridArchitecture.cs`

## 1. 类型

| 相对路径 | 类型名 | 一句话职责 | 关键依赖 |
| --- | --- | --- | --- |
| `Architecture/NineGridArchitecture.cs` | `NineGridArchitecture` | QFramework `Architecture<T>` 单例入口；`Init` 注册全部 Utility/Model/System | `QFramework`、全部 `I*System`/`AbstractModel`/`IUtility` |

命名空间：`NineGrid.Core`（根命名空间，非 `NineGrid.Core.Architecture`）。

## 2. 如何挂接

```csharp
// 伪签名（代码事实）
public sealed class NineGridArchitecture : Architecture<NineGridArchitecture>
{
    protected override void Init();
    public static IArchitecture Current { get; }  // → Interface
    public static void ResetForTests();           // Deinit 若已创建
}
```

- 首次访问 `NineGridArchitecture.Interface` / `Current` 时，QFramework 创建实例并调用 `Init()`。
- `ResetForTests()`：若 `mArchitecture != null` 则 `Deinit()`，供 EditMode 测试隔离。

### `Init()` 注册顺序（代码顺序）

**Utility**

1. `IRngUtility` → `DeterministicRngUtility`
2. `ILogUtility` → `InMemoryLogUtility`
3. `IConfigUtility` → `InMemoryConfigUtility`

**Model**（`RegisterModel`，非接口）

1. `CardRegistry`
2. `BoardModel`
3. `DeckModel`
4. `PlayerModel`
5. `RunModel`
6. `BattleContextModel`
7. `PendingChoiceModel`

**System**（接口 → 实现）

| 接口 | 实现 |
| --- | --- |
| `IStatSystem` | `StatSystem` |
| `IBattleScopeSystem` | `BattleScopeSystem` |
| `ITriggerSystem` | `TriggerSystem` |
| `IContentSystem` | `ContentSystem` |
| `IEffectSystem` | `EffectSystem` |
| `IEconomySystem` | `EconomySystem` |
| `IRewardSystem` | `RewardSystem` |
| `IPresentationSyncSystem` | `PresentationSyncSystem` |
| `IActionPipelineSystem` | `ActionPipelineSystem` |
| `IBoardSystem` | `BoardSystem` |
| `IDeckSystem` | `DeckSystem` |
| `IPhaseSystem` | `PhaseSystem` |

顺序含义（事实）：`EconomySystem`/`RewardSystem` 的 `OnInit` 会向 `ITriggerSystem` 注册系统级反应，故 Trigger 必须先于二者注册；`EffectSystem.OnInit` 会 `DiscoverLoadedAssemblies` 扫描原子。

**未在 Architecture 注册的类型：**

- `CoreCommandDispatcher` — 普通类，由外部 `new`，持有 `IArchitecture`
- `InitialGameFactory` — 静态工厂
- Domain `GameAction` 子类 — 由管线执行，非 System

## 3. 黑盒对外表面

消费方典型路径：

1. 确保 Architecture 已创建（访问 `Current`）
2. 向 `IConfigUtility` 写入 `GameContentCatalog`（键 `ContentConfigKeys.DefaultCatalog`）
3. `InitialGameFactory.Create(architecture, options)`
4. `new CoreCommandDispatcher(architecture).Send(command)`
5. 订阅 `Evt_PresentationBatchOpened` 等，播放后 `PresentationFinishedCommand`
6. 读状态：`GetModel` / `SendQuery(new EffectiveStatQuery(...))` / `CoreViewSnapshotFactory.Capture`

测试：`NineGridArchitecture.ResetForTests()` → 再访问 `Current` 重新 `Init`。

## 4. 公开 / 内部

| 类别 | 内容 |
| --- | --- |
| 公开 | `NineGridArchitecture`、`Current`、`ResetForTests`、`Init` 注册表本身 |
| 内部 | QFramework 基类字段 `mArchitecture`；各 System 的 `OnInit` 副作用 |

## 5. 文件清单

```
Architecture/NineGridArchitecture.cs
```
