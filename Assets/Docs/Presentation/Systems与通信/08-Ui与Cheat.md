# Ui 面板与 Cheat 作弊工具

> 覆盖范围：`Ui/` 15 个文件 + `Cheat/` 6 个文件，共 21 个。
> `Ui/` 是世界空间（SpriteRenderer + Collider）面板的场景接线层；`Cheat/` 是 F12 作弊面板（仅 `UNITY_EDITOR || DEVELOPMENT_BUILD`，正式包不含）。

## Ui/ 职责综述

五块**正式面板**（局内功能菜单、选人界面、完结结算、存档读档、主菜单继续游戏加载弹窗）共享同一套范式：

- 场景预置 GameObject（默认失活）+ 静态 `sInstance` + `RequestOpen/CloseIfOpen`；
- 命中走 `PointerHitRegistry` / `PointerHitRouter`（世界 UI，非 uGUI 射线）；
- 底幕分两档：局内功能菜单用半黑屏 `BattleUiDimmerOverlay.TryAcquire(reason)`；**选人界面 / 完结结算用纯黑屏 `PureBlackScreenOverlay.Acquire(reason)`**（`UI面板/纯黑屏BG`，全屏纯黑吞点击，与半黑屏引用计数各自独立）；
- 危险操作（覆盖存档 / 退出游戏 / 回到主菜单）经 `UiConfirmPrompt` 提示框二次确认（模态阻塞 + 同意/返回）；
- 声音一律经 `InteractionAudioCues.Pulse`（稳定 cue + contentId）；
- 玩家可见字面量一律 `L10n.Tr(key, 中文默认值)`（ADR-0046：zh 走默认值、en 查 ui 表缺键回中文）；场景静态 TMP 标签由 `SceneTextLocalizer` 统一覆盖。

第六块 `BattleLogPanel`（战斗日志）是**例外的一档**——它不是正式面板而是**场地层中间态**：不取任何底幕、不模态、排序层沉到 `UI/-12`（低于半黑屏 `UI/-1` 与功能菜单 `UI/0`），任何正式 UI 打开都盖住它，覆层激活时它的命中面自动让位。它既不阻塞别的 UI，别的 UI 也不必先关它。

