# DevTest Editor 工具

> 范围：`Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/Editor/**`  
> 程序集：`NineGrid.DevTest.Editor`（Editor only，引用 `NineGrid.DevTest`）

## 菜单清单

### `NineGrid/DevTest/*`（`TestKeyDevTestAssetMenu`）

根目录常量：`Assets/Resources/DevTest`；栈路径：`Assets/Resources/DevTest/TestKeyStack.asset`。

| 菜单项 | 方法 | 行为 |
|---|---|---|
| Register Selected Layer As Highest Priority | `RegisterSelectedLayerAsHighestPriority` | 要求选中 `TestKeyLayerProfileSO`；写入栈末项；选中栈资产 |
| Create Damage Number Manager Test Layer | `CreateDamageNumberManagerLayer` | 创建/更新 `Layer_DamageNumberManager.asset`（`damage-number-manager`，无声明键）并置顶 |
| Create In Battle Manager Test Layer | `CreateInBattleManagerLayer` | `Layer_InBattleManager.asset`；声明 Keypad1–6（菜单侧无 Keypad7；运行时 DevKeys 另有 Keypad7） |
| Create Selector Manager Test Layer | `CreateSelectorManagerLayer` | `Layer_SelectorManager.asset`；Keypad9 |
| Create Main Game Loop Test Layer | `CreateMainGameLoopLayer` | `Layer_MainGameLoop.asset`；Keypad0 |
| Create Ground Field Manager Test Layer | `CreateGroundFieldManagerLayer` | `Layer_GroundFieldManager.asset`；Keypad1/4/5/6/7/8 |
| Create Card Hand Manager Test Layer | `CreateCardHandManagerLayer` | `Layer_CardHandManager.asset`；Keypad7 |
| Create Default Test Key Stack Assets | `CreateDefaultAssets` | 创建牌组层 + 标准卡层；栈序 `[standard-card, card-deck-manager]`（后者更高优先）；对话框提示场景加 `TestKeyStackHost` |

公共 API：

| API | 行为 |
|---|---|
| `RegisterLayerAsHighestPriority(profile)` | 确保文件夹；无栈则创建；`stack.RegisterLayerAsHighestPriority` |
| `CreateOrUpdateLayerAsHighestPriority(path, id, name, bindings)` | `LoadOrCreateLayer` + 置顶 |

### CreateAssetMenu（运行时 SO，编辑器可见）

| 菜单 | 类型 | 默认文件名 |
|---|---|---|
| `NineGrid/DevTest/Test Key Stack` | `TestKeyStackConfigSO` | `TestKeyStack` |
| `NineGrid/DevTest/Test Key Layer` | `TestKeyLayerProfileSO` | `TestKeyLayer` |

### `Window/NineGrid/*`

| 菜单项 | 窗口类型 | 标题 |
|---|---|---|
| Test Runner | `TestKeyRunnerWindow` | Test Runner |
| Test Key Monitor | `TestKeyMonitorWindow` | Test Key Monitor |

## 窗口

### `TestKeyRunnerWindow` — 测试运行器

- UI Toolkit + `TestKeyRunnerWarmConsoleUi` 暖棕主题。
- 数据：`TestKeyCatalogProvider.GetCatalogEntries()`（SO 声明 ∪ Play 时运行时挂载）。
- 侧栏：全部测试 / 按层导航；搜索过滤 label、layerId、displayName、Key。
- 工具栏：刷新；打开键位监视器。
- 动作行「运行」→ `TestKeyRunnerPlayModeLauncher.RequestRun(layerId, key)`。
- 说明文案：点击不受小键盘级联限制（走 `TryInvokeRegisteredAction`）。

### `TestKeyMonitorWindow` — 键位监视器

- IMGUI；订阅 `TestKeyManager.Changed`。
- 显示：级联栈（标出末项「栈顶/最高优先级」）；每层声明/生效/溢出；「写入 SO 并置顶」→ `PromoteLayerToTop`。
- 「当前生效表」：`ActiveBindings` 键 → layerId + label。
- 非 Play Mode 提示进入后才有实时归属。

### `TestKeyRunnerPlayModeLauncher`（无窗口，`[InitializeOnLoad]`）

| 步骤 | 行为 |
|---|---|
| `RequestRun` | 记录 pending layerId+key |
| 已在 Play | `delayCall` 尝试派发 |
| 未在 Play | `EditorApplication.EnterPlaymode` |
| EnteredPlayMode | 最多 120 次 delayCall 轮询 `TryInvokeRegisteredAction` |
| 成功 | Log `[TestRunner] 已执行测试：…` |
| 超时 | Warning 提示检查场景 `*DevKeys` |
| ExitingPlayMode | 清空 pending |

## Inspector

### `TestKeyStackConfigSOEditor`

- `ReorderableList` 编辑 `layers`。
- 头注：上→低优先级，下→高优先级。
- HelpBox：级联溢出说明 + Monitor 窗口路径。
- 按钮「将选中层置顶（移到底部）」：要求 Project 选中 `TestKeyLayerProfileSO`，调用 `PromoteLayer`。

## 分类 / 目录支持

### `TestKeyCatalogProvider`

1. `Resources.Load("DevTest/TestKeyStack")` 的层列表。
2. `AssetDatabase.FindAssets("t:TestKeyLayerProfileSO", "Assets/Resources/DevTest")`。
3. Play Mode：`GetAllRegisteredActions` 合并 `HasLiveCallback` / `IsKeypadActive`。
4. 排序：displayName → label → key。

### `TestKeyCatalogEntry`

字段：`LayerId`、`LayerDisplayName`、`Key`、`Label`、`HasLiveCallback`、`IsKeypadActive`。

### `TestKeyRunnerWarmConsoleUi`

纯 UI 工厂：Theme 色板、Header/Toolbar/侧栏导航、统计卡、Section/ActionRow、Status HelpBox。无业务逻辑。

## Editor 源码路由表

| 文件 | 角色 |
|---|---|
| `TestKeyDevTestAssetMenu.cs` | 资产菜单与默认栈生成 |
| `TestKeyStackConfigSOEditor.cs` | 栈 SO 自定义 Inspector |
| `TestKeyMonitorWindow.cs` | 级联监视窗口 |
| `TestKeyRunnerWindow.cs` | 可点击测试运行器 |
| `TestKeyRunnerPlayModeLauncher.cs` | Play Mode 延迟派发 |
| `TestKeyCatalogProvider.cs` | 测试目录聚合 |
| `TestKeyCatalogEntry.cs` | 目录项 |
| `Ui/TestKeyRunnerWarmConsoleUi.cs` | Runner UI 样式 |

## 与运行时的边界

| 能力 | Editor | 运行时小键盘 |
|---|---|---|
| 改 SO 栈序 | 菜单 / Inspector / Monitor「置顶」 | 只读 SO（`PromoteLayerToTop` 仅 `#if UNITY_EDITOR`） |
| 触发动作 | Runner 旁路 Invoke | `PollInput` 仅生效表 |
| 目录来源 | Layer SO `declaredBindings` + 运行时 | 仅已挂载 DevKeys 回调 |
