---
status: superseded
---

# Windows Player 高回报率鼠标兜底（RIDEV_NOLEGACY）

> **已废止**（2026-08-14）：放弃进程内 Win32 鼠标管控（`RegisterRawInputDevices` / `RIDEV_NOLEGACY` / WndProc 子类化 / `GetCursorPos`+`GetAsyncKeyState` 注入），以及配套的 `WindowsHangWatchdog` minidump 取证。打包体改走 Unity 原生 New Input System。若 Win10 仍卡死或掉帧，视为引擎（UUM-142550）与系统之间的问题，不再在游戏进程内 workaround。

## 现行行为

- 指针读口仍是 [`WorldPointerUtility`](../../Assets/Scripts/NineGrid.Presentation/Flow/WorldPointerUtility.cs)（`Mouse.current`）。
- 卡牌/槽位命中仍禁止 `OnMouse*`，由 [`PointerHitRouter`](../../Assets/Scripts/NineGrid.Presentation/Flow/PointerHitRouter.cs) 轮询（约定改挂 [ADR-0023](0023-slot-hit-frame-and-claim.md)）。
- 工程 `activeInputHandler = 1`（New Input System only）不变。
- 不改变 [ADR-0004](0004-input-intake-two-axis-gating.md) 的 IntentIntake 两轴门禁。

## 历史决策（已废止）

曾在 Windows Standalone Player 内用 `RIDEV_NOLEGACY` 掐 legacy `WM_MOUSE*` 洪水，再轮询注入 Input System，并为卡死写 HangWatchdog。该路径在 Win10 上仍出现切屏卡死，且 `GetAsyncKeyState` 注入会合成虚假按下边沿（滑动鼠标误触点击）。已删除 `NineGrid.Presentation/Platform/` 整目录与 game1 诊断 bat。
