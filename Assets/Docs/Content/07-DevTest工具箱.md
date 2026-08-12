# DevTest 工具箱（NineGrid.DevTest · 开发测试按键与作弊通道）

> 覆盖范围：`Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/` 全部 28 文件（含 `Editor/` 子程序集 3 文件）。
> 除两个运行时安装宿主（`DevTestSceneInstaller`、`DevTestStandardCardInstaller`）外，所有代码包在 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 内；`Editor/` 三件为 `#if UNITY_EDITOR`。Release Player 中本程序集只剩两个空壳安装器。

## 职责综述

DevTest 提供两套通道：

1. **TestKey 级联栈**——小键盘 DevKeys 体系：各表现模块（卡组/场地/手牌/战斗/流程…）以「层」为单位向 `TestKeyManager` 挂按键回调；同键冲突由 SO 级联栈裁决（栈底层胜出，其余层「溢出」），编辑器 Test Key Monitor 可看归属并直接点触。
2. **QuickTest 入口与作弊命令**——主菜单 `\` 长按选关（`\0`–`\9` 效果体验通道，见 `.cursor/skills/quick-test-effect-channels/`）、强制胜利/改血等 QF Command 作弊、Battle/Flow/Perf/Registry 日志导出、F1 伤害日志面板。

装配纪律（#126）：MainScene 与卡牌底盘预制体**不序列化**任何 `#if` 专属类型；`DevTestSceneInstaller` / `DevTestStandardCardInstaller` 是唯一序列化的宿主，Dev 环境下运行时 `AddComponent` 装入各 DevKeys，Release 下空壳——避免 Release Player 出现 Missing Script。

## 关键类型表

### TestKey 基础设施（根目录）

| 类型 | 文件 | 一句话职责 |
|------|------|------|
| `TestKeyManager` | `TestKeyManager.cs` | 单例管理器：层字典 + SO 栈序（列表末项最高优先级）；`RebuildCascade` 自栈底向上认领按键（同键先到先得=高优先层持有）；`PollInput` 快照轮询（回调可安全 Attach/Detach）；动态未声明层插栈顶（最低优先级）并告警；Editor 下 `PromoteLayerToTop` 写回 SO |
| `TestKeyBinding` / `TestKeyRegisteredAction` / `ActiveTestKeyBinding` | `TestKeyBinding.cs` | 三个值结构：单键绑定（标签+回调）、注册动作描述（监视器 UI 用，含溢出标记）、级联胜出条目 |
| `TestKeyBindingDefinition` | `TestKeyBindingDefinition.cs` | SO 可序列化按键声明（仅键位+标签，回调运行时挂） |
| `TestKeyLayerProfileSO` | `TestKeyLayerProfileSO.cs` | 层配置 SO：layerId/displayName/声明键位表（`NineGrid/DevTest/Test Key Layer` 菜单创建） |
| `TestKeyLayerRuntimeState` | `TestKeyLayerRuntimeState.cs` | 运行时层状态：元数据 + 实际回调表（WithBindings 拷贝隔离） |
| `TestKeyStackConfigSO` | `TestKeyStackConfigSO.cs` | 级联栈 SO：层列表（上低下高优先级）；PromoteLayer(ById) 移到底部即置顶激活 |
| `TestKeyStackHost` | `TestKeyStackHost.cs` | 场景宿主（执行序 -9999）：Awake 注入栈 SO（缺省从 `Resources/DevTest/TestKeyStack` 加载） |
| `TestKeyInputPoller` | `TestKeyInputPoller.cs` | 常驻轮询器（执行序 -10000）：`RuntimeInitializeOnLoad` 自举栈 + DontDestroyOnLoad 单例，Update 驱动 `PollInput` |
| `TestKeyModuleBehaviour`（+ `TestKeyRegistrationBuilder`） | `TestKeyModuleBehaviour.cs` | DevKeys 模块基类：OnEnable 用 Builder 声明按键挂层、OnDisable 卸层；运行时安装（无序列化 profile）时按 LayerId 从 `Resources/DevTest` 回填 profile 保持显示名一致 |
| `DevTestCompileGate` | `DevTestCompileGate.cs` | 编译门闩标记（常量 true，声明本程序集 API 仅 Dev 可用） |

### Flow/（流程与战斗 DevKeys）

