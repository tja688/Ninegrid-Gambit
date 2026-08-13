---
status: accepted
---

# Windows Player 高回报率鼠标兜底（RIDEV_NOLEGACY）

## 决策

在 **Windows Standalone Player**（非 Editor）进程内启用高回报率鼠标缓解：

1. 以 `RegisterRawInputDevices` 注册鼠标设备并带 **`RIDEV_NOLEGACY`**，让 Windows 跳过 legacy `WM_MOUSE*` 翻译，消除高回报率（1k/2k/8kHz）下主线程消息泵洪水。**（2026-08-13 修订）NOLEGACY 只在「窗口聚焦且光标在客户区内」时启用**：光标移入非客户区（标题栏 / 边框 / 关闭钮）或窗口失焦时，重注册为 `dwFlags=0`（保留 Raw Input、恢复 legacy 翻译）。legacy 鼠标消息同时承担标题栏拖动、边框缩放、关闭按钮与点击激活；常开 NOLEGACY 会把打包版窗口变成拖不动、缩放不了、关不掉的死窗口（Win10 / Win11 实测复现，Win10 上叠加超尺寸窗口钉左上角被报为「卡死」）。
2. 在 `InputSystem.onBeforeUpdate` 中用 **`GetCursorPos` + `ScreenToClient` + `GetAsyncKeyState`** 轮询指针与按键，经 **`InputSystem.QueueStateEvent(Mouse.current, MouseState)`** 注回 New Input System。
3. 项目 **`activeInputHandler = New Input System only`**；业务指针一律经 [`WorldPointerUtility`](../../Assets/Scripts/NineGrid.Presentation/Flow/WorldPointerUtility.cs) 读取。
4. 卡牌/槽位命中 **禁止 `OnMouse*`**，改由 [`PointerHitRouter`](../../Assets/Scripts/NineGrid.Presentation/Flow/PointerHitRouter.cs) 每帧轮询 `IPointerHitTarget`（Enter/Exit/Down 边沿）。`OnMouse*` 依赖 legacy 消息，与 NOLEGACY 不兼容，且注入 Input System **不会**复活 `OnMouse*`。
5. **禁止**在 Editor / Play Mode 启用 NOLEGACY（会弄死编辑器 UI）。实现以 `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` 编译隔离。
6. 本决策是 Unity issue [UUM-142550](https://issuetracker.unity3d.com/issues/play-mode-framerate-drops-significantly-when-moving-the-mouse-cursor-with-high-polling-rate) 的进程内 workaround；官方修复（跟踪线含 6.7.X）落地且 Player 复测通过后，默认关闭并删除 P/Invoke 路径。

不改变 [ADR-0004](0004-input-intake-two-axis-gating.md) 的 IntentIntake 两轴门禁：平台层只改「指针从哪来 / 如何命中」，意图仍唯一经 Intake。

## 为什么

高回报率鼠标在 Unity 6（含 6000.3）会把 Raw Input 翻成海量 legacy `WM_MOUSEMOVE`，主线程卡在消息泵（Profiler 尖峰帧 self 巨大、无子项、无 GC）。玩家不会为游戏去调驱动回报率；双输入后端与软件光标只是放大器，不足以兜 2k/8k。社区与本仓确证：`RIDEV_NOLEGACY` + 轮询注入是可出货路径。

本仓出货点击曾依赖 `OnMouse*`；若只上 NOLEGACY 会断场地/手牌/槽位点击。故硬前置为轮询 HitRouter + New Input only。

## 考虑过的替代

- **只让玩家降回报率**：开发确证可用，出货不可接受。
- **只切 Old Input only**：实现便宜，挡不住 2k+，不作为终态。
- **Editor 也开 NOLEGACY**：会坏编辑器输入，否决。
- **等待引擎升到当前 6.3/6.4 修复**：issue 修复线不在近期 6.3 补丁承诺内，不能当出货依赖。

## 后果

- Win Player 启动即注册 mitigation（`RuntimeInitializeOnLoad`）；失焦时不注入按键，避免窗外点击串入。
- NOLEGACY 为逐帧期望态开关（聚焦 + 光标在客户区），非客户区交互（拖动 / 缩放 / 关闭 / 点击激活）交还系统 legacy 路径；回归时须同时验证「客户区内高回报率不卡」与「窗口 chrome 可正常操作」。
- 回归矩阵（专项验收，非每票默认）：回报率 125 / 1000 / 4000+ Hz × 窗口 / 无边框 / 全屏；覆盖地面 hover、点空槽、点怪、手牌拖放、BoardSelect、BounceFan。
- 结构护栏：HitProxy 源码禁 `OnMouse*`；ADR-0006 accepted。
- 退出策略：引擎修复后关 mitigation，删 `Platform/WindowsHighPollingMouseMitigation` P/Invoke 实现，保留指针缝与 HitRouter（它们仍是更稳的输入架构）。

## 相关

- [ADR-0004](0004-input-intake-two-axis-gating.md) — 输入唯一收口；本决策不改两轴语义
- Unity UUM-142550 — 高回报率鼠标掉帧
- code-map：[`docs/code-map/presentation.md`](../code-map/presentation.md) 指针缝 / HitRouter / mitigation
