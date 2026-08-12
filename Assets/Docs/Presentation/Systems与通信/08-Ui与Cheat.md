# Ui 面板与 Cheat 作弊工具

> 覆盖范围：`Ui/` 7 个文件 + `Cheat/` 6 个文件，共 13 个。
> `Ui/` 是世界空间（SpriteRenderer + Collider）面板的场景接线层；`Cheat/` 是 F12 作弊面板（仅 `UNITY_EDITOR || DEVELOPMENT_BUILD`，正式包不含）。

## Ui/ 职责综述

四块面板（局内功能菜单、人物选择、结算、存档读档）共享同一套范式：

- 场景预置 GameObject（默认失活）+ 静态 `sInstance` + `RequestOpen/CloseIfOpen`；
- 命中走 `PointerHitRegistry` / `PointerHitRouter`（世界 UI，非 uGUI 射线）；
- 打开时 `BattleUiDimmerOverlay.TryAcquire(reason)` 挡射线（引用计数，不改 CurrentOwner、不暂停主线）；
- 声音一律经 `InteractionAudioCues.Pulse`（稳定 cue + contentId）。

## Ui/ 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `PlayerAudioSettingsPanel` | `Ui/PlayerAudioSettingsPanel.cs` | 局内功能菜单（`UI面板/局内功能菜单BG`）总接线：`AfterSceneLoad` Install 强制 EnsureBound（失活面板不 Awake，否则主菜单「菜单按钮」挂不上命中代理）；音量模块双静音开关 + BGM/SFX 滑条双向绑定 `IPlayerAudioSettingsSystem`；`功能模块/回到主菜单`→`ReturnToMainMenuCommand`、`退出游戏`→`GameFlowController.QuitGame`、`关闭面板`/Esc/半黑屏→关闭；把 `存档/读档模块` 交给 `RunSaveLoadPanel.EnsureBound`（名字含 `/`，须按直接子节点名查找，不能用 `transform.Find` 路径语义）；滑条拖拽在 Update 里用 `WorldPointerUtility.IsPrimaryHeld` 轮询 |
| `RunSaveLoadPanel` | `Ui/RunSaveLoadPanel.cs` | 存档/读档模块（ADR-0041）：「保存」「加载」双按钮切模式；条目按场景预置「保存条目模板」「加载条目模板」克隆进「UI槽位」（行位常量 RowTopLocalY=1.05 / 间距 0.75）；打开时按上下文选默认页（局内有检查点→保存）；保存写 `RunSaveService.SaveCheckpointToSlot`，读取 `RequestLoadSlot` 前先关面板 |
| `CharacterSelectPanel` | `Ui/CharacterSelectPanel.cs` | 人物选择（`UI面板/人物选择BG`）：主菜单「开始游戏」→ `RequestOpen()`（缺预置返回 false，调用方回退直接开局）；战士（`avatar.default`）默认选中，两未解锁席近黑 tint + 拒选提示（1.6s 自清）；`出发按钮`→`GameFlowController.BeginFormalRun`；Esc/`返回按钮` 关闭；Shell 相位离开 MainMenu 自动收起（QuickTest 旁路开局兜底） |
| `RunSummaryPanel` | `Ui/RunSummaryPanel.cs` | 局终结算（`UI面板/结算面板BG`）：`TryShowAndWaitAsync(victory, ct)` 展示进度/金币/互动/属性/遗物图标墙/种子并**阻塞到玩家点返回**（UniTaskCompletionSource + 外部取消自动收起）；只读展示不发 Core 指令（终端相位纪律）；缺预置返回 false 回退旧 Notice |
| `WorldUiHitButton` | `Ui/WorldUiHitButton.cs` | 世界空间按钮通用件：BoxCollider2D + `IPointerHitTarget`（Overlay 优先级）+ 悬停缩放 + 点击回调；人物选择/结算面板运行时接线复用 |
| `ResourcesSpriteLoop` | `Ui/ResourcesSpriteLoop.cs` | Resources 序列帧循环装饰（`LoadAll<Sprite>` + 帧名尾号排序，unscaled 时钟）；仅面板装饰用，**不进卡面表现管线** |
| `UiAudioFeedback` | `Ui/UiAudioFeedback.cs` | uGUI Selectable 统一声音出口：PointerEnter→`ui.hover`、PointerDown→按可交互性 `ui.press`/`ui.reject`、`PulseAccepted` 由动作回调触发 |

## Cheat/ 职责综述

F12 综合测试后门（#与 code-map「Cheat」节对齐）：接线 MainScene 预置 `UI面板/作弊工具BG`（SpriteRenderer 世界 UI，缺结构才最小运行时兜底）。一级八按钮：一键清关（对齐 QuickTest `-`→`TryForceNodeVictory`）、一键跨层（`BattleSessionCheat.TryBeginCrossFloor`）、战斗加卡、添加遗物、金币+999、回复满血、记录log、无敌模式。

