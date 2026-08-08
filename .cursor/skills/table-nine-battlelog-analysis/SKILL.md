---
name: table-nine-battlelog-analysis
description: >-
  Analyze TableNine exported battle/flow logs under Assets/Notes/Logs/:
  correlate Run (sessionId/seed/runTag), Floor, Node, Battle (opIndex),
  and scan for Error/Exception/Assert. Use after a Play run to verify a
  formal/QuickTest session, to locate where a log diverged, or when the
  user mentions 终验 / 日志审查 / battlelog / corelog / 异常扫描.
---

# TableNine 战斗/流程日志分析（终验分析入口）

Play 结束自动导出至 `Assets/Notes/Logs/`（Editor 路径；Player 下为 persistentDataPath）。
本技能提供**终验分析入口**：关联 Run/Floor/Node/Battle + 扫描 Error/Exception/Assert。

## 日志落盘位置

| 轨 | 目录 | 文件前缀 |
|----|------|----------|
| BattleTrace | `Assets/Notes/Logs/OtherLog/BattleLog/` | `battlelog-…-seed<N>.json` |
| FlowTrace (CoreLog) | `Assets/Notes/Logs/CoreLog/` | `corelog-…-seed<N>.json` |
| Perf | `Assets/Notes/Logs/PerfLog/` | `perflog-…` |
| Registry | `Assets/Notes/Logs/OtherLog/RegistryLog/` | `registrylog-…` |
| **手动 Bug 快照** | Editor：`Assets/Notes/Logs/ManualBugSnapshots/`；Player：可执行文件旁 `ManualBugSnapshots/` | 目录/文件名带 `!!!AI-BUG-REPORT!!!`；必读 `!!!AI_READ_THIS_FIRST!!!.md`（含 `USER_PROBLEM_TAG`） |

试玩者 F12 →「记录log」→ 填 Tag → 保存：走 `DiagTraceManualSnapshot.Save`（先 `PerfTraceRecorder.StampUserObservation`，再导出四轨到上述快照目录）。

## 字段关联（#142 统一最低日志字段）

- **Run**：会话级 `sessionId` / `seed` / `runTag`（`QuickTest` = 快速测试，空 = 正式）。
- **Floor**：事件/op 级 `floor`（RunModel.Floor，1-based）。
- **Node**：事件/op 级 `nodeIndex`（shell 整局节点 1..24 优先，回退 RunModel 层内 0..7）。
- **Battle**：FlowTrace 事件 `refBattleOpIndex` → BattleTrace `ops[opIndex]`。

## 分析脚本（推荐）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".cursor/skills/table-nine-battlelog-analysis/scripts/scan-traces.ps1"
```

输出：每个日志文件的 runTag/seed/sessionId、floor/node 分布、op 数、以及
Error/Exception/Assert 命中（行号 + 摘要）。可选 `-Path <dir>` 指定日志目录。

## 手工命令

```powershell
# 扫描所有日志中的异常关键词（Error / Exception / Assert / Reject）
Get-ChildItem -Recurse "Assets/Notes/Logs" -Filter *.json | Where-Object { $_.Name -notlike "_*" } | ForEach-Object {
  $m = Select-String -Path $_.FullName -Pattern 'Error|Exception|Assert' -AllMatches
  if ($m) { "### $($_.Name)"; $m | ForEach-Object { $_.Line.Substring(0, [Math]::Min(160, $_.Line.Length)) } }
}

# 单局关联：取同一 sessionId 的 battlelog + corelog 各一份
$b = Get-ChildItem "Assets/Notes/Logs/OtherLog/BattleLog" -Filter "battlelog-*.json" | Sort-Object LastWriteTime | Select-Object -Last 1
$c = Get-ChildItem "Assets/Notes/Logs/CoreLog" -Filter "corelog-*.json" | Sort-Object LastWriteTime | Select-Object -Last 1
"battle=$($b.Name) core=$($c.Name)"
```

## 终验要点（#142 验收）

1. 正式局 `runTag` 为空；QuickTest 局为 `QuickTest`。
2. 事件/op 均带 `floor` + `nodeIndex`（可关联 Run-Floor-Node-Battle）。
3. `Error / Exception / Assert` 零命中（或全部为已归档的 LogAssert 预期）。
4. 节点 8 层主 / 第 3 层 Victory 应在 corelog 有 `Victory` 事件，且其 `floor`=3。

## 相关

- 自动导出：`BattleTracePlayModeExporter`（Editor）+ `InBattleManager.OnDestroy`。
- 手动导出：`DiagTraceEditorWindow`（Editor 菜单）或 DevTest 点对点。
