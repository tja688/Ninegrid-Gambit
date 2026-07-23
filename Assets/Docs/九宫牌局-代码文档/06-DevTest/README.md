# DevTest 总览

> 本文档仅从源码 `Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/**` 与 `NineGrid.DevTest.Tests/**` 归纳，辅以代码引用的 `Assets/Resources/DevTest/**` 资产路径。

## 一句话

DevTest 是 Editor / Development Build 下的开发测试子系统：以 **TestKey 级联栈** 把小键盘（及少量其它键）分给多层 `*DevKeys` 模块，并由 Editor 窗口可点击执行同一套动作。

## 编译门闩

几乎全部运行时 API 包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 中。

| 例外（无宏门闩，始终编译） | 说明 |
|---|---|
| `Flow/QuickTestEntryInputHandler.cs` | 主菜单 `\` 长按选关 |
| `Flow/InBattleDebugQuickModeInputHandler.cs` | 局内 `\` 长按调速 |

`DevTestCompileGate.IsEnabled` 恒为 `true`，仅作程序集存在性标记。

## asmdef

| 程序集 | 路径 | 平台 | 引用 |
|---|---|---|---|
| `NineGrid.DevTest` | `NineGrid.DevTest/NineGrid.DevTest.asmdef` | 全平台 | `DamageNumbersPro`, `NineGrid.Cards`, `NineGrid.Core`, `NineGrid.Flow`, `QFramework`, `UniTask` |
| `NineGrid.DevTest.Editor` | `NineGrid.DevTest/Editor/NineGrid.DevTest.Editor.asmdef` | Editor only | `NineGrid.DevTest` |
| `NineGrid.DevTest.Tests` | `NineGrid.DevTest.Tests/NineGrid.DevTest.Tests.asmdef` | Editor only | `NineGrid.DevTest`, TestRunner；`nunit.framework.dll`；`autoReferenced: false` |

## 架构（从代码）

```
BeforeSceneLoad
  TestKeyInputPoller.BootstrapStack
    → Resources.Load("DevTest/TestKeyStack") → TestKeyManager.SetStack

AfterSceneLoad
  TestKeyInputPoller.EnsureInstance (DontDestroyOnLoad)
    → Update → TestKeyManager.PollInput → Input.GetKeyDown → 生效绑定.Invoke

场景 Awake (DefaultExecutionOrder -9999)
  TestKeyStackHost
    → 序列化 SO 或 Resources 回退 → SetStack（可覆盖 Bootstrap）

*DevKeys : TestKeyModuleBehaviour
  OnEnable  → ConfigureBindings → AttachLayer(layerId, bindings)
  OnDisable → DetachLayer(layerId)

级联 RebuildCascade
  自 StackOrder 末项向前扫描；每键首次声明者写入 ActiveBindings（末项 = 最高优先级）
