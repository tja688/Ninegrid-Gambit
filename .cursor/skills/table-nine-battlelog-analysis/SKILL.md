---
name: table-nine-battlelog-analysis
description: >-
  Analyze TableNine diagnostic logs: BattleLog (combat), CoreLog (flow), PerfLog (presentation).
  Combat mode: damage/design numeric triangulation via BattleLog.
  Flow mode: reconstruct run timeline via CoreLog.
  Perf mode: card position/visibility/teleport bugs via PerfLog + beatId sync.
  Director lockstep mode: PresentationDirector batch/ack/intent via PerfLog Director* events.
  Use when the user asks to 分析战斗日志/BattleLog、流程日志/CoreLog/FlowLog、表现日志/PerfLog、
  导演锁步/批次ack/MainlineBusy卡死、错位/瞬移/闪消失、一反击就死、跳过奖励、房间卡住、
  全流程还原、对照设计文档查战斗问题、或根据日志复测/改接线.
---

# TableNine 诊断日志分析

Session 根：`sessionId` + `seed`（`DiagTraceShared`）。  
共通节奏键：`beatId`（`DiagBeatClock`）。同一 Beat 下 Core 可多事件、Perf 可少事件，**用 Beat 对齐，勿要求 1:1**。

| 层 | 落盘（权威） | 角色 |
|----|--------------|------|
| **CoreLog** | `Assets/Notes/Logs/CoreLog/corelog-*.json` | 流程 / 门禁 / 逻辑占格（原 FlowLog） |
| **PerfLog** | `Assets/Notes/Logs/PerfLog/perflog-*.json` | 卡牌世界坐标 / 显隐 / tween / BoardSnap |
| **RegistryLog** | `Assets/Notes/Logs/OtherLog/RegistryLog/registrylog-*.json` | CardManager 注册表专项：Delta/Audit/Checkpoint/IdleWatch |
| **Battle Probe** | `Assets/Notes/Logs/OtherLog/BattleLog/battlelog-*.json` | 战斗门禁 Op 深挖 |

> **勿搜旧目录**：`Assets/Notes/FlowLog/`、`Assets/Notes/BattleLog/` 已废弃且磁盘上通常不存在；仅当用户明确给出旧文件路径时才读历史 `flowlog-*`。

### 日志发现（分析前必做）

**文件名规则**（对齐 `DiagTraceShared.BuildFileName`）：

```
{prefix}[-{runTag}]-{sessionId}-seed{seed}.json
```

- `runTag` 为空时无中间段，如 `corelog-20260713-111111-seed1.json`
- QuickTest 时含 `-QuickTest-`，如 `corelog-QuickTest-20260713-113131-seed1.json`
- **禁止**仅用 JSON 内 `sessionId` 拼路径——必须带上文件名中的 `runTag` 段（若有）

**取最新 session（标准流程）**：

1. Glob `Assets/Notes/Logs/CoreLog/corelog-*.json`，按修改时间取最新
2. 去掉 `corelog-` 前缀与 `.json` 后缀 → **stem**（含 runTag，如 `QuickTest-20260713-113131-seed1`）
3. 同 stem 拼齐四件套：
   - `Assets/Notes/Logs/CoreLog/corelog-{stem}.json`
   - `Assets/Notes/Logs/PerfLog/perflog-{stem}.json`
   - `Assets/Notes/Logs/OtherLog/RegistryLog/registrylog-{stem}.json`
   - `Assets/Notes/Logs/OtherLog/BattleLog/battlelog-{stem}.json`

**Glob 结果为 0 时**：用 Shell 列 `Assets/Notes/Logs/` 复核，**不要**直接结论「无日志」。

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
| 导演锁步/未 ack 前进/意图缓冲/MainlineBusy 粘住/垂直切片还原门 | **E 导演锁步** | PerfLog 滤 `Director*`；同 `batchId`/`beatId` 对照 Motion/Choreo |

**硬区分**：`OccupancySnapshot`（CoreLog）= 逻辑占格登记，**不是**世界坐标。画面位置只信 PerfLog。`site` 是追责主键；无 site 的坐标变化视为插桩缺口，不臆测 Core。

导出：Play 退出 / 胜负 Notice → 四件套同 session（含 RegistryLog）；**不依赖 Keypad 导出**。RegistryLog 在 Opening.Settled / InteractionLoop.Idle 及 IdleWatch 自动打 Checkpoint。

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
- 日志：`Assets/Notes/Logs/CoreLog/...`（stem / sessionId / seed / runTag）
- 同局四件套：同 stem 拼 `battlelog-` / `perflog-` / `registrylog-`（见「日志发现」）

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

| 错位/空位有牌/瞬移/多次旋转无表现/Help 卡点不动 | **C 表现溯源** 或 **D 场地编排** | 见下 |

---

# 模式 D：场地编排（旋转 / 探求 / 拾取）

**入口**：同 session 的 `perflog-*` + `corelog-*` + `registrylog-*`。  
**工作流**：正常游玩复现 → Stop Play → 读 `Assets/Notes/Logs/`（无需 Keypad）。

