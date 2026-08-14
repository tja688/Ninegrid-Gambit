---
status: accepted
---

# Windows Player 高回报率鼠标兜底（RIDEV_NOLEGACY）

## 决策

在 **Windows Standalone Player**（非 Editor）进程内启用高回报率鼠标缓解：

1. 以 `RegisterRawInputDevices` 注册鼠标设备并带 **`RIDEV_NOLEGACY`**，让 Windows 跳过 legacy `WM_MOUSE*` 翻译，消除高回报率（1k/2k/8kHz）下主线程消息泵洪水。
   - **聚焦且光标在客户区内**：启用 `RIDEV_NOLEGACY`。
   - **聚焦且光标在标题栏/边框（非客户区）**：`RIDEV_REMOVE` 卸掉本进程注册，把 chrome 交互还给系统 legacy 路径——**禁止** `dwFlags=0` 重注册（「既要 Raw Input 又要 legacy」会在 Alt-Tab 切回时放大消息洪水）。
   - **失焦（Alt-Tab 离开）**：**保持 `RIDEV_NOLEGACY`，禁止改注册**。失焦时不需要拖标题栏；若在失焦时关 NOLEGACY，切回前积压的鼠标事件会被翻译成 `WM_MOUSEMOVE` 洪水并堵死主线程（Win10 高发，Win11 因消息合并较不易复现）。
   - **`WM_ACTIVATE` / `WM_SETFOCUS`**：经 `SetWindowLongPtr(GWLP_WNDPROC)` 子类化，在窗口重新激活时**立刻**重开 NOLEGACY，不等待 `InputSystem.onBeforeUpdate`（后者发生在消息泵之后，太晚）。
2. 在 `InputSystem.onBeforeUpdate` 中用 **`GetCursorPos` + `ScreenToClient` + `GetAsyncKeyState`** 轮询指针与按键，经 **`InputSystem.QueueStateEvent(Mouse.current, MouseState)`** 注回 New Input System。失焦时不注入（防窗外点击串入）。
3. 项目 **`activeInputHandler = New Input System only`（`1`）**；业务指针一律经 [`WorldPointerUtility`](../../Assets/Scripts/NineGrid.Presentation/Flow/WorldPointerUtility.cs) 读取。
4. 卡牌/槽位命中 **禁止 `OnMouse*`**，改由 [`PointerHitRouter`](../../Assets/Scripts/NineGrid.Presentation/Flow/PointerHitRouter.cs) 每帧轮询 `IPointerHitTarget`（Enter/Exit/Down 边沿）。`OnMouse*` 依赖 legacy 消息，与 NOLEGACY 不兼容，且注入 Input System **不会**复活 `OnMouse*`。
5. **禁止**在 Editor / Play Mode 启用 NOLEGACY（会弄死编辑器 UI）。实现以 `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` 编译隔离。
6. 本决策是 Unity issue [UUM-142550](https://issuetracker.unity3d.com/issues/play-mode-framerate-drops-significantly-when-moving-the-mouse-cursor-with-high-polling-rate) 的进程内 workaround；官方修复（跟踪线含 6.7.X）落地且 Player 复测通过后，默认关闭并删除 P/Invoke 路径。

不改变 [ADR-0004](0004-input-intake-two-axis-gating.md) 的 IntentIntake 两轴门禁：平台层只改「指针从哪来 / 如何命中」，意图仍唯一经 Intake。

## 为什么

高回报率鼠标在 Unity 6（含 6000.3）会把 Raw Input 翻成海量 legacy `WM_MOUSEMOVE`，主线程卡在消息泵（Profiler 尖峰帧 self 巨大、无子项、无 GC）。玩家不会为游戏去调驱动回报率；双输入后端与软件光标只是放大器，不足以兜 2k/8k。社区与本仓确证：`RIDEV_NOLEGACY` + 轮询注入是可出货路径。

本仓出货点击曾依赖 `OnMouse*`；若只上 NOLEGACY 会断场地/手牌/槽位点击。故硬前置为轮询 HitRouter + New Input only。

2026-08-14 修订：此前「失焦或光标出客户区时 `dwFlags=0`」会在 Win10 打包体 Alt-Tab 切回时复现卡死；改为失焦保持 NOLEGACY、chrome 仅 `RIDEV_REMOVE`，并在 WndProc 激活点抢先注册。

## 考虑过的替代

- **只让玩家降回报率**：开发确证可用，出货不可接受。
- **只切 Old Input only**：实现便宜，挡不住 2k+，不作为终态。
- **失焦关 NOLEGACY 换可拖标题栏**：否决——失焦时本就不需要拖标题栏，且切回洪水代价更高。
- **Editor 也开 NOLEGACY**：会坏编辑器输入，否决。
- **等待引擎升到当前 6.3/6.4 修复**：issue 修复线不在近期 6.3 补丁承诺内，不能当出货依赖。

## 后果

- Win Player 启动即注册 mitigation（`RuntimeInitializeOnLoad`）；失焦时不注入按键，**且不失焦关 NOLEGACY**。
- NOLEGACY 状态机：聚焦+客户区=ON；聚焦+chrome=`RIDEV_REMOVE`；失焦=保持 ON；`WM_ACTIVATE` 抢先 ON。回归须验证「客户区内高回报率不卡」「标题栏可拖/关」「Alt-Tab 切回不卡」。
- 命令行 `-ng-no-rawinput` 可整体关闭做排除法。
- 回归矩阵（专项验收，非每票默认）：回报率 125 / 1000 / 4000+ Hz × 窗口 / 无边框 / 全屏；覆盖地面 hover、点空槽、点怪、手牌拖放、BoardSelect、BounceFan；**另加 Alt-Tab × Win10**。
- 结构护栏：HitProxy 源码禁 `OnMouse*`；ADR-0006 accepted。
- 退出策略：引擎修复后关 mitigation，删 `Platform/WindowsHighPollingMouseMitigation` P/Invoke 实现，保留指针缝与 HitRouter（它们仍是更稳的输入架构）。

## 相关

- [ADR-0004](0004-input-intake-two-axis-gating.md) — 输入唯一收口；本决策不改两轴语义
- Unity UUM-142550 — 高回报率鼠标掉帧
- code-map：[`docs/code-map/presentation.md`](../code-map/presentation.md) 指针缝 / HitRouter / mitigation / HangWatchdog