## Cheat/ 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `CheatToolHotkeyHost` | `Cheat/CheatToolHotkeyHost.cs` | 常驻 DDOL 热键宿主：`AfterSceneLoad` 自举；每帧 `CheatToolGodMode.Tick()` + F12 → `CheatToolPanelController.TryToggle` |
| `CheatToolPanelController` | `Cheat/CheatToolPanelController.cs` | 面板主控（1264 行）：一级八按钮接线；二级`第二层_添加卡菜单`（WorldSpace Canvas 搜索列表，运行时补 GraphicRaycaster；Cards/Relics 双模式切换索引与占位文案）与`第二层_log记录面板`（Tag 输入 + 保存 → `DiagTraceManualSnapshot.Save`，产出 `!!!AI-BUG-REPORT!!!` 目录）；Esc 逐层退出（log 层→加卡层→面板）；加卡仅战斗阶段（`ShuffleIntoDrawPileAction` 顶插入），加遗物经 `GrantRelicAction`+`RelicHudHook.RequestSync` |
| `CheatToolPanelButton` | `Cheat/CheatToolPanelButton.cs` | 一级按钮命中代理：`IPointerHitTarget`（HitSort=10000，Overlay 优先级），挂在场景预置 Sprite+BoxCollider2D 上 |
| `CheatToolGodMode` | `Cheat/CheatToolGodMode.cs` | 无敌模式：每 3s 回满血（`BattleSessionCheat.TrySetAvatarHp` + HUD SyncFromCore）/ +999 金币（`PlayerModel.AddCoins`）/ 攻击+10（`TrySetAvatarAttack`）；由 HotkeyHost 驱动，面板关闭仍生效 |
| `CheatToolCardSearchIndex` | `Cheat/CheatToolCardSearchIndex.cs` | 战斗加卡搜索索引（纯逻辑可测）：Catalog 提取 Monster/Trap/HelpCard 三类、排除归档卡组；搜索文本=卡名+DefId+卡组名+技能名/描述（+可注入表现层描述）；显示名优先表现层 JSON `displayName` |
| `CheatToolRelicSearchIndex` | `Cheat/CheatToolRelicSearchIndex.cs` | 添加遗物搜索索引：非归档遗物，名/DefId/DesignText（+可注入描述）子串匹配 |

## 核心流程与数据流

- **局内功能菜单开闭**：主菜单`菜单按钮`（`FunctionMenuHitProxy`，面板开时禁其 collider）或局内 Esc（`EscapeInputRelay` 常驻监听：半黑屏未激活且相位非 MainMenu 时 `RequestOpen`）→ `SetOpen(true)`（Acquire 半黑屏 + `ui.confirm`）；关闭四路（半黑屏点击经 `UiOverlayHitProxy.CloseInRunFunctionMenu`、关闭钮、Esc、回主菜单/退出动作）。
- **音量双向绑定**：UI 改动 → `IPlayerAudioSettingsSystem.SetVolume/SetMuted` → `Changed` 事件 → `RefreshVisuals`（开关指示 + 滑条归一化）。与作者调音工作台完全分离（《04》）。
- **结算面板收口**：`GameFlowOrchestrator.ShowBattleEndAndReturnAsync`（Flow 侧）在 `ClearRunSession` 前快照 Run/Player/Avatar 数据 → `TryShowAndWaitAsync` → 玩家确认 → 放行 `EnterMainMenuImmediate`（该方法开头 `CloseIfOpen` 兜底强退路径）。
- **作弊加卡**：搜索索引 Match → 点击选项 → 战斗相位校验 → `ShuffleIntoDrawPileAction`（顶插入，下一张即到）；加遗物已开局即可（满栏/已拥有/归档拒绝）。

## 对外通信面

- Ui → System：`IPlayerAudioSettingsSystem`（音量）、`IGameFlowShellSystem`（相位监视）、`ReturnToMainMenuCommand`；`RunSaveService`（Flow 侧存档服务）。
- Ui → Flow 表现件：`BattleUiDimmerOverlay`、`InteractionAudioCues`、`WorldPointerUtility`、`KeyboardUtility`、`PointerHitRegistry`。
- Cheat → Core：`BattleSessionCheat`（Flow 侧作弊门面）、`GrantRelicAction`/`ShuffleIntoDrawPileAction`（Core 原子）、`DiagTraceManualSnapshot`（诊断落盘）。

## 关联 ADR / Issue

#171（局内功能菜单）、ADR-0041（存档/读档模块）、ADR-0020（世界 UI 命中范式）、ADR-0028（遗物护甲联动 HUD）、code-map「Cheat」节（F12 面板全行为清单）。

## 不变量与坑

- **失活面板不会 Awake**：所有"默认失活 + 静态入口"面板都依赖 `AfterSceneLoad` Install 或 `EnsureFromScene` 兜底；新增此类面板必须复制这套自举，否则按钮永远接不上。
- `PlayerAudioSettingsPanel.Awake` **不得**强制 SetActive(false)（会把刚打开的面板立刻关掉——代码注释记录过这个 bug）；开局误激活的收口只在 Install。
- `存档/读档模块` 节点名含 `/`——只能按直接子节点遍历名字匹配（`FindDirectChildNamed`），`transform.Find` 会当路径解析。
- `RunSummaryPanel` 只读快照，**不发 Core 指令**（终端相位纪律，ADR-0039 精神）。
- Cheat 全目录 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`；Release 构建物不含（#142 Release 打包终验），引用它们的代码同样要编译隔离。
- `CheatToolGodMode` 直改 Core（AddCoins/SetHp）不走 Batch-ack——HUD 靠 `SyncFromCore` 白名单路径刷新；这是作弊专属旁路，正式功能禁止效仿。
- 二级菜单是 WorldSpace Canvas + GraphicRaycaster（uGUI），与一级世界 Sprite 按钮是两套命中——`PointerHitRouter` 在 EventSystem UI 上时让渡点击。