| 类型 | 文件 | 一句话职责 |
|------|------|------|
| `DevTestSceneInstaller` | `Flow/DevTestSceneInstaller.cs` | MainScene 唯一序列化 DevTest 宿主（#126）：Awake 把 8 个 DevKeys + QuickTest 入口 AddComponent 到各导演宿主 |
| `QuickTestEntryInputHandler` | `Flow/QuickTestEntryInputHandler.cs` | 主菜单 `\` 键状态机：长按 0.35s 弹选关菜单 → 按 0–9（至多两位）→ 释放 `\` 确认 → `TryBeginQuickTestFromPickerCodeCommand`；Esc 取消；入口可用性由 `GameFlowDevQueries.CanAcceptQuickTestEntry` 裁决 |
| `InBattleManagerDevKeys` | `Flow/InBattleManagerDevKeys.cs` | 战斗层（Keypad1–7 + 减号）：真实局内入场、探测节点结算、玩家血量=99、导出 Battle+Flow(+Perf+Registry)Log、开关四路 Trace、强制本局胜利、BUG 现场戳点（PerfTrace StampUserObservation）、QuickTest 跳过战斗 |
| `MainGameLoopManagerDevKeys` | `Flow/MainGameLoopManagerDevKeys.cs` | 流程层（Keypad0）：进入主游戏循环测试（`BeginGameFlowRunCommand`，兜底直调 `BeginFormalRun`） |
| `SelectorManagerDevKeys` | `Flow/SelectorManagerDevKeys.cs` | 选择器层（Keypad9）：弹出 Bounce 三选一并打印选择结果 |
| `GoldGainFxManagerDevKeys` | `Flow/GoldGainFxManagerDevKeys.cs` | 金币表现层（Keypad9）：屏幕中心飞入 10 金币（`GoldGainPresentationBinder.PresentGainVisual`，走 ADR-0040 迁移后的正式链路） |
| `DamageNumberManagerDevKeys` | `Flow/DamageNumberManagerDevKeys.cs` | 已下线占位：OnEnable/OnDisable 空实现，不再注册按键（保留类型避免安装器改动） |
| `DamageLogPanel` | `Flow/DamageLogPanel.cs` | F1 开关的 IMGUI 伤害日志面板：读 `BattleTraceRecorder.CurrentSession` 逐 op 展开事件行（伤害/治疗/击杀着色），displayName/效果名经 Catalog 反查 |

### Commands/（QF 作弊命令）

| 类型 | 文件 | 一句话职责 |
|------|------|------|
| `CheatSetAvatarHpCommand` / `CheatSetAvatarAttackCommand` / `CheatForceNodeVictoryCommand` | `Commands/CheatBattleSessionCommands.cs` | 战斗作弊命令三件：委托表现层 `BattleSessionCheat.TrySet*/TryForceNodeVictory` |
| `BeginQuickTestRunCommand` / `TryBeginQuickTestFromPickerCodeCommand` / `GameFlowDevQueries` | `Commands/CheatGameFlowCommands.cs` | QuickTest 启动命令（经 `GameFlowShellSystem.EnsureRegistered().BeginRun(CreateQuickTest)` / `TryBeginQuickTestFromPickerCode`）与入口可用性只读查询 |

### Cards/（卡牌表现 DevKeys）

| 类型 | 文件 | 一句话职责 |
|------|------|------|
| `CardDeckManagerDevKeys` | `Cards/CardDeckManagerDevKeys.cs` | 卡组层（Keypad1–3）：纯表现入场+开局发牌（Spawn 15 张+Avatar 入场+发环）、清场+随机发 1 张、随机槽位增卡 |
| `GroundFieldManagerDevKeys` | `Cards/GroundFieldManagerDevKeys.cs` | 场地层（Alpha4/5 + Keypad1/4–8）：全体翻牌切换（纯表现 POC）、鼠标下卡 Core Flip（FlipCardAction → EventLog 切片 → 正式 Flip.anim，ADR-0016 链路）、导演攻击相邻怪（经 `AttackInputHook.TrySubmitAttack` 提交意图，反击走主线锁步——#2/#11 后不留旧旁路）、外圈旋转、随机移除、即死击杀、移除+旋转、武装下次交战即死 |
| `CardHandManagerDevKeys` | `Cards/CardHandManagerDevKeys.cs` | 手牌层（Keypad7）：随机抓场地卡入手（尊重 CanAcceptCard/IsBusy，失败释放） |
| `StandardCardViewDevKeys` | `Cards/StandardCardViewDevKeys.cs` | 单卡层（Keypad1–3）：加护甲/减护甲/加攻血——**走 `ApplyPresentation` 快照（与正式 Commit 出口同族），不旁路公开 Set\*** |
| `DevTestStandardCardInstaller` | `Cards/DevTestStandardCardInstaller.cs` | 卡牌底盘预制体的 DevTest 安装宿主（#126）：Dev 下为每实例补装 `StandardCardViewDevKeys`，Release 空壳 |

### Editor/（子程序集，Test Key Monitor 数据源）

| 类型 | 文件 | 一句话职责 |
|------|------|------|
| `TestKeyCatalogEntry` | `Editor/TestKeyCatalogEntry.cs` | 目录项值结构：层/键/标签 + Play Mode 才有意义的「已挂回调」「级联持有」标记 |
| `TestKeyCatalogProvider` | `Editor/TestKeyCatalogProvider.cs` | 目录构建：栈 SO 层 + `Assets/Resources/DevTest` 全部 Profile 的声明键 ∪ Play Mode 运行时挂载状态合并（供 `Window > NineGrid > Test Key Monitor`——监视器窗口本体在表现层 Editor） |
| `TestKeyStackConfigSOEditor` | `Editor/TestKeyStackConfigSOEditor.cs` | 栈 SO 自定义 Inspector：ReorderableList 拖排 + 「选中层置顶」按钮 + 级联溢出说明 |

## 核心流程与数据流

### 级联栈裁决

```
TestKeyStackConfigSO（Resources/DevTest/TestKeyStack）
  layers: [ 低优先级 … 高优先级 ]        ← 列表末项最高
        │ TestKeyInputPoller.BootstrapStack / TestKeyStackHost.Awake
        ▼
