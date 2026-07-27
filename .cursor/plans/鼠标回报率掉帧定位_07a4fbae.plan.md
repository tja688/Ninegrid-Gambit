---
name: 鼠标回报率掉帧定位
overview: 掉帧根因指向 Unity 6000.3.10f1 的已知缺陷：高回报率鼠标触发 Windows legacy 输入消息洪水卡住主线程。方案先用 30 秒的回报率实验做确证，再按成本递增落地缓解措施，最后修掉让前几轮排查失明的分析工具与残留的真实浪费。
todos:
  - id: confirm-polling
    content: 把鼠标回报率降到 125Hz，在 Editor 和 build 中各复测一次，确证根因
    status: pending
  - id: cursor-mode
    content: SoftwareCursorBootstrap 的 CursorMode.ForceSoftware 改 Auto，单独 A/B 测量
    status: pending
  - id: input-backend
    content: 评估并收敛 activeInputHandler（Both -> 单一后端），先测 Old only 的收益
    status: pending
  - id: ridev-nolegacy
    content: 实现 RIDEV_NOLEGACY + GetCursorPos 轮询注入 Input System 的出货级修复
    status: pending
  - id: fix-analyzer
    content: 修 ProfilerCaptureAnalyzer：不过滤 EditorLoop、遍历所有线程、输出等待型 marker 与未归因缺口
    status: pending
  - id: cleanup-hotpaths
    content: 清理残留浪费：手牌 alpha 每帧刷新、Camera.main 缓存、TableNineLookRig 脏标记、hover 短路顺序、删除 StartRunHoverScale
    status: pending
isProject: false
---

« 鼠标回报率掉帧：确证与治理

## 背景：为什么之前都没修对

Profiler 抓到的尖峰帧真相是 `frameTime 114.5ms = EditorLoop self 101.28ms + PlayerLoop 1.83ms`。但生成 `analysis.json` 的分析器在 [ProfilerCaptureAnalyzer.cs:417](Assets/Scripts/NineGrid.Presentation/Editor/ProfilerCaptureAnalyzer.cs) 把 `EditorLoop` / `PlayerLoop` 从榜单里剔除了，还只读了主线程（`GetRawFrameDataView(frame, 0)`）。于是所有人看到的「最贵项」都是 0.5ms 量级的东西，在那上面反复优化自然无效。

那 101ms 的形状——**无子项、无托管分配、无 GC**——不是「在算」，是主线程卡在 Win32 消息泵里。

## 根因假设

Unity 已知缺陷：高回报率鼠标产生的 legacy `WM_MOUSEMOVE` 洪水压垮主线程。证据链：

- 项目在 `6000.3.10f1`，Issue Tracker 复现版本含 `6000.3.15f1` / `6000.0.75f1` / `6000.4.7f1`
- 官方记录「Also reproducible in Standalone Player」，与你 build 里同样掉一致
- 官方记录严重度正比于回报率，8000Hz 时 170 FPS → 10 FPS；社区实测 Unity 6.3 下主线程 3ms → 100ms
- 鼠标移出窗口即恢复，与你 500-600 FPS 基线一致
- `ProjectSettings.asset:703` 的 `activeInputHandler: 2`（Old + New 双后端），legacy 消息被处理两遍，是放大器
- [SoftwareCursorBootstrap.cs:38](Assets/Scripts/NineGrid.Presentation/Setup/SoftwareCursorBootstrap.cs) 用 `CursorMode.ForceSoftware`，每次鼠标移动都要 Unity 自己重绘光标，是第二个放大器

## 第 0 步：30 秒确证（不改任何代码）

在鼠标厂商驱动里把回报率降到 **125Hz**，然后：

1. Editor Play Mode 重复你原来的操作：鼠标在格子/卡组位快速划动
2. 同一个 build 里重复一次

判定：
- 掉帧基本消失或大幅缓解 → 根因确认，进第 1 步
- 完全没变化 → 假设推翻，直接跳到第 4 步重做测量

顺带记录你鼠标的型号与当前回报率，后面评估要用。

## 第 1 步：低成本缓解（按顺序单独 A/B，每次只改一项）

1. **关掉软件光标**。把 [SoftwareCursorBootstrap.cs:38](Assets/Scripts/NineGrid.Presentation/Setup/SoftwareCursorBootstrap.cs) 的 `CursorMode.ForceSoftware` 改成 `CursorMode.Auto`，恢复回报率后再测一次。软件光标要求 Unity 在每次鼠标移动时重绘光标，与消息洪水相乘。

