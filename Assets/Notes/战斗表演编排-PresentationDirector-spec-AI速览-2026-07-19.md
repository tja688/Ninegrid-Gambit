# 战斗表演编排 · PresentationDirector Spec（AI 速览）

> **用途**：给并行改代码的 AI / 人快速对齐 #2 新设计，避免在共存期踩旧路径或扩大范围。  
> **权威源**：[#2 Spec](https://github.com/tja688/Ninegrid-Gambit/issues/2) · [ADR-0001](../../docs/adr/0001-battle-presentation-unified-timeline-batch-ack.md) · [`CONTEXT.md`「战斗表演编排」](../../CONTEXT.md) · [落地方案](../../.cursor/plans/表演编排统一时间线重构_f1ba353d.plan.md)  
> **状态快照**：2026-07-19 — #3–#10 已关；#11 硬切收尾进行中（Trigger/debounce/诊断 + 删 BoardQueue 泵 + 旧进攻入口改导演）。  
> **本轮性质**：导演为唯一编排出口；Present 薄适配可残留（反击等），勿再复活 `_boardPresentationQueue` / `legacyPostPresent` / `RequestBasicAttackInternal`。

---

## 1. 一句话目标

把战斗内表演从「三套编排 + Core `RunToCompletion` 核先行 + Cards 占格双真相」收敛为：

**唯一 `PresentationDirector` 编排时间线 + 一拍一 Batch + 表演 ack 驱动前进 + 输入意图缓冲 + Core 单一占格真相。**

对标商业参考：逻辑改写与表演步骤**同流交织**，核永不大幅领先屏幕。

---

## 2. 锁定架构（改代码前先背）

| 决策 | 必须遵守 | 禁止 |
|------|----------|------|
| 唯一出口 | 已迁流程只走 Director / Timeline | 再开并行编排器、散落 async 链抢时间轴 |
| 批次锁步 | `ResolveBatch` ↔ `Present` + `OpenBatch`/`FinishBatch`；未 ack 不得下一批 | 恢复整局 `RunToCompletion` 核先行 |
| 并发 | **一条串行主线**（输入互斥只认它）+ Step 内可 fork 并行子流 + 极少旁路装饰道 | 用飘字/闪白锁死主交互；旁路占主线 busy |
| 输入 | `InputIntent`；忙时缓冲**最早一条** + `uiPick`；Phase/战败/换层**硬清空** | 忙时静默吞点击；输入回调直接写 Core/Transform |
| 命名 | 表现侧原子叫 **Step** | 用 Action 指代表演原子（Action 专属 Core） |
| 分层 | Director/Timeline/Step/Intent/Trigger 在 **Flow**；Cards **零 Core 引用** | 非内核用 QFramework；Cards 直接裁决合法性 |
| 占格终态 | Core=`BoardModel` 唯一逻辑真相；Cards=几何注册；合法性 Flow idle 裁决 | 强化 `_uidBySlot` 权威；依赖 `SyncBoardOccupancyFromCore(force)` 静默修 |
| 位置 | 编排收编时间线 + 全局租约；净土域（手牌/卡组）保留 DOTween，边界 `Evict`/`Admit` | 本轮强行统一收敛基元 |
| FX | 脉冲 `Trigger`（发即完成、可降级）；卡时序用显式 `Delay` | `await` FX 当主线完成条件 |
| 迁移 | 按流程垂直切片；PerfLog+人眼还原后再拆该流程旧路径；全迁完再硬切 | bigbang 一次删旧；未验收就拆旧 |

**回退门（ADR）**：若 Core 切批受阻 → 可退到「只统一表现侧导演」；须显式记录双真相/核先行未根治，**不得假装已完成 #2**。

---

## 3. Issue 树与阶段（并行时认准票号）

| 阶段 | Issue | 内容 | 快照状态 |
|------|-------|------|----------|
| 0 | #3 | 导演骨架 + 批次缝 + 意图 + EditMode | **CLOSED** |
| 1 | #4 | explore（或拾取）整条迁入 + 拆旧 + 验收 | ready-for-human |
| 2 | #5 | 攻击→击杀补牌旋转承重切片 | ready-for-human |
| 3a–d | #6–#9 | 用牌/帮助卡、融合、洗回、drain refill | ready-for-human |
| 4 | #10 | 占格权威退场 + idle 合法性上移 Flow | **blocked by #9**，勿提前当已完成 |
| 5 | #11 | 脉冲 Trigger 全量、诊断回放、God Object 瘦身、删净旧机制硬切 | **blocked by #10** |

父票 #2 = Spec / 总纲，不替代子票验收。

### 人眼验收要点（#4–#9）

- 正式点击走导演；**勿用 DevTest Keypad 旧攻击入口**当验收（旧 `RequestBasicAttackAtSlotAsync` 仍可达）。
- PerfLog 看 `path=director` / `DirectorTrace` / 各切片关键词；与 `legacyPostPresent` 对照可辨共存边界。
- 已知非阻塞：Drain 尾部仍可能触 `SyncBoardOccupancyFromCore(force)` → 属 **#10**；反击等非导演 drain 仍可能 `legacyPostPresent` → 属 **#11** 硬切前共存。

---

## 4. 数据流（心智模型）

```text
点击/输入 → InputIntent 入队（忙则缓冲+uiPick）
    → PresentationDirector / BattleTimeline
    → Step: ResolveBatch（请 Core 解算一批 + OpenBatch）
    → EventLog 投影为 Present / Delay / Trigger …
    → Step: Present（播表演；主线串行，可 fork 并行子流）
    → ack / FinishBatch → 时间线取下一步
```

**busy 唯一真相** = 主线在跑。散落的 `PresentationLocked` / 各 `IsBusy` 应向此收口，不要再加第三套锁。

---

## 5. 关键类型与代码落点

最小 Step 集：`ResolveBatch` · `Present` · `Delay` · `Trigger`。

| 区域 | 路径（相对 `Assets/Scripts/`） | 说明 |
|------|-------------------------------|------|
| 导演 / 时间线 | `Flow/Presentation/PresentationDirector.cs` · `BattleTimeline.cs` | 唯一出口骨架 |
| 批次缝 | `Flow/Presentation/BatchLockstepSteps.cs` · `PresentationSyncBatchGate.cs` | 接 Core `OpenBatch`/`FinishBatch` |
| 意图 | `Flow/Presentation/InputIntent*.cs` · 各 `*IntentScriptFactory.cs` | 按流程工厂组剧本 |
| 攻击 Present | `Flow/Presentation/CombatAttackPresentChannel.cs` · `AttackIntentScriptFactory.cs` | #5 承重路径 |
| 合法性（#10 方向） | `Flow/Presentation/BoardIntentLegality.cs` | idle 时 Flow 裁决；勿把权威塞回 Cards |
| 桥接 / 共存 | `Flow/InBattleManagerSingleton.cs` | strangler 中：注册 Director + 未迁路径仍可能 legacy |
| 战斗 rig | `Cards/FieldBattleManagerSingleton.cs` | 被导演收编的 Present 通道；勿复活独立编排主时间轴 |
| 诊断 | `Flow/Diagnostics/DirectorTrace.cs` | PerfLog 剧本层；验收与排障用 |
| EditMode | `Flow/Tests/Editor/*VerticalSliceTests.cs` · `PresentationDirectorTests.cs` | 假时钟/假通道测语义 |

Core 仅动「解算切批」边缘（如 `ResolveInteractiveRotation` 分拍）；**不改规则语义 / 效果 DSL / 黑盒内部**。

---

## 6. 并行改代码时的硬纪律（最容易犯规）

### 做

1. 已迁流程：新逻辑挂 Director 剧本 / Step handler / ScriptFactory，不挂回 `InBattleManager` 长 async 链。
2. 需要「等表演」：用 Timeline Step 完成语义（Continue/Finished）或显式 `Delay`，不靠 FX await。
3. 攻击 Hit Present：目标 UID 必须用 **Resolve 批捕获的 combatUid**；禁止 Hit 后再 `Resolve`（致死会卸嘲讽等规则）。Cards 侧裁决入口见 `DirectorAttackPresentTargeting`。
4. 目标点选：几何命中可在 Cards；**可否操作**在 Flow idle（`BoardIntentLegality` 方向），保持 Cards 零 Core。
5. 改完用对应 VerticalSlice EditMode +（若动真流程）PerfLog 事件序对照；人眼走正式点击。
6. 场景/组件改动走 Unity MCP；**禁止手改 `.unity`**。

### 不做

1. 不要「为了省事」在未迁/半迁流程上继续加固 `legacyPostPresent` / `_boardPresentationQueue` / 战斗 rig 独立时间轴，当作长期方案。
2. 不要在 #10 完成前删除占格镜像后**假装**双真相已死——可加诊断/断言准备，但勿拆掉仍被未硬切路径依赖的同步而不留逃生。
3. 不要提前做 #11：全量删旧三套、强行瘦空 `InBattleManager` 编排，除非子票已验收且 blocker 解除。
4. 不要做范围外：收敛基元全域统一、tick 加速、灵动 UI 具体接入、纪律 C/DAG/固定逻辑帧、表现侧 Action 命名回潮。
5. 不要让装饰旁路重新变成输入门禁或主线完成条件。

---

## 7. 共存期边界（读日志用）

| 信号 | 含义 |
|------|------|
| PerfLog `path=director` / `DirectorTrace` | 已走新导演 |
| `path=legacyPostPresent` | 仍走旧 Present 后补牌等；属共存，硬切前可能合法 |
| Keypad 旧攻击 | DevTest 旁路，**不等于**正式点击导演路径 |
| `SyncBoardOccupancyFromCore(force)` 仍触发 | #10 未完成的预期债务，不要用「再 force 一次」当新功能修复 |

未迁走旧路径 ↔ 已迁走导演：以各垂直切片开关/入口为准；交叉污染 = 同一次玩家操作既进 Director 又进 legacy 双播。

---

## 8. 测试与验收缝（只验外部行为）

单一最高层缝 = **表演导演对外行为**，其下三点同缝：

1. 时间线 / 意图 / 并发语义（Continue/Finished、缓冲、硬清空、旁路不占锁）
2. 批次 ack 顺序（未 ack 拒绝前进）
3. 按流程垂直切片还原门（PerfLog 事件序 + 人眼）

不测：收敛五次曲线数值、灵动 UI 构型、tick 加速手感；不断言 God Object 私有字段或具体 tween 实现细节。

运行时日志：Play 结束 → `Assets/Notes/Logs/`；分析用 skill `table-nine-battlelog-analysis`。

---

## 9. 词汇速查（与 CONTEXT 一致）

- **表演导演** `PresentationDirector` — 时间线唯一所有者与出口  
- **编排时间线** `BattleTimeline` — 串行主线 + 可 fork / 旁路  
- **编排步骤** Step — ResolveBatch / Present / Delay / Trigger  
- **解算批次** Batch — 一拍 Core 解算，ack 驱动下一批  
- **就位回执** ack — Present 完成 → FinishBatch  
- **输入意图** InputIntent — 可缓冲请求，不直接改状态  
- **触发脉冲** Trigger — 发即完成、可降级  
- **旁路装饰道** — 不占主线输入锁  
- **占格权威 vs 几何注册** — Core 真相 vs Cards 命中几何  

---

## 10. 给 AI 的开工检查清单

改战斗表演 / 输入 / 占格 / InBattle / FieldBattle 相关代码前：

- [ ] 我改的是哪张子票（#4–#11）？是否越权碰 blocker 未解除的 #10/#11？
- [ ] 新代码是挂在 Director 剧本上，还是在加固 legacy？
- [ ] 是否重新引入核先行、双真相 force sync、或第三套 busy？
- [ ] 表现原子是否误叫 Action？
- [ ] Cards 是否新增了 Core 引用或合法性权威？
- [ ] 是否手改了 `.unity`？
- [ ] 范围是否滑向 tick 加速 / 灵动 UI / 收敛全域统一？

若不确定：先读 ADR-0001 + 本票对应 GitHub issue body，再动代码。

---

## 11. 相关文档

| 文档 | 角色 |
|------|------|
| `#2` + 子票 #3–#11 | Spec / 切片验收标准 |
| `docs/adr/0001-battle-presentation-unified-timeline-batch-ack.md` | 根决策（accepted） |
| `CONTEXT.md`「战斗表演编排」 | 词汇 |
| `.cursor/plans/表演编排统一时间线重构_f1ba353d.plan.md` | 阶段 todos 与现状靶点 |
| `Assets/Notes/多层级代理约束收敛驱动控制-范式设计.md` | 位置收敛地基（本轮上游编排，不重做塔） |
| `Assets/Notes/灵动UI架构-spec-2026-07-17.md` | **范围外**；本轮只保证 InputIntent 出口对其开放 |