TestKeyManager.SetStack → _stackOrder
各 DevKeys 模块 OnEnable → AttachLayer(layerId, bindings)
        ▼
RebuildCascade：for i = 末项 → 首项，未被认领的键归当前层
  → _activeBindings（键 → 胜出层）；其余层同键 = 「溢出」（监视器可见、可直接点触 TryInvokeRegisteredAction）
TestKeyInputPoller.Update → PollInput（快照遍历 GetKeyDown → Invoke）
```

要点：**优先级唯一权威是 SO**——运行时 Attach 顺序不影响裁决；未在 SO 声明的动态层落栈顶（最低优先级）并 LogWarning 提示补进 SO。

### QuickTest 入口链

```
主菜单（CanAcceptQuickTestEntry=true）
  按住 \ ≥0.35s → GameFlowController.ShowQuickTestPickerNotice(菜单文本)
  按数字 0–9（≤2 位）→ 缓冲
  释放 \ → SendCommand(TryBeginQuickTestFromPickerCodeCommand(code))
            → GameFlowShellSystem.TryBeginQuickTestFromPickerCode
  Esc → 取消
```

通道语义（`\0` 与正式镜像一致、`\1`–`\9` 定向注入白板怪挂技能等）在表现层 `GameFlowShellSystem` / `QuickTestRunOptions`，本程序集只负责输入状态机与命令发送。

### 作弊与诊断出口

- 全部状态修改经 QF Command（`CheatSetAvatarHpCommand` 等）→ 表现层 `BattleSessionCheat` 静态缝 → Core；DevTest 不直改 Core 模型。
- 攻击/翻面等盘面操作经正式意图链（`AttackInputHook.TrySubmitAttack`、`FlipCardAction`+`BattleBeatFlush.PresentEventLogSlice`），与玩家输入同轨——#2/#11 明确删除了旧旁路编排。
- 日志：Keypad4 一键导出 Battle/Flow/Perf/Registry 四路 Trace（落 `Assets/Notes/Logs/`，配合 `table-nine-battlelog-analysis` 技能）；Keypad5 四路总开关联动；Keypad7 在 PerfTrace 打「BugScene」人工观察戳点。

## 对外通信面

- **依赖**（全部朝表现层/Core 单向）：`NineGrid.Flow` / `NineGrid.Cards` 命名空间的 Controller/View/Singleton（运行时 Find 兜底 + 序列化覆盖）、`NineGridArchitecture` QF 接口、`BattleSessionCheat` / `AttackInputHook` / `GameFlowShellSystem` 等表现层 Dev 缝、四路 TraceRecorder、`GoldGainPresentationBinder`。
- **被依赖**：仅场景/预制体上的两个安装宿主；`Test Key Monitor` 窗口（表现层 Editor）消费 `TestKeyCatalogProvider`。
- 资源约定：`Assets/Resources/DevTest/`（TestKeyStack + 各 Layer Profile SO）。

## 关联 ADR / 约定

#126（Release 空壳安装器纪律）、ADR-0016（Core Flip 链）、ADR-0040（金币 DevKey 走正式播放链）、ADR-0002/0005（单卡数值调试走 ApplyPresentation 不旁路 Set*）；QuickTest 通道语义见 `.cursor/skills/quick-test-effect-channels/` 与 `Assets/Notes/QuickTest效果通道-技能测试预期对照.md`。

## 不变量与坑

- **场景/预制体永不序列化 `#if` 专属组件**——新增 DevKeys 必须挂进 `DevTestSceneInstaller`（或对应安装宿主）的 Install 列表，直接拖到场景上会在 Release 变 Missing Script。
- 同键多层是常态（如 Keypad9 同时被 Selector 与 GoldGainFx 声明、Keypad1 被三层声明）——运行哪个由级联栈当前顺序决定，调试时先开 Test Key Monitor 看归属，别以为按键坏了。
- `TestKeyManager.PollInput` 轮询前做字典快照——回调里 Attach/Detach 层是安全的；但回调抛异常会被 `TryInvokeRegisteredAction` 捕获、被 `PollInput` 直接抛出（两条路径行为不同）。
- 运行时 AddComponent 的模块没有序列化 layerProfile，按 ModuleId 从 `Resources/DevTest` 回填——新层的 Profile SO 的 layerId 必须与代码 `ModuleId` 精确一致，否则监视器显示裸 id 且不受栈序控制（动态层最低优先级）。
- `DamageNumberManagerDevKeys` 是刻意的空占位；删除它需同步改 `DevTestSceneInstaller`。
- QuickTest 入口只在 `CanAcceptQuickTestEntry`（主菜单空闲态）放行；局内没有 `\` 调速（code-map 明确「无局内 \ 调速」）。
- `DamageLogPanel` 未接入安装器（无场景常驻实例）——需要时手动 AddComponent 到任意常驻对象，F1 开关。
