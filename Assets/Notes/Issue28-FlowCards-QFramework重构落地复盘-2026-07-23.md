# Issue #28 · Flow/Cards → QFramework 表现层重构 · 落地复盘报告

> **审计日期**：2026-07-23  
> **对照基准**：[Spec · Flow/Cards 全量 QFramework 表现层重构](https://github.com/tja688/Ninegrid-Gambit/issues/28)  
> **代码基准**：分支 `重构`，HEAD `9d544487`（完成清理）  
> **上游地图**：[#17 重构地图](https://github.com/tja688/Ninegrid-Gambit/issues/17)（已关，决策移交 #28）  
> **审计性质**：对照 Spec 的源码事实复盘，不修改 Issue 状态

---

## 一句话结论

**主目标已基本达成、父 Spec 尚不应关闭。**  
程序集合并、组合根、主交互路径 QF 化、四大宿主改名收缩、`CombatHitSink` 删除、ADR 行为不变量与 EditMode 行为基线均已落地；但目录/命名空间仍复刻旧 Flow/Cards 边界、8 个 `*ManagerSingleton` 命名残留、静态 Hook 矩阵仍在、源码镜像文档未弃用，且 **#42 最终 PlayMode 验收无书面证据、#28 仍 OPEN**——按 Spec 字面，整体验收门槛未闭合。

| 维度 | 判定 |
|------|------|
| 程序集收缩为单一 `NineGrid.Presentation` | ✅ 已满足 |
| QF Controller/Command/Query/Event 主路径 | ✅ 已满足（Events 未平铺目录） |
| 废除 `CombatHitSink` + 四大巨型宿主 Singleton | ✅ 已满足 |
| 废除**所有** `*ManagerSingleton` / 业务静态 Sink | ⚠️ 部分满足 |
| 目录按角色平铺、不复刻 Flow/Cards | ❌ 未满足 |
| ADR-0001/0002 行为不变量 | ✅ 已满足 |
| Expand-contract 子票全关闭 | ✅ #29–#43 全 CLOSED |
| 最终真实场景 PlayMode 验收并关 Spec | ❌ 证据不足 / Spec 未关 |
| 弃用源码镜像文档 + 轻量 Code Map | ❌ 未满足 |

**综合完成度（主观加权）**：约 **75–80%** —— 运行时架构主线完成，合同收口与文档/终验未完。

---

## 1. 范围与时间线

### 1.1 Spec 要解决什么

敏捷期 Flow/Cards 形成单例管理器、静态 Sink、散落 Architecture 访问与跨层反向桥。统一时间线 / Batch-ack / 场地收敛 / 卡牌底盘已先落地，但执行宿主仍集中在巨型类，阻碍效果开发与 AI 独立落地。

目标：全量迁入 QFramework，收缩为单一 `NineGrid.Presentation`；场景入口 `IController`；写走 Command、读走 Query/只读 Model·System；废除 Singleton 与业务静态 Sink；拆分三大宿主；保留 ADR 深模块（Director/Timeline）。

### 1.2 执行序列（2026-07-22 单日主冲）

| 阶段 | 票 | 标题 | 关闭时刻 (UTC) | 关键提交（代表） |
|------|----|------|----------------|------------------|
| P | #29 | 建立 Presentation QF 地基 | 03:39 | `99f26004` |
| P | #30 | 测试夹具与行为基线 | 05:44 | `e5d9990d` |
| V | #31–#35 | Explore→Attack→Pickup/Use→Fusion/Drain→Shuffle | 06:19–07:49 | `1b3aa9ee`…`9756f79a` |
| V | #36–#39 | 生命周期 / 场地战斗 / 流程壳 / 输出诊断 | 08:09–08:42 | `c28d06e1`…`9ece821b` |
| C | #40 | 收缩为单一 Presentation 程序集 | 09:14 | `62475058` |
| C | #41 | 场景组合根 + 删除遗留入口（先稳） | 09:55 | `5952e5e3` |
| A | #42 | 真实场景 PlayMode 终验并关 Spec | 11:29 | **无评论/无 AC 勾选** |
| D | #43 | A1 后硬拆三大宿主 + 门禁投影 | 15:27 | `348ca2f4`…`9d544487` |

后续 #43 批次 1–7（约 `348ca2f4`→`9d544487`）完成宿主改名、System 下沉、结构护栏与文档局部清理。

**节奏特征**：expand-contract 垂直切片清晰；#41 主动把「硬拆」延后到 #43，避免场景未验就大改宿主——策略合理。代价是 **#42 与 Spec 关闭条件解耦**，终验证据链断裂。

---

## 2. 子票落地矩阵

| 票 | 状态 | 核心交付 | 关闭时已知残留 / 风险 |
|----|------|----------|------------------------|
| #29 P1 | ✅ | asmdef、`PresentationController`、`PresentationRuntimeSystem` | 过渡依赖 Flow asmdef；窄能力未暴露 |
| #30 P2 | ✅ | Fixtures + Busy/BatchAck/EventLogCommit 基线 | — |
| #31 V1 | ✅ | Explore → Command → Director | — |
| #32 V2 | ✅ | Attack + 目标/击杀 Query | — |
| #33 V3 | ✅ | Pickup/UseItem/Zone；删 ZoneOwnershipSink | — |
| #34 V4 | ✅ | Fusion/Drain Scheduler + Command | — |
| #35 V5 | ✅ | Shuffle Scheduler + Flush Controller | — |
| #36 V6 | ✅ | CardEntityLifecycle System/Command | Manager 仍为 compat shell |
| #37 V7 | ✅ | Ground/Field System + Hook | 收敛算法仍在 Cards/Convergence |
| #38 V8 | ✅ | GameFlowShell / Room / Reward / Relic | **当时** MainGameLoop 仍是权威（#43 后迁走） |
| #39 V9 | ✅ | 描述/伤害/金币/诊断 → Command/Event | 诊断旁路未全 Safe* |
| #40 C1 | ✅ | 删除 Flow/Cards asmdef；源码迁入 Presentation | 命名空间/目录仍 Flow·Cards |
| #41 C2 | ✅ | SceneRoot 挂场景；去 Instance / Sink 业务委托 | 三大宿主硬拆延期 → #43 |
| #42 A1 | ⚠️ 关但无证据 | 票面要求关 Spec | AC 全未勾；0 评论；#28 未关 |
| #43 D1 | ✅ | 四宿主改名；CompositionRoot 唯一装配；删 CombatHitSink；结构测试 | PlayMode「沿用 A1」；骷髅卡组既有 bug 不修 |

---

## 3. User Stories 对照（US1–US20）

| US | 诉求摘要 | 判定 | 证据 / 缺口 |
|----|----------|------|-------------|
| 1 | 主交互链规则与表现顺序不变 | ⚠️ | EditMode 垂直切片齐全；真人 PlayMode 无书面全链 checklist |
| 2 | 表演未就位 Core 不推进 | ✅ | `BatchAckBaselineTests`；Director↔SyncGate |
| 3 | 忙时只缓冲最早一条 | ✅ | `BusyBufferBaselineTests` |
| 4 | 场景入口统一 QF Controller | ⚠️ | 18 个 `PresentationController`；四宿主仍为非 IController 场景壳 |
| 5 | 写入只能经 Command | ✅ 主路径 | Controllers 统一 `SendCommand`；厚 Executor/Orchestrator 仍直编排 |
| 6 | 查询经 Query / 只读 Model·System | ✅ 主路径 | 16 Query；门禁分型 Query |
| 7 | Director/Timeline 保持深模块 | ✅ | `Flow/Presentation/*`，RuntimeSystem 持有 |
| 8 | Cards 可直接用 Core/QF | ✅ | 单一 Presentation asmdef 引 Core |
| 9 | 合并为 `NineGrid.Presentation` | ✅ | 旧 Flow/Cards asmdef 已删 |
| 10 | 显式组合根统一场景绑定 | ✅ | `PresentationSceneRoot` + `CompositionRoot` + `Bindings` |
| 11 | `CombatHitSink` 按语义消失 | ✅ | 类型删除；`HostContractStructuralTests` 护栏 |
| 12 | 三大巨型类拆成可测职责单元 | ⚠️ | 已拆出 System/Player/Orchestrator；Deck/Hand/Motion 仍 >800 行 |
| 13 | 每类状态唯一权威 | ✅ 关键 | busy / BoardModel / 流程壳；force-sync 自愈已禁 |
| 14 | 卡面值只经投影 Commit | ⚠️ | 主路径 Commit；`MarkFieldDead` 无投影时 `SetHealth(0)` 过渡旁路 |
| 15 | 行为契约保留，Singleton/Sink 夹具替换 | ✅ | BehaviorBaseline + 垂直切片；结构护栏防回流 |
| 16 | 票有 blocker/AC/Unity CLI | ✅（过程） | 子票结构完整；执行期大量用过 EditMode |
| 17 | 普通票编译+EditMode 即可关 | ✅ | 过程符合 |
| 18 | Spec 仅终验后关闭 | ❌ | #42 无证据；#28 OPEN；与票面「关 Spec」不一致 |
| 19 | 效果扩展点在 Command/Query/Event 与 Step | ✅ 可用 | 边界存在；文档/目录导航仍弱 |
| 20 | 保留 ADR/Spec/轻量 Code Map，弃用镜像库 | ❌ | ADR 仍 accepted；镜像库仍自称权威且今日仍在改 |

---

## 4. Implementation Decisions 落地核对

| 决策 | 判定 | 说明 |
|------|------|------|
| 目标程序集 `NineGrid.Presentation` | ✅ | 含 `.Tests` / `.Editor` |
| 目录平铺 Architecture/Controllers/… | ❌ | 有 Controllers/Commands/Queries/Systems/Setup；**缺** Architecture/Models/Views/顶层 Presentation/Diagnostics；**仍有** `Flow/`(~118 cs)、`Cards/`(~136 cs) |
| MonoBehaviour 实现 IController | ⚠️ | 输入/输出/壳 Controller 已实现；场景四宿主为 View/Controller 壳，非 IController |
| 显式组合根，Controller 不互取单例 | ✅ | SceneRoot 注入 Bindings；生产禁 `.Instance` 互取 |
| 写 Command / 读 Query / 下→上 Event | ✅ 主路径 | ~33 Command / ~16 Query；Event 散落无独立目录 |
| Director/Timeline 普通 C# 深模块 | ✅ | 由 `PresentationRuntimeSystem` 管理 |
| `IsMainlineBusy` 唯一 busy 真相 | ✅ | 只读 BindableProperty 投影；禁第二份可写标志 |
| Core `BoardModel` 占格权威 | ✅ | 几何登记非规则；force-sync 仅诊断拒绝 |
| 单底盘四卡面 + 编排内 Commit | ✅ | ADR-0002 路径保留；一处过渡 Setter |
| Sink→Command/Query/Event/诊断 | ⚠️ | CombatHitSink/业务委托已清；**16 个 Hook** + 诊断/Trigger Sink 仍在 |
| 三大巨型类按职责拆、非等体量改名 | ⚠️ | #43 完成改名+权威下沉；Deck/Hand/BoardPlayer/Orchestrator/Motion 仍巨大 |
| Expand-contract，路径切完删旧入口 | ✅ | V 票模式清晰；结构测试防回流 |
| 程序集合并宽重构保绿 | ✅ | C1 后 EditMode 446/446 记录 |
| Generated/Core/Content/DevTest/LivingUI 不重构 | ✅ 主边界 | DevTest 仅适配引用/改名（灰区可接受） |
| 更新 Code Map，弃用镜像文档 | ❌ | 见 §7 |

---

## 5. 架构落地实况

### 5.1 生产装配拓扑（已实现）

```text
PresentationSceneRoot (IController, order -100)
  └─ PresentationCompositionRoot.Install(PresentationSceneBindings)
        ├─ PresentationRuntimeSystem  → PresentationDirector → BattleTimeline
        ├─ BattleSessionSystem / CoreBatchProjection / BoardPresentationPlayer …
        ├─ GroundFieldGeometrySystem  ← GroundFieldView (锚点)
        ├─ FieldBattlePresentationSystem ← FieldBattleView (锚点)
        ├─ GameFlowShellSystem + GameFlowOrchestrator
        └─ PresentationInputStateSystem（只读门禁投影）
```

护栏：`HostContractStructuralTests`、`PresentationCompositionRootTests`、`SceneBindingBaselineTests`。

### 5.2 QF 通道规模（约）

| 层 | 规模 | 代表 |
|----|------|------|
| Controllers | 18 | Explore/Attack/Pickup/UseItem、FlowShell、Geometry、Lifecycle、Output* |
| Commands | 25 文件 / ~33 类型 | Submit*Intent、ApplyPickup、Gate、Occupancy、Handoff、输出请求 |
| Queries | 11 文件 / ~16 类型 | 分型 InputGate、目标估计、区归属、场地快照 |
| Systems | 18 | Runtime / InputState / Ground / FieldBattle / Session / FlowShell / Lifecycle |
| Events | 无统一目录 / ~14 struct | IntentRejected、*Requested、ShellStateChanged、SessionEnded |

### 5.3 仍存在的「旧形态」

**`*ManagerSingleton`（8，已无 static Instance，但仍是场景厚组件）**

- Cards：`CardManager` / `CardHand` / `CardDeck`
- Flow：`Relic` / `Selector` / `Description` / `DamageNumber` / `GoldGainFx`

**静态 Hook（16）**：Explore/Attack/Pickup/UseItem、Geometry/Battle、Zone/Lifecycle、FlowShell/Room/Reward/Relic、输出与诊断——比 CombatHitSink 轻，但仍是隐式组合面。

**诊断 / Trigger Sink**：`FlowFieldTraceSink`、`PerfTraceSink`、`*TriggerPulseSink` 等——Spec 允许可拔诊断旁路，但与「废除业务静态 Sink」需在验收时划清边界。

**巨型文件（>800 行，非 Tests）示例**

| 行数 | 文件 |
|------|------|
| 1746 | `Cards/Ground/GroundMotionExecutor.cs` |
| 1416 | `Cards/CardDeckManagerSingleton.cs` |
| 1309 | `Cards/CardHandManagerSingleton.cs` |
| 1229 | `Cards/CardAttackBasicDirectionRig.cs` |
| 1099 | `Flow/BattleSession/BoardPresentationPlayer.cs` |
| 822 | `Cards/CardManagerSingleton.cs` |
| 815 | `Flow/GameFlow/GameFlowOrchestrator.cs` |

### 5.4 Out of Scope 边界（合规）

- Core 规则 / Content / LivingUI：#28 窗口内无实质规则重写。
- Director/Timeline 算法：未重做；仅持有权与装配迁移（+ 少量只读属性如 ExternalHold）。
- 卡牌底盘产品方案：未重做。
- DevTest：仅 asmdef / DevKeys / Cheat Command 适配——符合「允许更新引用」。

---

## 6. 测试与终验

### 6.1 EditMode 保护网 — 强

- **BehaviorBaseline**：Busy 缓冲、BatchAck、EventLog→Commit→ack、ExternalHold、InputGate 矩阵、Ground 双向索引、Flow 状态顺序、SceneBinding GUID。
- **垂直切片**：Explore / Attack / Fusion / Drain / Shuffle / UseItem（原 Flow 契约迁入 Presentation.Tests）。
- **第二层**：Lease / Barrier / Commit / Handoff / FieldGeometry / OccupancyForceSyncGuard。
- **结构护栏**：禁旧四宿主名、`CombatHitSink`、`DirectorIntentRuntime`、生产本地 `new PresentationDirector`、第二份流程 `_state`、System 不暴露具体 View。

#43 关闭时记录：Presentation EditMode **357/357**，Core **92/92**。

### 6.2 #42 PlayMode 终验 — 弱 / 证据链断裂

| #42 AC | 状态 |
|--------|------|
| 真实局内验证探索、攻击/反击、拾取/用牌、Fusion/Drain、Shuffle、奖励 | ❌ 未勾；无评论 |
| busy/缓冲、Batch-ack、Commit、占格、场地运动符合 ADR | ❌ 未勾 |
| Console 无新 Error；Battle/Flow 日志无目标异常 | ❌ 无 Console 摘录；无 FlowLog |
| EditMode+PlayMode 通过；证据回填父 Spec 后关 Spec | ❌ #28 仍 OPEN |

`Assets/Notes/Logs/`（约 2026-07-22）可见 QuickTest CoreLog/PerfLog/BattleLog：  
- CoreLog 可含 Pickup/Drain/Lease 等，**未必覆盖 Explore/CombatAttack 全链**；  
- 同会话 BattleLog 常仅 `StartNode` 一条；  
- **无 FlowLog**；  
- **Notes 内无 #42 验收报告 / checklist**。

#43 关闭评论写「PlayMode 回归沿用 A1」——在 A1 本身无书面包的情况下，这是**口头继承，不能替代 Spec 终验**。

### 6.3 已知非阻塞问题（过程记录）

- 骷髅卡组既有 bug：#43 明确不修；`4aaf54d9 骷髅卡组还是有bug` 等提交提示回归风险需基线对照，而非本 Spec 新债。
- V8/V9 曾经 worktree 并行后合入并清理——与仓库 no-worktree 规则冲突的历史过程，当前工作区已回到单根。

---

## 7. 文档状态

| 项 | 现状 |
|----|------|
| ADR-0001 / ADR-0002 | `status: accepted`，与实现一致 |
| Spec #28 | **OPEN**，无终验回填评论 |
| `Assets/Docs/九宫牌局-代码文档/` | Spec 要求最终**弃用**；实际仍自称「代码现状权威」；2026-07-23 仍有提交（`9d544487`） |
| `现状结论.md` | 部分更新（宿主壳名），仍写「Cards ↛ Core」等过期分层 |
| `程序集与依赖.md` | **严重过期**（仍画分程序集 Flow/Cards） |
| 独立轻量 Code Map | **缺失**；「轻量 Code Map」自称仍挂在镜像树内 |

**文档结论**：决策层（ADR）健康；导航层（镜像库）与 Spec US20 **冲突**——继续改镜像会加大 AI/人类误读成本。

---

## 8. 分维度评分卡

| 维度 | 权重 | 得分 | 说明 |
|------|------|------|------|
| 程序集与依赖拓扑 | 高 | 9/10 | 单一 Presentation；命名空间未收 |
| QF 通信范式（主路径） | 高 | 8/10 | Command/Query 齐；Hook/Events 目录弱 |
| 反模式清除（Sink/Singleton） | 高 | 7/10 | CombatHitSink+四大宿主完成；8 Singleton 名+Hook 残留 |
| 宿主拆分深度 | 中高 | 7/10 | 权威下沉完成；厚模块仍多 |
| ADR 行为不变量 | 高 | 9/10 | busy/ack/占格/Commit 主路径稳 |
| 测试护栏 | 高 | 9/10 | EditMode+结构测试强 |
| 终验与 Spec 闭合 | 高 | 3/10 | #42 无包；#28 未关 |
| 文档契约 | 中 | 3/10 | 镜像未弃用；依赖图失真 |
| Out-of-scope 纪律 | 中 | 9/10 | 未误改 Core/底盘/Director 算法 |
| **加权观感** | — | **~7.5/10** | 可玩可测的新地基已立；合同未封口 |

---

## 9. 残留债与风险（按优先级）

### P0 — 阻塞「关闭 #28」

1. **补齐真实场景 PlayMode 终验书面包**（对照 #42 AC）：交互链 checklist、Console 增量、Battle/Flow 日志解读；回填 #28 后关闭 Spec。  
2. **明确是否接受「#43 后无需二次 PlayMode」**：若不接受，须在当前 `重构` HEAD 重跑终验（宿主硬拆后风险更高）。

### P1 — 合同字面未完成

3. 废除或改名 8 个 `*ManagerSingleton`（至少去 Singleton 后缀，职责继续下沉 System）。  
4. 收敛/替换静态 Hook 矩阵为组合根显式接线（或文档裁定「Hook = 过渡装配，非业务 Sink」）。  
5. 目录/命名空间平铺：拆除 `Flow/`·`Cards/` 作为边界语义；补 `Events/`（及可选 Models/Views）。  
6. 按 US20 **弃用** `Assets/Docs/九宫牌局-代码文档/`，另建轻量 Code Map；停更失真镜像。

### P2 — 质量与可维护性

7. `MarkFieldDead` → `SetHealth(0)` 过渡旁路收口到 Commit。  
8. Deck/Hand/Motion/BoardPlayer/Orchestrator 继续拆深模块（非本 Spec 必须，但影响「US12 可独立理解」）。  
9. DevTest 类名 `*ManagerDevKeys` 与 Layer 资产名同步新宿主。  
10. 诊断 Sink 标清「可拔旁路」，避免效果开发误接。

### 已知接受的非本 Spec 债

- 骷髅卡组既有表现 bug（#43 基线外）。  
- 新效果内容、FaceUp 门闩、完整 DAG 等 Out of Scope。

---

## 10. 给后续工作的建议

1. **先闭合 #28，再开新效果大票**：否则「地基已好」与「Spec 未关」并存，优先级会漂。  
2. **终验以 #43 后代码为准**，不要再「沿用」无证据的 #42。  
3. **文档策略二选一并执行**：  
   - A：镜像库标 DEPRECATED + 冻结，新建 `docs/code-map/`；或  
   - B：把镜像库收缩成真正轻量 Code Map（删源码复述章节）。  
4. **残留 Singleton/Hook 单开收口票**，勿混进玩法内容票。  
5. 效果落地优先接现有 **Command / Query / `ITimelineStep` / `IPresentChannel`**，禁止新增静态业务 Sink。

---

## 11. 附录

### A. 关键路径一览（当前）

| 路径 | 入口 | QF 通道 |
|------|------|---------|
| 探索 | `ExploreInputController` | `SubmitExploreIntentCommand` + Gate Query |
| 攻击 | `AttackInputController` | `SubmitAttackIntentCommand` + 目标/击杀 Query |
| 拾取 | `PickupInputController` | `ApplyPickupItemCommand` |
| 用牌 | `UseItemInputController` | `SubmitUseItemIntentCommand` |
| Fusion/Drain | Scheduler + Resolve*Command | + Present Controller |
| Shuffle | `EnqueueShuffle*` + Flush Controller | |
| 流程/房间/奖励 | `GameFlowShellController` 等 | Shell Command/Event |
| 场地/交战 | Geometry/Battle System | Occupancy Command + View 锚点 |

### B. 审计方法

- GitHub：`gh issue view` #17、#28–#43（含关闭评论）。  
- 源码树：`Assets/Scripts/NineGrid.Presentation/` 目录与类型扫描。  
- 结构/行为测试：`Tests/BehaviorBaseline`、`HostContractStructuralTests`。  
- 文档：`docs/adr/*`、`Assets/Docs/九宫牌局-代码文档/`、`Assets/Notes/Logs/`。  
- 并行只读探索子代理交叉核对程序集、QF 范式、测试与文档三轴。

### C. 相关链接

- Spec：https://github.com/tja688/Ninegrid-Gambit/issues/28  
- 地图：https://github.com/tja688/Ninegrid-Gambit/issues/17  
- 终验票：https://github.com/tja688/Ninegrid-Gambit/issues/42  
- 宿主硬拆：https://github.com/tja688/Ninegrid-Gambit/issues/43  
- 计划：`.cursor/plans/issue_43_宿主拆分_36af1806.plan.md`

---

*本报告仅陈述对照 Spec 的落地事实与缺口，不自动关闭或重开任何 Issue。*
