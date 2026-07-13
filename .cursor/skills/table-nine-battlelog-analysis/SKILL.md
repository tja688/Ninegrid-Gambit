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
| **RegistryLog** | `Assets/Notes/Logs/OtherLog/RegistryLog/registrylog-*.json` | CardManager 注册表专项：Delta/Audit/Checkpoint/IdleWatch |
| **Battle Probe** | `Assets/Notes/Logs/OtherLog/BattleLog/battlelog-*.json` | 战斗门禁 Op 深挖 |

旧顶层 `Assets/Notes/FlowLog/`、`Assets/Notes/BattleLog/` **仅历史归档**；最新以 `Logs/` 为准。  
兼容：CoreLog 分析时可顺带认旧名 `flowlog-*`（若仍存在）。

### DevTest 快速测试（QuickTest）

主菜单 `\` 键触发的快速测试局，日志会带 **`runTag: "QuickTest"`**（四轨 session JSON 均有），导出文件名含 **`-QuickTest-`**（如 `battlelog-QuickTest-…`）。CoreLog 的 `StartRun` payload 另有 `quickTestNodeOrder`、`runTagNote`。

**分析前先看是否 QuickTest**：若是，该 session **不宜用于正常数值/流程回归判定**——玩家 HP/ATK 被 cheat、全局速度 ×2、战斗内容节点乱序；表现/时序也可能与正式局不同。可用来查接线或复现 bug，但勿与设计文档或 EditMode 基线直接三角对照。详情读 session 顶部的 `runTagNote`。

---

## 何时用 / 读哪份

| 用户意图 | 模式 | 读什么 |
|----------|------|--------|
| 伤害/反击/数值对不对 | **A 战斗分析** | BattleLog；需节奏时同 `beatId` 看 CoreLog |
| 跳过环节/房间/流程缺步 | **B 流程溯源** | CoreLog；需画面时同 `beatId` 开 PerfLog |
| 错位/空位有牌/瞬移补位/怪闪消失/消失还能打 / CardManager actor 变少 | **C 表现溯源** | **PerfLog + RegistryLog 为主**；同 `beatId` 对照 CoreLog 占格/意图；伤害细节回 Battle |

**硬区分**：`OccupancySnapshot`（CoreLog）= 逻辑占格登记，**不是**世界坐标。画面位置只信 PerfLog。`site` 是追责主键；无 site 的坐标变化视为插桩缺口，不臆测 Core。

导出：Play 退出 / 胜负 Notice / DevTest Keypad4 → 四件套同 session（含 RegistryLog）；Keypad5 同步开关四轨 Enabled。RegistryLog 在 Opening.Settled / InteractionLoop.Idle 及 2s/5s/10s IdleWatch 自动打 Checkpoint+BoardSnap，无需 Keypad7。

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

# 模式 C：表现溯源

精炼流程：

```
1 取最新或指定四件套（同 sessionId/seed），缺卡 bug 优先 registrylog-*.json
2 RegistryLog：按 trigger 筛 Checkpoint（Opening.Settled → IdleWatch.*）比 registryCount / BoardSnap
3 RegistryLog：筛 RegistryDelta op=remove / Despawn，记 uid、reason、caller、tMs
4 同 beatId/tMs 开 CoreLog：OccupancySnapshot / SyncDiff / Vacate
5 画面/显隐疑点回 PerfLog：VisChange / ParentChange / Motion* / Anomaly
6 定性：Release 调用方 / 非 Release 显隐 / Core↔注册表不同步
```

kind / Anomaly / site：[`references/perf-events.md`](references/perf-events.md)。  
RegistryLog 专表：[`references/registry-events.md`](references/registry-events.md)。

### CardManager actor 变少（开局 idle 缺卡）专查

| 步骤 | RegistryLog 优先 | 回落 |
|------|------------------|------|
| 1 | 筛 `Checkpoint` trigger=`Opening.Settled`…`IdleWatch.10s`，比相邻 `registryCount` 与 `BoardSnap.cards` | 无 RegistryLog 时用 PerfLog `RegistryAudit` |
| 2 | 首条 `CombatHit`/`SyncBoard` 前：筛 `RegistryDelta op=remove` 或 `Despawn` | 有=真实 Release；无=查 B 类 |
| 3 | 每条 remove：`reason`+`caller` 反查 call site | Sync.SweepOrphan / Ground.* / Combat.* |
| 4 | 同 beatId CoreLog：`OccupancySnapshot` / `SyncDiff` / `Vacate` | Core 是否同时卸占格 |
| 5 | `RegistryAudit` ghosts vs orphans | ghosts=占格有 uid 无视图；orphans=有视图无占格 |
| 6 | remove=0 但 Checkpoint 间 `registryCount` 降或 BoardSnap 缺 uid | bypass / 非 Release（Vis/Parent/scale） |

自动锚点（无需 Keypad7）：`Opening.Settled`、`InteractionLoop.Idle`、`IdleWatch.2s/5s/10s`。  
Keypad7 `UserMark` 仍可用作可选加强，非必需。

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

### 汇报模板（表现 / 缺卡）

```markdown
## 表现溯源结论
- Registry：`Assets/Notes/Logs/OtherLog/RegistryLog/registrylog-…`（sessionId / seed）
- Perf：`Assets/Notes/Logs/PerfLog/perflog-…`（若有画面疑点）
- Core：`Assets/Notes/Logs/CoreLog/corelog-…`
- trigger=… / beatId=… / uid=…

## Registry 证据
- Checkpoint 序列：Opening.Settled → IdleWatch.*（registryCount 变化）
- remove：#n reason=… caller=… uid=…（或无）
- Audit：ghosts=… orphans=…

## Core 对照（同 beatId）
- Occupancy / SyncDiff / Vacate：…

## 判定
- [ ] A 意外 Release（沿 reason+caller 修调用方）
- [ ] B 非 Release（显隐/reparent/tween）
- [ ] C Release 有但 Core 占格仍在
- [ ] 合理 / 插桩缺口

## 建议改层
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
- Registry 事件：[`references/registry-events.md`](references/registry-events.md)
- 数值查表：[`references/numeric-tables.md`](references/numeric-tables.md)
- EditMode：[`references/test-harness.md`](references/test-harness.md)
- 产出代码：`DiagBeatClock` / `PerfTraceRecorder` / `FlowTraceRecorder` / `BattleTraceRecorder` / `CardPresentationProbe`