## Ui/ 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `PlayerAudioSettingsPanel` | `Ui/PlayerAudioSettingsPanel.cs` | 局内功能菜单（`UI面板/局内功能菜单BG`）总接线：`AfterSceneLoad` Install 强制 EnsureBound（失活面板不 Awake，否则主菜单「菜单按钮」挂不上命中代理）；音量模块双静音开关 + BGM/SFX 滑条双向绑定 `IPlayerAudioSettingsSystem`；`功能模块/回到主菜单`、`退出游戏` 先经 `功能模块/提示框`（`UiConfirmPrompt`）确认，同意才发 `ReturnToMainMenuCommand` / `GameFlowController.QuitGame`；`关闭面板`/Esc/半黑屏→关闭（反馈窗开着时 Esc 先关反馈窗；提示框开着时 Esc 让位给提示框）；把 `存档/读档模块` 交给 `RunSaveLoadPanel.EnsureBound`（名字含 `/`，须按直接子节点名查找，不能用 `transform.Find` 路径语义）；并把 `功能模块/BugLogo` 交给 `PlaytestFeedbackPanel.EnsureBound`；滑条拖拽在 Update 里用 `WorldPointerUtility.IsPrimaryHeld` 轮询 |
| `PlaytestFeedbackPanel` | `Ui/PlaytestFeedbackPanel.cs` | 局内试玩反馈：点子 Logo 打开 `功能模块` 下的 `输入框窗口` / `BugLogo输入框窗口`（可与 Logo 平级，避免 Logo 悬停缩放带动窗体；默认失活，WorldSpace Canvas）；上勾提交 Bug、下勾提交意见，均在 FNS `游戏开发项目/九宫格登神/游戏开发协作/` **各新建一篇笔记**（不续写、不传 log）；成功后覆盖「提交意见按钮」下 TMP（Bug：`bug已提交，立刻加班修复！` / 意见：`建议收到啦，感谢你的支持！`）；叉关窗。凭据只读 gitignore 的 `StreamingAssets/fns-playtest.secret.json`；提交协程挂 DDOL 宿主，关菜单不中断上报 |
| `RunSaveLoadPanel` | `Ui/RunSaveLoadPanel.cs` | 存档/读档模块（ADR-0041）：默认展示**保存**面板（保存钮激活/加载钮失活），「加载」切读档列表；保存视图恒四行——槽位一为**默认存档**（自动档展示，系统维护不可手动覆盖）+ 三个手动槽（空位直接存、已有存档先经提示框确认覆盖）；加载视图只列已有存档（自动档 + 手动槽 ≤4，无档显示「暂无存档」）；条目按场景预置「保存条目模板」「加载条目模板」克隆进「UI槽位」（行位常量 RowTopLocalY=1.05 / 间距 0.75）；保存写 `RunSaveService.SaveCheckpointToSlot`，读取 `RequestLoadSlot` 前先关面板 |
| `CharacterSelectPanel` | `Ui/CharacterSelectPanel.cs` | 选人界面（`UI面板/选人界面BG`，**纯黑屏底幕**）：主菜单「开始游戏」→ `RequestOpen()`（缺预置返回 false，调用方回退直接开局）；角色1=战士——`立绘/__Art` 挂 `ResourcesSpriteLoop` 会动 idle（帧高 5.6 世界单位、按包围盒回中，Play 实测校准）+「角色专属道具卡」显示初始遗物图标，**点击立绘即选定并出发**（`BeginFormalRun`）；角色2/3=未解锁席（Layla/Icey 序列帧黑剪影，点击拒绝音 + 描述文字闪橙提示）；难度三档可点选（普通默认，全部路由默认数据，仅记录进 `RunSetupSelection` 供结算展示——后续实装难度路由再扩展）；`回到主菜单 (1)` 直接返回**不提示**，小字说明**悬停才出现**；Esc 关闭；Shell 相位离开 MainMenu 自动收起 |
| `RunSummaryPanel` | `Ui/RunSummaryPanel.cs` | 完结结算（`UI面板/完结结算BG`，**纯黑屏底幕**）：`TryShowAndWaitAsync(victory, ct)` 展示——胜负大字（胜利金字/失败灰红字，文案不同）、会动战士立绘、本局所选难度图标（读 `RunSetupSelection`）、本局遗物墙（12 占位槽按视觉序填充）、右侧文字统计（所用时长/击败怪物数/损失血量/使用道具卡数，数据源 `RunRecapTracker`）；`回到主菜单`/`退出游戏` 先经 `提示框` 确认，`再来一局` 直接收面板放行回主菜单收口后自动重开选人界面；**阻塞到玩家选择去向**（UniTaskCompletionSource + 外部取消自动收起）；只读展示不发 Core 指令（终端相位纪律）；缺预置返回 false 回退旧 Notice |
| `MainMenuLoadPanel` | `Ui/MainMenuLoadPanel.cs` | 主菜单「继续游戏」加载弹窗（`MainPanel/ContinueGame/加载的UI槽位`，默认失活）：列出已有存档（自动档优先 + 手动槽 ≤4，复用场景预置 4 行模板、多余行隐藏），点击条目即 `RunSaveService.RequestLoadSlot`；`关闭面板 (1)`/Esc 关闭；无任何存档 `RequestOpen` 返回 false（`GameFlowController` 播拒绝音 + 短暂 Notice）；弹窗底板吞点击防误触其下主菜单按钮 |
| `BattleLogPanel` | `Ui/BattleLogPanel.cs` | 战斗日志面板（`UI面板/战斗日志BG`，默认失活，2026-08-13）：`AfterSceneLoad` Install 强制 EnsureBound（同功能菜单，失活面板不 Awake）；根级「战斗日志按钮」开、面板内 `关闭面板 (1)`/Esc 关；Scroll View 的 `Viewport/Content` 下运行时建单个 `__BattleLogText`（`TextMeshProUGUI`，字体取场景已有 SmileySans，32pt）整段渲染 `BattleLogStore` 富文本（房间段标题 + 效果组头 + `<indent=1.6em>` 明细，上限 400 行/20000 字），打开即滚到底（默认看当前房间，上翻是全局历史）；命中占覆层 sort 6（按钮）/7（面板吞点面，防点穿棋盘）/8（关闭钮），`BattleUiDimmerOverlay.IsActive` 时后两者每帧自动禁用；按钮可点性由挂在按钮上的常驻 `ToggleButtonGate` 判定（主菜单相位 / 覆层激活 / 面板已开一律禁 collider）；`PlayerAudioSettingsPanel.CanOpenFromEscape` 已加入 `BattleLogPanel.IsOpen` 判断，Esc 不会一下既关日志又开功能菜单。日志数据侧见 [Flow/11-人读战斗日志](../Flow/11-人读战斗日志（BattleLog）.md) |
| `UiConfirmPrompt` | `Ui/UiConfirmPrompt.cs` | 通用确认提示框：绑定场景预置「提示框」节点（多条提示文案 TMP 子节点 + 确认 `F_UI_MenuIcons_A4`/取消 `F_UI_MenuIcons_A3` 图标钮）；`Show(文案名, onConfirm, onCancel)` 只显示对应文案；打开时全屏模态阻塞（BlockerHitSort=20000 压过作弊面板/手牌/场地）；Esc=取消（`EscapeHandledThisFrame` 供宿主面板让位判定）；随宿主面板整体关闭时 OnDisable 自动出栈清回调；两处实例——`局内功能菜单BG/功能模块/提示框`（覆盖存档/退出/回主菜单三文案）与 `完结结算BG/提示框`（退出/回主菜单两文案） |
| `PureBlackScreenOverlay` | `Ui/PureBlackScreenOverlay.cs` | 纯黑屏底幕静态门面：绑定 `UI面板/纯黑屏BG`（引用计数 Acquire/Release + Swallow 吞点击）；绑定时若发现该节点误挂 `BattleUiDimmerOverlay` 组件会**先摘除**（该组件是单例，随激活 Awake 会劫持半黑屏静态实例） |
| `RunSetupSelection` | `Ui/RunSetupSelection.cs` | 开局选择状态静态存根（选人界面写、结算面板读）：难度 id/显示名/图标精灵；当前难度全部路由默认（普通）数据，仅作展示记录 |
| `PanelArtUtility` | `Ui/PanelArtUtility.cs` | 面板装饰美术小工具（选人/结算共用）：`__Art` 子节点序列帧装载（`EnsureLoopArt`）/ 静态图标（`SetStaticArt`）+ `FitWorldHeight`（按精灵包围盒等比缩放到目标世界高度并把包围盒中心对回锚点——战士序列帧 pivot 在脚底，不回中会整体上跑）；只服务面板装饰层，不进卡面表现管线 |
| `WorldUiHitButton` | `Ui/WorldUiHitButton.cs` | 世界空间按钮通用件：BoxCollider2D + `IPointerHitTarget`（Overlay 优先级）+ 悬停缩放 + 点击/悬停进出回调（onClick 传 null 即纯吞点击阻挡层）；选人/结算/提示框/加载弹窗运行时接线复用 |
| `ResourcesSpriteLoop` | `Ui/ResourcesSpriteLoop.cs` | Resources 序列帧循环装饰（`LoadAll<Sprite>` + 帧名尾号排序，unscaled 时钟）；仅面板装饰用，**不进卡面表现管线** |
| `UiAudioFeedback` | `Ui/UiAudioFeedback.cs` | uGUI Selectable 统一声音出口：PointerEnter→`ui.hover`、PointerDown→按可交互性 `ui.press`/`ui.reject`、`PulseAccepted` 由动作回调触发 |
| `SceneTextLocalizer` | `Ui/SceneTextLocalizer.cs` | MainScene 静态标签本地化（ADR-0046）：显式序列化 TMP 引用 + ui 键数组（禁止 Find）；Awake 捕获场景中文原文作默认值，订阅 `ILanguageSettingsSystem.Changed` 即时刷新（失活面板下的 TMP 同样可写）；MainScene 挂一处，条目跨主菜单/局内功能菜单/战斗信息预览等面板 |

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