2. **收敛输入后端**。`activeInputHandler: 2` 改为单一后端。代价评估要先做：代码里大量依赖 legacy 的 `OnMouseEnter/Exit/Down`（`GroundCardHitProxy`、`GroundSlotHitProxy`、`HandCardHitProxy`、`BoardSelectParkedCardHitProxy`）和 `Input.mousePosition`，所以短期改成 **Old only（0）** 成本最低、能立刻砍掉一半消息处理；改成 New only（1）需要先替换全部 `OnMouse*`，属于中期工作。

## 第 2 步：出货级修复（确证成立后必做）

玩家不会为了你的游戏去调回报率，所以要在进程内绕开 legacy 消息：注册 Raw Input 时带 `RIDEV_NOLEGACY` 标志，让 Windows 跳过 legacy 消息翻译；再在 `Update` 里用 `GetCursorPos` + `GetAsyncKeyState` 轮询鼠标状态，通过 `InputSystem.QueueStateEvent` 注入回 Input System。

注意：`RIDEV_NOLEGACY` 会连点击相关的 `WM_MOUSE*` 一起干掉，所以必须补上轮询注入，否则按钮点击会失效。这与第 1 步第 2 项（收敛到 New only）是配套的，建议一起做。

同时到 Unity Issue Tracker 上关注该 issue 的修复版本，评估是否升级 Editor。

## 第 3 步：修好测量工具，别再瞎一次

改 [ProfilerCaptureAnalyzer.cs](Assets/Scripts/NineGrid.Presentation/Editor/ProfilerCaptureAnalyzer.cs)：

- 删掉第 417 行对 `EditorLoop` / `PlayerLoop` 的过滤
- 遍历所有线程，不要只取 `threadIndex = 0`（Render Thread 之前从没被看过）
- 额外输出 `Gfx.WaitForPresent*` / `Semaphore.WaitForSignal` / `WaitForTargetFPS` 这类等待型 marker，以及每帧 `frameTimeMs` 减去主线程有 marker 覆盖时间的「未归因缺口」

## 第 4 步：残留的真实浪费（次要，第 1 步之后再动）

这些确实在浪费 CPU，但都在 `PlayerLoop` 里，而尖峰帧 `PlayerLoop` 只有 1.8–5ms，所以它们不是本次的根因，属于顺手清理：

- [CardHandManagerSingleton.cs:867-871](Assets/Scripts/NineGrid.Presentation/Cards/CardHandManagerSingleton.cs) hover 目标未变化时仍每帧调 `RefreshHandHoverAlphas`，内部对每张手牌 `GetComponentsInChildren<SpriteRenderer>(true)`。改为仅在 hover 目标切换时刷新，并缓存 renderer 数组
- `Camera.main` 每帧调用三处（`CardHandManagerSingleton.cs:708`、`CardDeckManagerSingleton.cs:1644`、`PlayerInfoHudPresenter.cs:571`），缓存起来
- [TableNineLookRig.cs:113-116](Assets/Scripts/UI/VisualLook/TableNineLookRig.cs) `[ExecuteAlways]` 每帧无条件 `ApplyLookParams()`，其中含 `FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, ...)` 全场景扫描。加脏标记
- [GroundCardHitProxy.cs:210-211](Assets/Scripts/NineGrid.Presentation/Cards/GroundCardHitProxy.cs) `CanRespondToHover` 把便宜的 `IsGroundHoverEligible` 放在 `&&` 右侧，导致每次都先跑完整的 `TryPassGroundInputGate`。交换顺序
- 删掉 [StartRunHoverScale.cs](Assets/Scripts/Temporary%20Test/StartRunHoverScale.cs)，它是热重载临时实验、MainScene 未引用，但每次 hover 打两条 `Debug.Log`

## 已排除，不要再查

- 场景规模：`MainScene.unity` 只有 196 个 GameObject、175 个 Transform
- GC：110ms 尖峰帧托管分配仅 0.5–2KB，全程无 `GC.Collect` 采样
- 渲染：尖峰帧 `Inl_UniversalRenderTotal` 仅 1.5ms
- Gizmos：业务脚本零 `OnDrawGizmos`
- 诊断探针：Perf/Registry 只写内存，hover 热路径不打日志不写盘
- Description TMP 系统：已退役为 no-op
- Locus / Pipeline 编辑器桥：桌面端未运行，且 build 里根本不存在却照样掉帧

## 收尾

第 1 步定论后，按 `.cursor/rules/code-map-maintenance.mdc` 判断是否需要同步 `docs/code-map/`：若第 1 步第 2 项改了输入后端、或第 2 步引入了 Raw Input 注入层，属于输入通信范式变更，需要写 ADR 并在 code-map 里引用。»