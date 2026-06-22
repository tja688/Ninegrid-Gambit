---
name: table-nine-adapter-crafting
description: >-
  Build TableNine/NineGrid presentation adapters that connect human-authored
  DOTween Animation / DOTween Timeline performances to the Core event + snapshot
  contract. Use when the user says they finished a 表演/动效 (九宫格旋转、发牌补位、
  UI 弹出、按钮扰动 等) and asks AI to 做适配器 / 对接 / 接入 it to game logic, or to
  map kernel events to an existing performance.
---

# TableNine 适配器制作

人用 DOTween 手搓表演，AI 用适配器把表演接到内核。适配器是**翻译器**，不是表演本身。

## 核心诉求（最高优先级，所有取舍以此为准）

> 我有的才表现，我不看内核发了多少事件，我只看我需要表现的，多的我就自己吃掉，少的就去内核补。

- **表现以人的设计为准。** 最终游戏表现按人的设计来，不迁就内核发了多少。
- **适配器 = 翻译器。** 把"固定的、用来给人看的、在尝试时锁死的表演"和"真实游戏发出的信息"进行翻译。
- **用适配器去"控制"锁死的表演。** 人可以把表演做死（写死数量/坐标/锁进 2 秒 Timeline/绑死 Animator Trigger），适配器负责用真实内核数据去驱动它、让它随实际需求变动。

## 翻译三原则

- **多了我吃掉**：内核事件或字段超出表现所需 → 适配器忽略它，不强行演出多余的"枝丫"。
- **少了去补**：表现需要内核没给的信息 → 去 Core 加事件/字段，**不要在表现层造假数据**。
- **对不上就问**：映射有歧义、要舍弃哪种表现拿不准时，用 AskQuestion 让人拍板，别替人决定。

## 轻量协作工作流（按需，别死板）

1. **听描述**：人会说做了什么表演、用什么做的（DOTween Animation/Timeline、是否带音效）、入口在哪、想表达什么游戏场景。
2. **找事件**：AI 翻内核契约（见下方地图），列出候选事件/字段。
3. **对齐映射**：逐条用 AskQuestion 问"内核有 X、你的表现是 Y，怎么对应？"——由人选；多余的剪掉、缺的提请补内核。
4. **写适配器**：一条 `PresentationInstruction` → 驱动人的表演；同 `ActionId` 的指令编排为并行；只回所需，批末发 `PresentationFinishedCommand`。
5. **兜底**：批次播完用 `CoreViewSnapshot` 把演员对齐到权威终态，保证即便动画被压缩/跳过也最终正确。

## 内核契约地图（AI 去哪找事件）

- 事件类型枚举 `CoreEventType` — [Assets/Scripts/NineGrid.Core/Domain/CoreEnums.cs](Assets/Scripts/NineGrid.Core/Domain/CoreEnums.cs)
- 事件载荷字段 `CoreGameEvent`（`CardUid` / `FromSlot` / `ToSlot` / `Amount` / `Delta` / `ActionId` / `SourceDefId` / `Cause` …）— [Assets/Scripts/NineGrid.Core/Domain/Events/CoreGameEvent.cs](Assets/Scripts/NineGrid.Core/Domain/Events/CoreGameEvent.cs)
- 事件→指令映射（`InstructionKind` / `Category` / `LocksInput` / `RequiresPlayback`）— [Assets/Scripts/NineGrid.Core/Presentation/PresentationEventMap.cs](Assets/Scripts/NineGrid.Core/Presentation/PresentationEventMap.cs)
- 批次与权威快照 — [Assets/Scripts/NineGrid.Core/Presentation/PresentationBatch.cs](Assets/Scripts/NineGrid.Core/Presentation/PresentationBatch.cs)、[Assets/Scripts/NineGrid.Core/Presentation/CoreViewSnapshot.cs](Assets/Scripts/NineGrid.Core/Presentation/CoreViewSnapshot.cs)
- 输入锁 / 播完握手 — `IPresentationSyncSystem` + `PresentationFinishedCommand`

## 接入约定（演员-锚点）

- **演员按 `CardUid` 解析、锚点按 `SlotId` 解析。** 演员是绑定 uid 的视图，锚点是固定坐标；卡在锚点之间动，空格无演员则不动。
- 人锁死的表演由适配器用真实数据**驱动 / 裁剪**：循环真实的那一组卡，而不是表演里写死的数量；把写死坐标替换成锚点。
- **DOTween 优先**（可打断、可变速，契合批次回放）；Timeline / Animator 留给不可打断的展示性演出（如入场、大招）。

## 边界

- 适配器**不含游戏逻辑**；任何状态变更只发 Command。
- **落盘约定（本项目）**：适配器脚本放在
  `Assets/Scripts/NineGrid.Presentation/Adaptors/`，命名空间
  `NineGrid.Presentation.Adaptors`，类名以 `Adaptor` 结尾（如
  `TableNineCardDeckAdaptor`）。与盒子③ `Performance/` 严格分离——适配器只调度，
  不含 DOTween 手感编排。
- 不手改 `.unity`，相关改动走 Unity MCP。
- 不自动扩展到人没要求的表演；只接当前这一个作品。
- 落地后按 AGENTS.md 触发 Unity 刷新、读 Console 排错。
