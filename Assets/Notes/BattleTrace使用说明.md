# BattleTrace 局内卡牌 Debug 日志

## 启用

MainScene 生产入口 [`NineGridSceneBootstrap`](../Scripts/NineGrid.Presentation/Bridge/NineGridSceneBootstrap.cs) 会自动挂载 [`BattleTraceController`](../Scripts/NineGrid.Presentation/Debugging/Trace/BattleTraceController.cs)。

Inspector 设置：

- **Trace Level**：`Off` / `Batch`（推荐日常）/ `Full`（含交互）
- **Include Interaction**：Full 模式下记录 Hover/Drag/Layout

## 产出位置

```
Application.persistentDataPath/BattleTraces/
  session_YYYYMMDD_HHmmss.jsonl
  session_YYYYMMDD_HHmmss_summary.md
```

Windows 典型路径：`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\BattleTraces\`

## 使用流程

1. Play Mode 开战，复现异常
2. 按 **F9** Flush 摘要，或退出 Play Mode 自动写入
3. 将 `*.jsonl` 与 `*_summary.md` 拖给 AI 分析

## AI 阅读顺序

1. 读 `_summary.md` 的 Violations 与 Timeline
2. 在 JSONL 中按 `seq` 追踪：`Command` → `BatchStart` → `PlanStep` → `FlowResolve` → `Snapshot`
3. 重点字段：
   - `FlowResolve.status == "fallback_noop"`：Core 已变但 Flow 静默跳过
   - `Violation.code`：`BOARD_GHOST` / `HAND_GHOST` / `HAND_ORDER_MISMATCH` 等
   - `Snapshot.tag`：`pre_batch_*` vs `post_batch_*`

## 架构说明

日志覆盖四层：Core 快照 → Actor 注册表 → Batch/Flow 播放 → 手牌布局/交互扰动。批末在 `WireAllHandActors` 之后做不变量校验。