1. **首看** 末条 `SessionChoreoSummary`（四轨均有）：`choreoPartialAnimateCount` / `pickupGateFailCount` / `lastPickupGate` / `lastChoreoSeqId`
2. **多次旋转无表现**：CoreLog `BoardQueue*` + `RotateClassify` → 同 `choreoSeqId` 的 `ChoreoBegin→RingShift×→Motion*→ChoreoEnd`；Anomaly `ChoreoPartialAnimate` / `MotionOverlap`
3. **乱飘/瞬移**：PerfLog 按 uid 查 `SnapSet(killedTween=1)`、`ExploreTrace` / `MotionBegin(SlotFrame.*)` 是否与 `RingShift` 同 seq 重叠；征用看 `Handoff(commandeerAdmit)`
4. **Help 卡点不动**：RegistryLog `PickupEligibility` + CoreLog `PickupGate` 最后一条 `gate=`；同 beatId `OccupancySnapshot phase=pickupClick`

> 已迁导演的流程优先 **模式 E**（`Director*` 剧本序）；模式 D 的 `BoardQueue*` 在迁移期仍可用于旧路径 bisect。

---

# 模式 E：导演锁步溯源

**入口**：同 session 的 `perflog-*`（主）+ 必要时同 `beatId` 开 `corelog-*` / Motion·Choreo。  
**实现**：`DirectorTrace` → PerfLog（**无第五轨 DirectorLog**）。事件表见 [`references/perf-events.md`](references/perf-events.md)「导演剧本层」。

### 工作流

1. 滤 PerfLog `kind` 前缀 `Director`
2. 按 `batchId` 串：`BatchOpen` → `PresentBegin` →（同 beatId 的 Motion*/Choreo*/`path=director` 切片）→ `PresentAck`
3. 意图：`IntentAccepted` / `Buffered(uiPick=1)` / `Rejected` / `Flush` / `HardClear`
4. Busy：`BusySnapshot.directorMainlineBusy` / `activeBatchId` / `bufferedIntent`
5. 卡死：`DirectorPresentStall` 或 Begin 后长期无 Ack；对照 `phase=!complete|!ack`；或 `DirectorBatchOpenRejected(dispatchReject)` 后无 `DirectorScriptAborted`（旧缺陷：Resolve 无限 Continue → MainlineBusy 粘死）

### 垂直切片验收序（还原门）

```
DirectorIntentAccepted
  → DirectorBatchOpen → DirectorPresentBegin → (Motion*|Choreo*|切片 path=director)
  → DirectorPresentAck
  → …下一批
```

失败则禁止拆该流程旧路径。迁移期对照 `path=director|legacy*`。

### 汇报模板（导演）

```markdown
## 导演锁步结论
- 日志：`Assets/Notes/Logs/PerfLog/perflog-…`（stem / sessionId）
- 意图：Accepted/Buffered/HardClear …
- 批次序：batchId=… Open→PresentBegin→Ack（或缺哪环）
- Stall / Reject：…

## 判定
- [ ] 锁步顺序坏 / [ ] Present 卡死 / [ ] 意图缓冲异常 / [ ] 切片还原偏差 / [ ] 合理
```

---

## 阶段 5 诊断清理（硬切时执行；迁移期尚未删）

旧编排机制删净后，**清旧专属挂点，保留 Diag 四轨基建**：

| 清理 | 保留 |
|------|------|
| CoreLog `BoardQueueEnqueue/Dequeue/Skip`（#11 泵已删，历史日志仍可读） | `DiagTraceShared` / `DiagBeatClock` / 四轨导出 |
| BusySnapshot 旧泵字段：`queueDepth` / `pumpRunning` | `Director*` + `directorMainlineBusy` + `DirectorTriggerPulse` |
| `path=legacyPostPresent`（已改为 `presentAdapter`） | `Motion*` / Lease / Barrier / Occupancy / Registry / Battle |
| 模式 D 中「只讲 BoardQueue」的验收路径 | 模式 E 导演剧本序 + 收敛探针 |

迁移期：**双写对照保留**，勿提前删 emitter。

---

## 用户意图分流

| 用户说 | 做 |
|--------|----|
| 分析 / 合不合理 / 汇报 | 只出简报（选对模式） |
| 修 / 改 / 落地 | 按定性改层；默认不改 Core |
| 复测 | 只跑/补测试 |
| 多次旋转/乱飘/Help 拾取失败 | **模式 D**（场地编排专查）；已迁导演则先 **E** |
| 锁步/ack/意图缓冲/MainlineBusy 粘住 | **模式 E** |

## 参考

- Core 事件：[`references/flow-events.md`](references/flow-events.md)
- Perf 事件：[`references/perf-events.md`](references/perf-events.md)（含 Director*）
- Registry 事件：[`references/registry-events.md`](references/registry-events.md)
- 数值查表：[`references/numeric-tables.md`](references/numeric-tables.md)
- EditMode：[`references/test-harness.md`](references/test-harness.md)
- 产出代码：`DiagBeatClock` / `PerfTraceRecorder` / `DirectorTrace` / `FlowTraceRecorder` / `BattleTraceRecorder` / `CardPresentationProbe`
