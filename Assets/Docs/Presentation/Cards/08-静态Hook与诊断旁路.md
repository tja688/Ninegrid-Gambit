# 08 · 静态 Hook 装配缝与诊断旁路

> 权威代码事实文档 · 生成于 2026-08-12 · 覆盖 Cards 根目录全部 15 个 `*Hook`、4 个 `*TraceSink` 与 `CardPresentationProbe`，共 20 个 .cs。

## 职责综述

历史上 `NineGrid.Cards` 与 `NineGrid.Flow` 是两个程序集（现已合并进 `NineGrid.Presentation`，但目录与命名空间保留）；为避免 Cards→Presentation/Flow 的环依赖，跨界通信全部走**静态委托装配缝**：`*Hook` 是「场景视图就绪 → 通知 Controller 装配」与「输入提交 → Controller 内 IntentIntake」的窄接线，`*TraceSink` 是 Cards→Flow 的诊断旁路（失败一律吞掉，不阻塞游戏路径），`CardPresentationProbe` 是打点门面（经 `PerfTraceSink.Record` 汇入 PerfLog）。code-map 明确：这些是**装配缝，不是业务 Sink**——`CombatHitSink` 式的跨层读写规则状态已被禁止，新交互一律走 Command/Query/Event/System。

## 关键类型表

### 视图装配 + 意图提交 Hook（Controller 在 Flow/Presentation 侧注册）

| 类型 | 文件 | 一句话职责 |
|---|---|---|
| `AttackInputHook` | `AttackInputHook.cs` | 交战视图就绪装配 + `TrySubmitAttack(slot)` 攻击提交（含忙时缓冲） |
| `ExploreInputHook` | `ExploreInputHook.cs` | 场地就绪装配 + `TrySubmitExplore(slot)` 空格探索提交 |
| `BoardWalkInputHook` | `BoardWalkInputHook.cs` | 场地就绪装配 + `TrySubmitBoardWalk(slot)` 跳格提交 + `IsEnabled` 门禁开关（非战斗相位才开） |
| `PickupInputHook` | `PickupInputHook.cs` | 手牌就绪装配 + `TryApplyPickup(slot)` 拾取写 Core（返回 `PickupItemPresentationResult` 表现摘要） |
| `UseItemInputHook` | `UseItemInputHook.cs` | 手牌就绪装配 + `TrySubmitUseItem(uid, targets, defId)` 用牌提交 |
| `RecycleItemInputHook` | `RecycleItemInputHook.cs` | 手牌就绪装配 + `TrySubmitRecycleItem(uid)` 回收提交（Allow/Buffer 皆 true） |
| `AvatarBoardFacingHook` | `AvatarBoardFacingHook.cs` | 场地就绪后装配盘面 Avatar 朝向 Controller |

### 跨 Manager 解析与状态桥 Hook

| 类型 | 文件 | 一句话职责 |
|---|---|---|
| `CardEntityLifecycleHook` | `CardEntityLifecycleHook.cs` | 三 Manager（Cards/Hand/Deck）显式绑定与跨域解析（CardsOrNull/HandOrNull/DeckOrNull）+ Release 通知 |
| `CardZoneOwnershipHook` | `CardZoneOwnershipHook.cs` | Core 区域归属只读桥：`CoreSaysItemSlots/DrawPile(uid)`（表现层接线到 Query） |
| `GroundFieldGeometryHook` | `GroundFieldGeometryHook.cs` | 场地视图宿主桥：`FieldOrNull()` 全 Cards 区取场地的唯一入口 |
| `FieldBattlePresentationHook` | `FieldBattlePresentationHook.cs` | 交战视图宿主桥：`BattleOrNull()` |
| `DamageNumberHook` | `DamageNumberHook.cs` | 伤害飘字入口：总伤/治疗/血伤/甲伤四口，amount≤0 静默；接线为 QF Event |
| `DescriptionDisplayHook` | `DescriptionDisplayHook.cs` | **已退役** no-op 壳（动态 HUD 描述 TMP 管道已砍，保留防编译断裂；禁复活） |
| `DiagnosticOutputHook` | `DiagnosticOutputHook.cs` | 诊断 Recorder Attach/Detach 接线入口 |
| `TriggerPulseOutputHook` | `TriggerPulseOutputHook.cs` | 触发脉冲 Hub 生产装配/重置入口（`ResetFxToNull` 只重置 FX 通道，音频/VFX 保持应用会话） |

### 诊断旁路 Sink 与探针

