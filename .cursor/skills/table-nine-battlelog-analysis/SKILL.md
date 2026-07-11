---
name: table-nine-battlelog-analysis
description: >-
  Analyze TableNine diagnostic logs: BattleLog (combat), CoreLog (flow), PerfLog (presentation).
  Combat mode: damage/design numeric triangulation via BattleLog.
  Flow mode: reconstruct run timeline via CoreLog.
  Perf mode: card position/visibility/teleport bugs via PerfLog + beatId sync.
  Use when the user asks to 分析战斗日志/BattleLog、流程日志/CoreLog/FlowLog、表现日志/PerfLog、
  错位/瞬移/闪消失、一反击就死、跳过奖励、房间卡住、全流程还原、对照设计文档查战斗问题、
  或根据日志复测/改接线.
---

# TableNine 诊断日志分析（三模式）

Session 根：`sessionId` + `seed`（`DiagTraceShared`）。  
共通节奏键：`beatId`（`DiagBeatClock`）。同一 Beat 下 Core 可多事件、Perf 可少事件，**用 Beat 对齐，勿要求 1:1**。

| 层 | 落盘（权威） | 角色 |
|----|--------------|------|
| **CoreLog** | `Assets/Notes/Logs/CoreLog/corelog-*.json` | 流程 / 门禁 / 逻辑占格（原 FlowLog） |
| **PerfLog** | `Assets/Notes/Logs/PerfLog/perflog-*.json` | 卡牌世界坐标 / 显隐 / tween / BoardSnap |
| **Battle Probe** | `Assets/Notes/Logs/OtherLog/BattleLog/battlelog-*.json` | 战斗门禁 Op 深挖 |

旧顶层 `Assets/Notes/FlowLog/`、`Assets/Notes/BattleLog/` **仅历史归档**；最新以 `Logs/` 为准。  
兼容：CoreLog 分析时可顺带认旧名 `flowlog-*`（若仍存在）。

---

## 何时用 / 读哪份

| 用户意图 | 模式 | 读什么 |
|----------|------|--------|
| 伤害/反击/数值对不对 | **A 战斗分析** | BattleLog；需节奏时同 `beatId` 看 CoreLog |
| 跳过环节/房间/流程缺步 | **B 流程溯源** | CoreLog；需画面时同 `beatId` 开 PerfLog |
| 错位/空位有牌/瞬移补位/怪闪消失/消失还能打 | **C 表现溯源** | **PerfLog 为主**；同 `beatId` 对照 CoreLog 占格/意图；伤害细节回 Battle |

**硬区分**：`OccupancySnapshot`（CoreLog）= 逻辑占格登记，**不是**世界坐标。画面位置只信 PerfLog。`site` 是追责主键；无 site 的坐标变化视为插桩缺口，不臆测 Core。

导出：Play 退出 / 胜负 Notice / DevTest Keypad4 → 三件套同 session；Keypad5 同步开关三轨 Enabled。

---

# 模式 A：战斗分析

精炼流程：读 BattleLog → **数值三角对照** → 对照 Docs 规则 + Core → 分层定性 →（有疑点）EditMode 复测。

Schema 要点（每条 Op）不变：`reason` / `attacker`/`target` snap / `events` / `presentation` / `phaseBefore`→`phaseAfter`。

同局可有 PerfLog；**表现疑点切模式 C**，勿在 Battle 里找世界坐标。

### 数值维度 / Docs / Core

见原流水线与 [`references/numeric-tables.md`](references/numeric-tables.md)、[`references/test-harness.md`](references/test-harness.md)。

### 汇报模板（战斗）

```markdown
## 战斗日志结论
- 日志：`Assets/Notes/Logs/OtherLog/BattleLog/...`（seed / sessionId）
- 关键 Op：#n reason=… → …（一句话）

## 数值对照
- …

## 判定
- [ ] 数值问题 / [ ] Core / [ ] 表现接线 / [ ] 设计歧义 / [ ] 合理无问题
```

---

# 模式 B：流程溯源

读 CoreLog → 按 `events` 还原时间线 → 标缺失/`accepted=false` → 对照挂点。