- **局内功能菜单开闭**：主菜单`菜单按钮`（`FunctionMenuHitProxy`，面板开时禁其 collider）或局内 Esc（`EscapeInputRelay` 常驻监听：半黑屏未激活且相位非 MainMenu 时 `RequestOpen`）→ `SetOpen(true)`（Acquire 半黑屏 + `ui.confirm`）；关闭四路（半黑屏点击经 `UiOverlayHitProxy.CloseInRunFunctionMenu`、关闭钮、Esc、回主菜单/退出动作——后两者先过提示框确认）。
- **音量双向绑定**：UI 改动 → `IPlayerAudioSettingsSystem.SetVolume/SetMuted` → `Changed` 事件 → `RefreshVisuals`（开关指示 + 滑条归一化）。与作者调音工作台完全分离（《04》）。
- **结算面板收口**：`GameFlowOrchestrator.ShowBattleEndAndReturnAsync`（Flow 侧）在 `ClearRunSession` 前展示 → `TryShowAndWaitAsync` → 玩家确认回主菜单（提示框）/ 退出游戏（提示框）/ 再来一局 → 放行 `EnterMainMenuImmediate`（该方法开头 `CloseIfOpen` 兜底强退路径）。**再来一局**在放行后由 `OpenCharacterSelectAfterReturnAsync` 等 Shell 收口回 MainMenu（超时 8s 放弃）再 `CharacterSelectPanel.RequestOpen()`，直接从选人界面重开。
- **结算统计数据源**：`Flow/GameFlow/RunRecapTracker`（`GameFlowOrchestrator.Start` 每次 BeginRun 调 `HandleRunStarted` 清零重开计时）以 EventLog 游标只读累计——`CardKilled`（registry Kind=Monster 计击杀）、`HpChanged`（Delta<0 且 Kind=Avatar 计损血）、`ItemUsed`（cause=replay 的存档重放不计）；结算面板取数前 `ScanNow()` 追扫末批。读档恢复后统计从恢复点重新累计（已知限制）。
- **主菜单继续游戏**：`GameFlowController` 轮询 `ContinueGame` 按钮（hover/press/reject 三音）→ `MainMenuLoadPanel.RequestOpen()`；弹窗/选人界面/纯黑屏任一激活时主菜单按钮轮询整体让位（Update 早退）。
- **作弊加卡**：搜索索引 Match → 点击选项 → 战斗相位校验 → `ShuffleIntoDrawPileAction`（顶插入，下一张即到）；加遗物已开局即可（满栏/已拥有/归档拒绝）。