| 类型 | 文件 | 一句话职责 |
|---|---|---|
| `ChoreoTraceSink` | `ChoreoTraceSink.cs` | 场地编排诊断：Begin/EndChoreo（choreoSeqId）、ExploreTrace、BusySnapshot、Anomaly、强制收口 |
| `FlowFieldTraceSink` | `FlowFieldTraceSink.cs` | 占格/发牌/手牌/租约/栅栏诊断旁路（19 个委托口；chainId+choreoSeqId 由 Flow 侧 Enrich 注入） |
| `PerfTraceSink` | `PerfTraceSink.cs` | PerfTrace 旁路：Record 通用打点、BoardSnap 请求、Beat 开合、参战者标记 |
| `RegistryTraceSink` | `RegistryTraceSink.cs` | RegistryTrace 旁路：用户交互戳点、可疑场地释放、拾取资格审计 |
| `CardPresentationProbe` | `CardPresentationProbe.cs` | 打点门面：Motion/Snap/Vis/Spawn/Registry/RingShift/Lease/Barrier/Dust/BattleBind/DeathCallback 等全部标准化 kind |

## 核心流程与数据流

### 1. 装配方向（谁 wire 谁）

1. **视图 → Hook → Controller**：场景视图 Awake 时调 `RequestWire(this)`（如 `GroundFieldView.Awake` 依次 wire Geometry/Explore/BoardWalk/AvatarFacing；`FieldBattleView.Awake` wire FieldBattle/Attack；`CardHandManagerSingleton.Awake` wire UseItem/Pickup/RecycleItem）。
2. **Controller → Hook 委托**：Flow/Presentation 侧 Controller 在 `RuntimeInitializeOnLoad`/`OnBind` 把 `WireController`、`TrySubmit*`、`Resolve*` 等委托装上；`RequestWire` 内部保存视图解析闭包并回调 `Wire`，Controller 借此登记 QF System 绑定。
3. **提交方向**：Cards 侧只调 `TrySubmit*`/`TryApply*`——真正的 IntentIntake 两轴裁决、ExternalHold、Core Command 全在 Controller（ADR-0004 唯一收口）；返回 bool/摘要告知「接纳（含缓冲）与否」。
4. **flush 回流**：忙时缓冲的意图由 Director flush 后经 Flow 侧 `PickupIntentFlushHook`/`RecycleItemIntentFlushHook`（定义在 Flow，此处只是消费点）回调手牌管理器承接表演（见 04 篇）。

### 2. 诊断链路

- `CardPresentationProbe.Emit` → `PerfTraceSink.Record`（Flow `PerfTraceRecorder` 注册）→ PerfLog JSON；坐标厘米级量化（`FormatXy`）。
- `ChoreoTraceSink`：`SafeBeginChoreo` 返回 choreoSeqId，供 Motion 打点关联；`SafeEndChoreo(outcome, planned, actual)` 配对；`SafeForceCloseOpenChoreos` 供合体等复杂流程兜底收口。全部 Safe* 包 try/catch 吞异常。
- `FlowFieldTraceSink`：占格冲突/Vacate/发牌 Attempt/Result/手牌生命周期/拾取门禁三连（Attempt/Gate/Success）/旋转分类/纪律 B/租约/栅栏/交接——是战后日志审查（battlelog 分析 skill）的核心数据源。
- 所有 Sink 提供 `ClearHandlers()`，由 Flow 诊断 Recorder Detach 时清空。

## 对外通信面

这一子域**就是** Cards 区的对外通信面本体：Cards 目录代码不 `using` Flow/Presentation/Core 的宿主类型（个别文件只读 Core 纯数据枚举如 `AttackPattern`），一切跨界都经此处委托。禁令（code-map）：不得新增业务静态 Sink；`DescriptionDisplayHook`、`GroundSlotHitProxy` 等退役壳不得复活为通路。

## 关联 ADR

ADR-0004（输入唯一收口——Hook 只是运输，不裁决）、ADR-0020（简要解释/Notice 通路不走 DescriptionDisplayHook）、ADR-0003（诊断关联层 chainId/choreoSeqId）、ADR-0006（输入提交源头是 PointerHitRouter 体系）。

## 不变量与坑

- **Hook 无裁决权**：任何门禁逻辑写进 Hook 都是违规；`BoardWalkInputHook.IsEnabled` 是唯一的「开关」型委托，其真相仍在 Flow 侧维护。
- **委托可空是常态**：全部调用点都 `?.Invoke` 或判空告警——场景加载顺序/测试夹具下 Hook 未装配必须安全降级。
- **OnDestroy 反注册要比对引用**：`CardHandManagerSingleton.OnDestroy` 只在 `Notify` 仍指向自己的方法时置 null（防止销毁旧实例误清新实例的接线）。
- **诊断绝不抛入玩法**：Sink/Probe 全链 try/catch 吞异常；写诊断代码时保持该纪律。
- **`DamageNumberHook.RequestSpawn*` 的 EnsureWired 前置**：先确保 Hook→Event 接线已安装再发（懒装配场景下防丢首条飘字）。