Schema（schemaVersion **3**）：每条含 `beatId`；`category` / `name` / `loopState` / `phase*` / `accepted` / `refBattleOpIndex` / `payload`。

事件名与挂点：[`references/flow-events.md`](references/flow-events.md)。

乱飘/缺牌/占格冲突：先看 CoreLog `OccupancyConflict` / `HopPlan` / `OccupancySnapshot.hasDiff`；**若占格一致但画面错 → 切模式 C**。

### 汇报模板（流程）

```markdown
## 流程溯源结论
- 日志：`Assets/Notes/Logs/CoreLog/...`
- 同局 Battle：`.../OtherLog/BattleLog/battlelog-{sessionId}-seed{seed}.json`
- 同局 Perf：`.../PerfLog/perflog-{sessionId}-seed{seed}.json`（若有）

## 时间线（关键）
- #i beatId=… SetState / StartNode / …

## 判定
- [ ] 流程壳漏步 / [ ] Core 门拒 / [ ] UI 未完成 / [ ] 设计跳过 / [ ] 合理
```

---

# 模式 C：表现溯源（新）

精炼流程：

```
1 取最新或指定 perflog-*.json，记下 sessionId/seed
2 先扫 Anomaly；无则按现象选 uid 或 beatKind（OpeningDeal / PostKillDrain / CombatHit / SyncBoard）
3 按 uid 滤：Motion* / SnapSet / VisChange，盯 site + beatId
4 同 beatId 打开 CoreLog：HopPlan / OccupancySnapshot / SyncDiff / Drain*
5 比该 Beat 的 BoardSnap Open vs Close
6 定性：表现接线 / 与 Core 占格不一致 / 合理（如 FinalStateGuard 硬 Snap）
```

kind / Anomaly / site：[`references/perf-events.md`](references/perf-events.md)。

### 三类痛点速查

| 现象 | 先看 |
|------|------|
| 开局错位、该空位有牌 | OpeningDeal BoardSnap / `OrphanAtWrongAnchor` / `SnapSet` site=`Ground.Place.Snap` |
| 补位瞬移跳过缓动 | PostKillDrain：`MotionPlan` 后无完整 Begin→End，或 `SnapDuringMotion` |
| 怪闪消失仍能打 | CombatHit：`VisChange active=0` + `VisOffWhileCombatant`；site 指向退场/Guard |

### 反向三步（卡卡在错误地点）

1. 按 **uid** 滤 PerfLog → 最后一次 `SnapSet` / `MotionEnd` 的 **site** + **beatId**
2. 同 **beatId** 打开 CoreLog → 意图槽位 / HopPlan / OccupancySnapshot
3. 比 BoardSnap Open vs Close；有 `Anomaly` 直接当索引

### 汇报模板（表现）

```markdown
## 表现溯源结论
- 日志：`Assets/Notes/Logs/PerfLog/...`（sessionId / seed）
- beatId=… beatKind=… uid=…

## Anomaly
- code=… detail=…（或无）

## 证据（site）
- SnapSet/Motion/Vis：…

## Core 对照（同 beatId）
- Occupancy / HopPlan：…

## 判定
- [ ] 表现接线 / [ ] Core↔表现占格不一致 / [ ] 插桩缺口 / [ ] 合理

## 建议下一步
- …
```

---

## 用户意图分流

| 用户说 | 做 |
|--------|----|
| 分析 / 合不合理 / 汇报 | 只出简报（选对模式） |
| 修 / 改 / 落地 | 按定性改层；默认不改 Core |
| 复测 | 只跑/补测试 |
| 错位 / 瞬移 / 闪消失 | **模式 C**，不要只翻 Battle/数值表 |

## 参考

- Core 事件：[`references/flow-events.md`](references/flow-events.md)
- Perf 事件：[`references/perf-events.md`](references/perf-events.md)
- 数值查表：[`references/numeric-tables.md`](references/numeric-tables.md)
- EditMode：[`references/test-harness.md`](references/test-harness.md)
- 产出代码：`DiagBeatClock` / `PerfTraceRecorder` / `FlowTraceRecorder` / `BattleTraceRecorder` / `CardPresentationProbe`