```

### 优先级规则（权威）

来源：`TestKeyStackConfigSO` 注释 + `TestKeyManager.SetStack` / `RebuildCascade` / 测试 `Cascade_BottomLayerWins_OnOverlappingKeys`。

1. **列表顺序**：`layers` 从上到下优先级递增；**最末项 = 最高优先级**。
2. **同键唯一持有者**：高优先级层占用后，低优先级同键记为「溢出」，小键盘不触发。
3. **运行时挂载不改 SO 顺序**：`AttachLayer` 只填回调；已在 SO 中声明的层保持 SO 顺序。
4. **未声明动态层**：插入栈顶（索引 0）= **最低优先级**，并打 Warning。
5. **编辑器置顶**：`PromoteLayer` / `RegisterLayerAsHighestPriority` / `PromoteLayerToTop` 把层移到列表末尾。
6. **面板旁路**：`TryInvokeRegisteredAction(layerId, key)` 不受级联限制，供 Test Runner 点击执行溢出项。

### 当前 `TestKeyStack.asset` 层序

路径：`Assets/Resources/DevTest/TestKeyStack.asset`  
Resources 加载名：`DevTest/TestKeyStack`

| 索引 | 资产 | layerId | 优先级 |
|---:|---|---|---|
| 0 | `Layer_CardDeckManager.asset` | `card-deck-manager` | 最低 |
| 1 | `Layer_CardHandManager.asset` | `card-hand-manager` | |
| 2 | `Layer_StandardCard.asset` | `standard-card` | |
| 3 | `Layer_SelectorManager.asset` | `selector-manager` | |
| 4 | `Layer_MainGameLoop.asset` | `main-game-loop` | |
| 5 | `Layer_GroundFieldManager.asset` | `ground-field-manager` | |
| 6 | `Layer_DamageNumberManager.asset` | `damage-number-manager` | （运行时无绑定） |
| 7 | `Layer_GoldGainFx.asset` | `gold-gain-fx` | |
| 8 | `Layer_InBattleManager.asset` | `in-battle-manager` | **最高** |

在上述栈序且相关 DevKeys 均挂载时，小键盘冲突归属（末层胜出）：

| 键 | 生效层 | 溢出层（同键声明但未持有） |
|---|---|---|
| Keypad0 | `main-game-loop` | — |
| Keypad1–3 | `in-battle-manager` | `ground-field-manager`(仅1)、`card-deck-manager`、`standard-card` |
| Keypad4–6 | `in-battle-manager` | `ground-field-manager` |
| Keypad7 | `in-battle-manager` | `ground-field-manager`、`card-hand-manager` |
| Keypad8 | `ground-field-manager` | — |
| Keypad9 | `gold-gain-fx` | `selector-manager` |

> 实际生效以场景中已 `OnEnable` 的 DevKeys 为准；未挂载的层不参与竞争。

## 资产路径（代码推断）

| 用途 | 路径 / 菜单 |
|---|---|
| 栈 SO | `Assets/Resources/DevTest/TestKeyStack.asset` |
| 层 Profile | `Assets/Resources/DevTest/Layer_*.asset` |
| CreateAssetMenu 栈 | `NineGrid/DevTest/Test Key Stack` |
| CreateAssetMenu 层 | `NineGrid/DevTest/Test Key Layer` |
| 编辑器菜单根 | `NineGrid/DevTest/...`（见 [Editor工具.md](./Editor工具.md)） |
| 窗口 | `Window/NineGrid/Test Runner`、`Window/NineGrid/Test Key Monitor` |

## 非 TestKey 输入

| 组件 | 键 | 行为 |
|---|---|---|
| `QuickTestEntryInputHandler` | `\` 长按 ≥0.35s | 选关菜单；数字键输入码；松 `\` 确认；Esc 取消 |
| `InBattleDebugQuickModeInputHandler` | `\` 长按 ≥0.35s | 局内调速菜单；`1`/`2`（含小键盘）改 timeScale；松 `\` / Esc 关闭 |
| `DamageLogPanel` | F1 | 切换伤害日志 OnGUI 面板 |

## 源码路由表（每个 .cs 一条）

### 运行时核心 — `NineGrid.DevTest/`

| 文件 | 职责 |
|---|---|
| `DevTestCompileGate.cs` | 编译门闩标记；`IsEnabled = true` |
| `TestKeyBinding.cs` | `TestKeyBinding` / `TestKeyRegisteredAction` / `ActiveTestKeyBinding` 值类型 |
| `TestKeyBindingDefinition.cs` | SO 可序列化键位声明（key + label，无回调） |
| `TestKeyLayerProfileSO.cs` | 层 Profile：`layerId`、`displayName`、`declaredBindings` |
| `TestKeyLayerRuntimeState.cs` | 运行时层状态：显示名 + 绑定字典 |
| `TestKeyStackConfigSO.cs` | 级联栈 SO；`PromoteLayer` / `RegisterLayerAsHighestPriority` |
| `TestKeyManager.cs` | 单例：栈、挂载、级联重建、轮询、旁路 Invoke |
| `TestKeyModuleBehaviour.cs` | `TestKeyRegistrationBuilder` + DevKeys 基类 OnEnable/OnDisable |
| `TestKeyStackHost.cs` | 场景宿主；Awake 注入栈 SO |
| `TestKeyInputPoller.cs` | Bootstrap + 常驻 Update 轮询 |

### Cards — `NineGrid.DevTest/Cards/`

| 文件 | ModuleId | 目标 |
|---|---|---|
| `CardDeckManagerDevKeys.cs` | `card-deck-manager` | `CardDeckManagerSingleton`（兼用 `CardManagerSingleton` / `GroundFieldView`） |
| `CardHandManagerDevKeys.cs` | `card-hand-manager` | `CardHandManagerSingleton` + `GroundFieldView` |
| `GroundFieldManagerDevKeys.cs` | `ground-field-manager` | `GroundFieldView` + `FieldBattleView` + `CombatHitSink` |
| `StandardCardViewDevKeys.cs` | `standard-card` | 同 GO 上 `StandardCardView` |

### Flow — `NineGrid.DevTest/Flow/`

| 文件 | ModuleId / 角色 | 目标 |
|---|---|---|
| `InBattleManagerDevKeys.cs` | `in-battle-manager` | `BattleSessionController` + Trace Recorders |
| `MainGameLoopManagerDevKeys.cs` | `main-game-loop` | `GameFlowController` |
| `SelectorManagerDevKeys.cs` | `selector-manager` | `SelectorManagerSingleton` |
| `GoldGainFxManagerDevKeys.cs` | `gold-gain-fx` | `GoldGainFxManagerSingleton` |
| `DamageNumberManagerDevKeys.cs` | `damage-number-manager` | **已下线**：不注册按键 |
| `QuickTestEntryInputHandler.cs` | 非 TestKey | `GameFlowController` 快速选关 |
| `InBattleDebugQuickModeInputHandler.cs` | 非 TestKey | `GameFlowController` 局内调速 |
| `DamageLogPanel.cs` | 非 TestKey | `BattleTraceRecorder` OnGUI 伤害日志 |

### Editor — `NineGrid.DevTest/Editor/`

| 文件 | 职责 |
|---|---|
| `TestKeyDevTestAssetMenu.cs` | `NineGrid/DevTest/*` 菜单：创建层 / 默认栈 / 置顶 |
| `TestKeyStackConfigSOEditor.cs` | 栈 SO Inspector：ReorderableList + 置顶按钮 |
| `TestKeyMonitorWindow.cs` | `Window/NineGrid/Test Key Monitor` 级联监视 |
| `TestKeyRunnerWindow.cs` | `Window/NineGrid/Test Runner` 可点击测试面板 |
| `TestKeyRunnerPlayModeLauncher.cs` | 非 Play 点击 → EnterPlaymode → 延迟 Invoke |
| `TestKeyCatalogProvider.cs` | 从 Resources Layer SO + 运行时合并目录 |
| `TestKeyCatalogEntry.cs` | 目录项结构 |
| `Ui/TestKeyRunnerWarmConsoleUi.cs` | Test Runner 暖棕 UI Toolkit 样式 |

### Tests — `NineGrid.DevTest.Tests/`

| 文件 | 职责 |
|---|---|
| `TestKeyManagerTests.cs` | EditMode：级联、置顶、未声明层最低优先、溢出旁路 Invoke |

## 相关文档

- [测试键栈与模块.md](./测试键栈与模块.md) — 栈机制与各 DevKeys 键位明细
- [Editor工具.md](./Editor工具.md) — 全部菜单与窗口
