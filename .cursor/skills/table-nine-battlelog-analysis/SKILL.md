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

自动导出时机：Editor Play 结束、**每局胜/负回主菜单**、失败重开轮转、Player 退出（2026-08-13 起）。
Editor 落 `Assets/Notes/Logs/`；**Development Player 落 exe 旁 `GameLogs/Logs/`**（2026-08-13 起，此前为 persistentDataPath）。
本技能提供**终验分析入口**：关联 Run/Floor/Node/Battle + 扫描 Error/Exception/Assert。

## 日志落盘位置

| 轨 | 目录（Editor；Player 把 `Assets/Notes/` 换成 exe 旁 `GameLogs/`） | 文件前缀 |
|----|------|----------|
| BattleTrace | `Assets/Notes/Logs/OtherLog/BattleLog/` | `battlelog-…-seed<N>.json` |
| FlowTrace (CoreLog) | `Assets/Notes/Logs/CoreLog/` | `corelog-…-seed<N>.json` |
| Perf | `Assets/Notes/Logs/PerfLog/` | `perflog-…` |
| Registry | `Assets/Notes/Logs/OtherLog/RegistryLog/` | `registrylog-…` |
| **Console（2026-08-13 起）** | `Assets/Notes/Logs/OtherLog/ConsoleLog/` | `consolelog-…`；抓 Warning/Error/Exception/Assert（环形上限 2000 条），查「报错在哪」优先看它 |
| **手动 Bug 快照** | Editor：`Assets/Notes/Logs/ManualBugSnapshots/`；Player：可执行文件旁 `ManualBugSnapshots/`（含 consolelog） | 目录/文件名带 `!!!AI-BUG-REPORT!!!`；必读 `!!!AI_READ_THIS_FIRST!!!.md`（含 `USER_PROBLEM_TAG`） |

试玩者 F12 →「记录log」→ 填 Tag → 保存：走 `DiagTraceManualSnapshot.Save`（先 `PerfTraceRecorder.StampUserObservation`，再导出四轨到上述快照目录）。

## 字段关联（#142 统一最低日志字段）

- **Run**：会话级 `sessionId` / `seed` / `runTag`（`QuickTest` = 快速测试，空 = 正式）。
- **Floor**：事件/op 级 `floor`（RunModel.Floor，1-based）。
- **Node**：事件/op 级 `nodeIndex`（shell 整局节点 1..24 优先，回退 RunModel 层内 0..7）。
- **Battle**：FlowTrace 事件 `refBattleOpIndex` → BattleTrace `ops[opIndex]`。

## Rhythm 诊断轨（#206：节奏 / 翻面 / 敌方行动裁决）

CoreLog（FlowTrace）`category=Rhythm`，由 `RhythmFaceFlowTraceBinder` 全互动链扫描（含拾取/点空/翻开，不只交战）：

| name | 关键 payload | 含义 |
|------|--------------|------|
| `CardFaceChanged` | uid/defId/faceUp/action(FlipCard·ConcealFace·RevealFace)/sourceDefId/cause | 谁在何时把谁翻向哪面 |
| `ActionCountdownChanged` | uid/defId/delta/remaining | 卡级共享倒计时轨迹 |
| `RhythmFireOpened` | uid/defId | 卡级开火窗（ADR-0038） |
| `EnemyActionVerdict` | uid/defId/verdict/detail/patternFires/remaining | 敌方行动裁决：`roster`（报名名单）/`fired`/`voidPosition`/`voidActionBanned`/`skipFaceDown`/`skipInvalid`/`abortAvatarDown` |
| `CounterVerdict` | uid/defId/verdict/detail | 交战反打裁决：`firstStrike`/`counterScheduled`/`skipBanned`/`skipAvatarDefeated`/`skipInvalid`/`skipNoChannel`/`rejected`；`skipBanned` 的 detail 带 `banSources=…`（禁反击修正来源，近战怪出现即异常，如按 uid 挂的修正跨局泄漏） |

`category=CombatSummary` 的 `EffectTriggered` 自 #206 起覆盖**全部**互动链（此前只有交战链）。

## 遗物挂载轨（2026-08-12 起，查「遗物选了没效果」必读）

`category=CombatSummary`，由 `RhythmFaceFlowTraceBinder` 从 EventLog 翻译：

| name | 关键 payload | 含义 |
|------|--------------|------|
| `RelicGranted` | defId/route(grant·reactivate)/action | 遗物入装备栏；`route=reactivate` 表示 StartNode 自愈重挂曾发生（说明之前挂载被打断过） |
| `RelicMountAudit` | defId/declared/mounted/modifiers/detail | 挂载审计四数核对：`declared`=申报装配数、`mounted`=实挂实例数、`modifiers`=修饰符数。detail 尾部 `mods=[…]` 列出每条修饰符形态（stat/op/value@层，带条件的标 `[cond]`）。**mounted < declared 中 Implemented 数 → 有装配没挂上；`kind:Modifier` 遗物 modifiers=0 → 空挂死实例** |
| `ConditionalModifierAudit` | uid/defId/source/active/route(initial·flip)/detail | 条件修饰符激活态采样（2026-08-13 起）：`route=initial` 挂载后首见、`flip` 激活态翻转；detail 带 `hp=X/有效上限` 快照。**带 `[cond]` 修饰符的遗物全程只有 initial(active=0)、没有 flip → 条件从未满足（判定口径或内容配错）** |
| `PipelineFault` | actionName/depth/detail | ADR-0047 熔断遏制（深度/总量超限或动作 Apply 异常）。detail 含异常类型名；出现即为待修 bug，紧邻的 RelicGranted/RewardChosen 极可能受害 |

审「遗物没效果」流程：先查该局 `RelicMountAudit`（挂载层）→ 条件修饰符类（detail 有 `[cond]`）查 `ConditionalModifierAudit`（条件层，激活/翻转轨迹）→ 再查 `EffectTriggered`（开火层，OnNodeStart 类遗物每关开始应有 sourceDefId=该遗物的记录）→ 最后查 `BaseStatModified`/伤害/护甲事件（结算层）。各层都在即为生效；哪层缺失即为断点。`PipelineFault` 任何出现都按 Error 同级处理。

**HP 百分比条件口径**（2026-08-13 修正）：`HpBelow` 类条件分母＝**有效** MaxHp（含遗物 MaxHp 修饰，与 UI/HealAction 同源）；「低于」为严格小于（恰等于阈值不触发）。

## BattleTrace 交战 op reason（2026-08-12 起）

battlelog `CombatHit` op 带 attacker/target 双快照（此前 Intent 路径 attacker 恒为 null，且怪→玩家批完全不落 op）：

| reason | 含义 |
|--------|------|
| `IntentCombatHit` | 玩家命中怪（attacker=avatar） |
| `IntentCounterHit` | 交战反击（怪未被击杀后的反打，attacker=怪） |
| `IntentFirstStrike` | 怪物先手（玩家点击交战但怪先出手） |
| `EnemyVolleyStrike` | 敌方行动阶段开火且造成伤害的批 |

审「怪不反击」：先在 corelog 查 `CounterVerdict`，`skipBanned` 直接给出禁反击来源；再对照 battlelog 是否有对应 `IntentCounterHit` op。

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