## 对外通信面

- Ui → System：`IPlayerAudioSettingsSystem`（音量）、`IGameFlowShellSystem`（相位监视）、`ReturnToMainMenuCommand`；`RunSaveService`（Flow 侧存档服务）。
- Ui → Flow 表现件：`BattleUiDimmerOverlay`、`InteractionAudioCues`、`WorldPointerUtility`、`KeyboardUtility`、`PointerHitRegistry`。
- Cheat → Core：`BattleSessionCheat`（Flow 侧作弊门面）、`GrantRelicAction`/`ShuffleIntoDrawPileAction`（Core 原子）、`DiagTraceManualSnapshot`（诊断落盘）。

## 关联 ADR / Issue

#171（局内功能菜单）、ADR-0041（存档/读档模块）、ADR-0020（世界 UI 命中范式）、ADR-0028（遗物护甲联动 HUD）、code-map「Cheat」节（F12 面板全行为清单）。

## 不变量与坑

- **失活面板不会 Awake**：所有"默认失活 + 静态入口"面板都依赖 `AfterSceneLoad` Install 或 `EnsureFromScene` 兜底；新增此类面板必须复制这套自举，否则按钮永远接不上。且 `AddComponent` 到失活节点同样不触发 Awake——`SetOpen`/`EnsureBound` 开头必须自带 `EnsureInstanceState`（选人面板修过一次「RequestOpen 返回 true 但面板没开」就是这个坑）。
- `PlayerAudioSettingsPanel.Awake` **不得**强制 SetActive(false)（会把刚打开的面板立刻关掉——代码注释记录过这个 bug）；开局误激活的收口只在 Install。
- `存档/读档模块` 节点名含 `/`——只能按直接子节点遍历名字匹配（`FindDirectChildNamed`），`transform.Find` 会当路径解析。
- **`纯黑屏BG` 不得挂 `BattleUiDimmerOverlay`**：该组件是单例（Awake 抢 s_instance），复制半黑屏做纯黑屏时必须摘掉组件（场景已摘，`PureBlackScreenOverlay.EnsureBound` 另有防御性摘除）。
- **Esc 优先级**：反馈窗开着时 Esc 只关反馈窗（`PlaytestFeedbackPanel.TryHandleEscape`）；提示框开着时 Esc 只取消提示框（`UiConfirmPrompt.IsAnyOpen / EscapeHandledThisFrame` 双判），宿主面板同帧不得再消费同一次按键。
- `RunSummaryPanel` 只读快照，**不发 Core 指令**（终端相位纪律，ADR-0039 精神）；`RunRecapTracker` 同为只读旁路，失败静默不阻塞主线。
- 战士序列帧（`Human_Soldier_Sword_Shield_Idle-Sheet`）pivot 在**脚底**且 96×96 帧内主体只占一小块——面板立绘必须走 `PanelArtUtility.FitWorldHeight`（含包围盒回中），帧高常数是 Play 实测校准值，勿凭 PPU 推算。
- Cheat 全目录 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`；Release 构建物不含（#142 Release 打包终验），引用它们的代码同样要编译隔离。
- `CheatToolGodMode` 直改 Core（AddCoins/SetHp）不走 Batch-ack——HUD 靠 `SyncFromCore` 白名单路径刷新；这是作弊专属旁路，正式功能禁止效仿。
- 二级菜单是 WorldSpace Canvas + GraphicRaycaster（uGUI），与一级世界 Sprite 按钮是两套命中——`PointerHitRouter` 在 EventSystem UI 上时让渡点击。
