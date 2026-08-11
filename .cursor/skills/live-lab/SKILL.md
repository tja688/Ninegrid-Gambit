---
name: live-lab
description: >-
  Run a live collaboration session on the running game: the human plays in
  the Editor while the agent injects probes and patches into the live domain
  via MCP execute_code (tune audio/visual/feel without stopping Play), then
  lands accepted changes as real code/JSON afterward. Use when the user wants
  边玩边调 / 运行时协作 / live tuning during Play, asks to hot-adjust sounds,
  visuals, or game feel at runtime, mentions LiveLab / execute_code 注入 /
  热改, or asks to 落地 changes recorded from a previous live session.
---

# Live Lab（运行时协作开发）

人开着 Play 玩游戏，agent 同场用 `execute_code` 往运行中的编辑器域注入**探针**（轮询状态、打 `[LiveLab]` 日志，当 agent 的眼睛）和**补丁**（改音效、画面、手感）。Play 结束注入全部蒸发——所以每个补丁都要留下可**重放**的痕迹，人满意的改动会后**落地**成真实代码/JSON。

## 原理一句话

`execute_code` 把片段包成方法体，Roslyn 内存编译成新程序集载入 Editor 进程域（Editor 与 Play 同域），主线程同步执行——因此它能引用项目全部程序集、反射私有状态、订阅事件、当场改声改画。代价：域重载（进 Play / recompile）一切蒸发。包装解剖、生命周期表、坑目录见 [reference.md](reference.md)。

## 通道阶梯（能用上面的就不用下面的）

| # | 通道 | 用途 | 痕迹形态 |
|---|------|------|---------|
| 1 | **数据缝** | 调绑定/参数：`IAudioSystem.ApplyWorkbenchCatalog(json)`、`IVfxSystem` 同款 | live JSON 文件本身（最原生） |
| 2 | **注入钩子** | 新触发/轮询/临时接线：`EditorApplication.update` + 局部函数 | 片段文件，可重放 |
| 3 | **方法重定向** | 改现有方法行为：MonoMod detour（`Locus.Detour.dll` 已在域内） | 片段文件 + 尽快落地 |
| 4 | **可拆卸实验室** | 实验太结构化（Shader / RendererFeature / 新组件），注入装不下 | 真实代码，热键装配⇄还原 |

通道 4 不是注入，是退回正常开发：先落一票可整体拆除的 lab 代码再进 Play 对比（先例：`Assets/Scripts/VisualFxLab/` + F9 一键装配/还原，见 `Assets/Notes/画面实验室-视觉效果试装-2026-08-11.md`）。

## 会话流程

1. **开局探针**：跑 [snippets/probe.cs](snippets/probe.cs)，确认 Play 状态、残留钩子、缝可用性。域台账已存在的领域先读其规范再动手（音频 → `Assets/Notes/音频实时协作-统一台账.md`）。
2. **建实验目录**：`Assets/Notes/LiveLab~/<日期>-<主题>/`（`~` 结尾 Unity 不导入）。数据缝走 live 副本（见 [snippets/audio-workbench.cs](snippets/audio-workbench.cs)）；每个注入片段先存成该目录下的 `.cs` 文件再执行，文件头注释写 marker、意图、落地目标路径、状态。
3. **循环：人玩 → agent 改**。补丁一律幂等可重装（同 marker 先卸后挂，模板 [snippets/hook-install.cs](snippets/hook-install.cs)）；观察靠 `[LiveLab]` 日志 + read_console，必要时截图。人不满意就改参数重装同 marker；人否决就卸载并把片段头注释标 `不落地`。
4. **Play 中纪律**：不改会触发 recompile 的源码；不写正式 JSON（人明确说「保存」除外）；新素材必须登记 manifest 并清缓存（坑目录见 reference.md）。
5. **收尾**：人结束 Play 前跑 [snippets/hook-uninstall.cs](snippets/hook-uninstall.cs) 全卸。完成标准：场上无任何 LiveLab 钩子，且每个片段头注释都有状态（`待落地` / `不落地`）；域台账领域同步追加台账条目。

## 落地（Play 结束后，本会话或另开会话）

- 逐片段翻译：数据缝改动 = live JSON diff 合回正式 JSON；钩子/重定向 = 在片段头注释指明的权威接线点写真实代码（音频类新 cue 须补 `AudioCueAttribute` 声明）。
- 状态机（与音频台账一致）：`临时生效 → 待确认 →（人确认）→ 已落地`；人否决 → `不落地`。
- 验证门槛照旧：recompile 后 Console 无本次改动导致的新增 Error；涉及接口/装配改 `docs/code-map/`，行为不变量改 ADR。
- 落地后回填片段头注释状态，已落地片段可删。下次会话要恢复体验：按目录顺序重放 `待落地` 片段。

## 硬边界（能力上限）

- 只在 Editor（Mono JIT）可用；IL2CPP 构建版永远不行。
- 不能给现有类型加成员/字段、不能改序列化布局、不能动态定义 MonoBehaviour 子类；局部函数可以，类型声明不行。
- 单片段 ≤50k 字符；主线程同步执行（长循环 = 卡死编辑器）；片段之间静态不互通（跨片段状态用 AppDomain data，见 reference.md）。
- 域重载三清：进 Play、recompile 都会蒸发全部注入程序集、钩子与 execute_code history。**退出 Play 不清**——钩子会在编辑态继续 tick，所以钩子体永远先 `if (!EditorApplication.isPlaying) return;`，会话收尾必须卸载。
