# VFX 系统（Cue/State 管线 + 四类播放器 + 诊断）

> 覆盖范围：`Systems/VfxSystem.cs`、`Systems/VfxPulseContracts.cs`、`Systems/VfxStateContracts.cs` + `Systems/Vfx/` 全部 24 个文件，共 27 个。
> 结构总纲见 ADR-0040：**Cue/State（语义）→ Binding（`Resources/VFX/vfx_bindings.json` 单一决策真源）→ Player（播放实现）**；与音频三层同构但身份独立。

## 职责综述

- `VfxSystem`：唯一 VFX 运行时。两条轨：**Pulse**（一次性 `RequestCue`/`ScheduleCue`）与 **Persistent State**（`SetSlot` 幂等期望槽）。安装在 `PresentationSceneRoot` 主菜单生命周期（应用会话），由 SceneRoot.Update Tick。
- 播放器四类（`VfxPlayerRegistry` 登记）：`sprite-sheet`（素材帧，Pulse+State）、`gold-flight`（程序化飞币，仅 Pulse）、`particle`（程序化 Shuriken，Pulse+State）、`projectile`（程序化弹道，仅 Pulse）。
- `VfxDiagnosticsTracker`：全生命周期诊断（Request/Resolve/Create/Start/Complete）+ 帧统计 + 峰值钻取 + Release 轻量计数。

## 关键类型表

### 系统与契约

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `IVfxSystem` / `VfxSystem` | `Systems/VfxSystem.cs` | VFX 运行时：Cue 解析管线、State 槽表、Tick 推进、场景清理、工作台缝、`DefaultVfxPlayerFactory`（四厂路由）与 `UnityVfxCueScheduler`（unscaled 协程排期）内嵌其中 |
| `VfxPulseStartRequest` / `VfxPresentationPlan` / `VfxPlayerStartResult` / `IVfxPulsePlayer` / `IVfxPulsePlayerFactory` / `IVfxDomainHostResolver` | `Systems/VfxPulseContracts.cs` | Pulse 播放契约：启动请求（绑定字段已折算）、批次表现计划（首达/末达，金币与弹道用）、启动结果、播放器接口 |
| `VfxStateSlotOutcome` / `VfxStateSlotResult` / `IVfxSlotOwner` / `VfxStateStartRequest` / `IVfxStatePlayer` / `IVfxStatePlayerFactory` / `IVfxPlayerFactory` / `VfxStateExitMode` | `Systems/VfxStateContracts.cs` | State 播放契约：槽结果、槽所有者（`BuildStateRequest` 提供稳定选择器）、退出模式 immediate/segment |
| `VfxCueAggregate` / `VfxWorkbenchActivePulse` / `VfxWorkbenchActiveStateSlot` / `VfxWorkbenchSnapshot` / `VfxWorkbenchApplyResult` / `VfxWorkbenchPreviewResult` | `Systems/Vfx/VfxWorkbenchContracts.cs` | Editor/Dev 工作台快照 DTO（#202） |
| `VfxLifecyclePhase` / `VfxEndReason(+VfxEndReasons.IsIssue)` / `VfxReleaseCounters` / `VfxLifecycleRecord` / `VfxFrameStats` / `VfxPeak*` / `Vfx*Aggregate` / `VfxDiagnosticsSnapshot` / `VfxInstanceDiagnosticContext` | `Systems/Vfx/VfxDiagnostics.cs` | 诊断数据契约：五阶段、13 种结束原因（5 种算 issue）、Release 计数器、快照结构 |
| `VfxDiagnosticsTracker` | `Systems/Vfx/VfxDiagnosticsTracker.cs` | 诊断实现：有界明细环(512)+帧环(300)+Binding/Player 聚合+峰值贡献者；全部阶段写 `PerfTraceKinds.VfxLifecycle*`（带 correlationId/chainId/batchId）；Release 构建 AppendRecord 内联为空 |
| `IVfxMaterialFrameLoader` / `DefaultVfxMaterialFrameLoader` | `Systems/Vfx/IVfxMaterialFrameLoader.cs` | 素材帧加载缝：默认委托 Content 侧 `VfxMaterialFrameSource`（visual_effects sheetPath → Resources.LoadAll，`spritesheet_N` 数字序） |

### sprite-sheet 播放器

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `VfxSpriteSheetPlayerFactory` | `Systems/Vfx/VfxSpriteSheetPlayerFactory.cs` | 共享池 + 帧加载器，产 Pulse/State 双实现 |
| `VfxSpriteSheetPulsePlayer` | `Systems/Vfx/VfxSpriteSheetPulsePlayer.cs` | 一次性精灵表：默认 1 循环；附着型挂域宿主父级/跟随/遮罩/排序协商，独立型挂 `VfxIndependentSpatialRoot`（Main/5000） |
| `VfxSpriteSheetStatePlayer` | `Systems/Vfx/VfxSpriteSheetStatePlayer.cs` | 持续精灵表：主段无限循环；`BeginExit(immediate)` 立拆、segment 改配 `exitLoopLimit` 有限循环后自然结束 |
| `VfxSpriteSheetPlayback` | `Systems/Vfx/VfxSpriteSheetPlayback.cs` | 纯帧推进数学：fps×speed、loopLimit(0=无限)、startOffset 先扣后播、scaled/unscaled 双时间基 |
| `VfxSpriteSheetVisual` | `Systems/Vfx/VfxSpriteSheetVisual.cs` | 池化实例视图：只改本节点 SpriteRenderer，`ResetVisual` 归零 |
| `VfxSpriteSheetVisualPool` | `Systems/Vfx/VfxSpriteSheetVisualPool.cs` | DDOL 池根 + Stack 复用；Acquire/Release/DestroyAll 均报诊断 |
| `VfxSpriteSheetPoolDiagnostics` | `Systems/Vfx/VfxSpriteSheetPoolDiagnostics.cs` | Dev-only 池事实计数（create/reuse/release/destroy，256 条环） |

### gold-flight（#203）

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `VfxGoldFlightPlayer` + `GoldFlightTiming` | `Systems/Vfx/VfxGoldFlightPlayer.cs` | 程序化飞币：爆发→惯性滑散→加速吸入三段位姿；`GoldFlightTiming` 纯数学（可视币数 clamp 1..24、软窗 1.0s/硬上限 1.65s 压缩、catch-up warp ≤2.15）；一次性返回 `VfxPresentationPlan`（首达/末达）供 HUD 数字窗口；终点/图标吞噬经 `IGoldHudDomainHost`（`GoldHudDomainHost.Instance`，Flow 侧）；币面 `icons_full_32_9`（旧 `VFX/GoldFlightCoin` 回退），Unlit 材质防 Lit 全黑 |
| `VfxGoldFlightPlayerFactory` | `Systems/Vfx/VfxGoldFlightPlayerFactory.cs` | 仅 Pulse；State 请求直接失败 |

### particle（程序化粒子）

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `VfxParticlePresetLibrary` + `VfxParticlePreset` + `VfxParticleShape/SizeCurve` | `Systems/Vfx/VfxParticlePresetLibrary.cs` | 预设参数真源（与 Content 侧 `VfxParticlePresetIds` 一一对应，21 Pulse + 6 Loop，契约测试护栏）；世界单位参照卡面 3.8×4.9 / 格距 5×5.5；语义色对齐血伤 #CB3834、护甲 #5E7E74 |
| `VfxParticleEmitterRig` + `VfxParticleRigParams` | `Systems/Vfx/VfxParticleEmitterRig.cs` | 运行时装配 ParticleSystem（emission/shape/color/size/velocity/rotation/noise/renderer 全程序化）；`TryResolveMount` 统一解析附着/独立挂接与排序（独立型 Main/5000+delta）；`IsFinished` 带暂停态兜底、`IsDrained` 供 State 排空 |
| `VfxParticleTextureBank` | `Systems/Vfx/VfxParticleTextureBank.cs` | 程序化贴图（SoftDot/Pixel/Spark/Ring/Shard）× 混合（Alpha=Sprites/Default、Additive=`NineGrid/VFX/ParticleAdditive`）共享材质缓存，全部运行时烘焙 |
| `VfxParticlePulsePlayer` | `Systems/Vfx/VfxParticlePulsePlayer.cs` | Pulse：非 loop 预设一次爆发；绑定字段映射 fps=数量覆盖 / scale=缩放 / speed=模拟速度 / tint=乘色 / startOffset=发射延迟；15s failsafe |
| `VfxParticleStatePlayer` | `Systems/Vfx/VfxParticleStatePlayer.cs` | State：loop 预设持续发射；segment 退出=停发射等 `IsDrained`（`exitLoopLimit` 不适用） |
| `VfxParticlePlayerFactory` | `Systems/Vfx/VfxParticlePlayerFactory.cs` | Pulse+State 双能力工厂 |

### projectile（弹道，2026-08-12）

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `VfxProjectilePresetLibrary` + `VfxProjectilePreset` + `VfxProjectilePathStyle` | `Systems/Vfx/VfxProjectilePresetLibrary.cs` | 8 条弹道预设真源（magic_bolt/arrow_shot/fireball/void_orb/spark_zip/bone_shard/venom_glob/gleam_streak）：直线/弧线/摆动三轨迹 + 缓动偏置 + 齐射错峰散布 + 弹头/柔光/拖尾/起手/命中参数（爆点复用粒子预设库 + Tint 染色） |
| `VfxProjectileFlightRig` + `VfxProjectileRigParams` + `VfxProjectileSpriteBank` | `Systems/Vfx/VfxProjectileFlightRig.cs` | 弹道装配：每发=弹头 Sprite（朝向/拉伸/自旋/发光层）+ 世界空间 rateOverDistance 拖尾 + 起手/命中爆点（复用 `VfxParticleEmitterRig`）；`FirstArrivalDelay/LastArrivalDelay` 供表现计划 |
| `VfxProjectilePulsePlayer` | `Systems/Vfx/VfxProjectilePulsePlayer.cs` | Pulse：源=`PositionSnapshot`、靶=`TargetPositionSnapshot`（缺靶退化为向右前方演示飞行供工作台预览）；命中时刻经 `VfxPresentationPlan` 回传；fps=弹道数覆盖(1~8)、speed=时间倍率 |
| `VfxProjectilePlayerFactory` | `Systems/Vfx/VfxProjectilePlayerFactory.cs` | 仅 Pulse（弹道天然一次性） |

### 空间

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `VfxIndependentSpatialRoot` | `Systems/Vfx/VfxIndependentSpatialRoot.cs` | 独立型 VFX 的 DDOL 世界锚点根；不触碰卡级变换塔；`SubsystemRegistration` 重置静态（DisableDomainReload 兼容） |

## 核心流程与数据流

### Pulse 请求管线（`VfxSystem.RequestCueCore`）

```
BeginCorrelation + RecordRequest（诊断五阶段之一）
→ Catalog.TryResolveCueStrict（五维稳定选择器最高特异性；同分歧义 → InvalidBinding）
→ !Enabled（非预览）→ Suppressed
→ VfxPlayerRegistry.SupportsPulse(playerId) 否 → PlayerUnavailable
→ Attached 且域宿主缺/不可用（可经 IVfxDomainHostResolver 二次解析）→ DomainUnavailable
→ AcceptSpatial：Independent 绑定剥掉 DomainHost（保留位置快照/Amount/靶点）
→ 最短间隔（非预览）→ Suppressed("minimum interval")
→ BindingDelaySeconds>0 → UnityVfxCueScheduler（WaitForSecondsRealtime，unscaled）延迟后 StartResolvedCue，
   立即返回 Played（无 InstanceId）
→ StartResolvedCue：工厂造播放器 → 素材型播放器走变体池（加权随机、默认避免立即重复；
   程序化 gold-flight/projectile 不走变体，particle/projectile 的 materialKey=预设 ID 仍经
   VfxPlayerRegistry.UsesMaterialVariantSelection 分支）→ player.StartPulse
   → 失败 BackendFailure；成功登记 ActivePulseInstance + RecordStart，
     结果携带 PresentationPlan（gold-flight / projectile）
```

### State 槽语义（`SetSlot` / `ClearSlotIf` / `ReleaseOwner`）

- 槽键 = `(owner 引用, slot 字符串)`；`SetSlot(owner, slot, desiredState)`：
  - desiredState 空 → 清槽（`SlotCleared` 退出）；
  - 同值且未在退出 → `NoOp`；
  - 新值 → 旧槽 `BeginStateExit(SlotReplaced)`（immediate 立拆 / segment 移入 exiting 列表按 `exitLoopLimit` 播完）→ `StartResolvedState`（解析/门禁流程与 Pulse 同构，走 `TryResolveStateStrict`）。
- `ClearSlotIf(owner, slot, expectedState)`：条件清除——槽当前状态≠expected 时 `NoOp`（较早异步流程不得误停后来者）。
- `ReleaseOwner(owner)`：清该 owner 全部槽（`OwnerReleased`）。
- 持续状态**不走** Pulse 冷却/排期。

### Tick 与生命周期

`Tick(deltaTime)`（SceneRoot 每帧驱动）：

- ActivePulse：附着型宿主丢失 → `Cancel` + `AttachedHostLost`（**正常结束**，非 issue）；`player.Tick` 返回 true → `NaturalComplete`。
- ActiveStateSlots / ExitingStateSlots：同上；退出中槽播完移除。
- 帧末 `mDiagnostics.OnFrameEnd(activeCount)`：帧统计 + 会话峰值捕获（含贡献者列表）+ Binding/Player 聚合峰值。

`ClearSceneInstances()`（局内装配 Shutdown 时调）：**只清附着型** Pulse 实例与 State 槽（`SceneExit`）；独立型（爆点、飞币）接纳后自然播完，不随场景清理中断（ADR-0040 空间所有权）。

### 工作台缝（Editor/Dev，#202）

`GetWorkbenchSnapshot`（History+ActivePulses+ActiveStateSlots+聚合+诊断快照）、`ApplyWorkbenchCatalog`（严格 TryFromJson 失败保旧 catalog；成功清冷却/变体记忆并 `RebuildActiveStateSlotsAfterCatalogApply`——把在场持续槽按新 Binding 安全重建）、`PreviewWorkbenchBinding`（cue 直接播/延迟排期；state 用内部 `VfxWorkbenchPreviewOwner` 假 owner 占 "preview" 槽）、`ClearWorkbenchStateSlot`（按 ownerLabel+slot 反查清除）。

### 诊断（#194）

`VfxDiagnosticsTracker`：五阶段记录（Request/Resolve/Create/Start/Complete）+ `RecordTerminalFailure`（解析失败类直接合成 Complete）；issue 五类（Unbound/InvalidBinding/PlayerUnavailable/DomainUnavailable/BackendFailure）计入 `VfxReleaseCounters`（Release 也保留）；Editor/Dev 另有明细环/帧环/聚合/峰值钻取（`GetDiagnosticsSnapshot`）。全阶段写 PerfTrace（`VfxLifecycle*`，带 correlationId + chainId/batchId/sessionId 关联）。

## 对外通信面

- **发射入口**：`TriggerPulseHub.PulseVfx(VfxCueRequest[, VfxSpatialContext])` → `VfxTriggerPulseSink`（三线装配见《01》/《07》TriggerPulseOutputController）；权威发射点在 Flow 侧（`BattleVfxCues` / `ProjectileVfxCues` / `GoldGainPresentationBinder` 等，兄弟文档）。
- **表现计划回传**：`VfxCueResult.PresentationPlan` → 金币 HUD 数字窗口（`PlayerInfoHudPresenter`+`GoldHudNumberWindow`，Flow 侧）/ 未来弹道命中对齐。
- **空间上下文**：`VfxSpatialContext`（Content 侧类型）携带语义角色/域宿主/位置快照/`Amount`（金币数）/`TargetPositionSnapshot`（弹道靶点）；运行时身份只用于本次定位，不进绑定主键。
- **域宿主**：`IVfxDomainHost`（附着父级/跟随/遮罩/坐标转换/排序边界）；金币专用 `IGoldHudDomainHost`（Flow/GoldHudDomainHost 安装）。

## 关联 ADR / Issue

ADR-0040（总纲）；#193 声明与绑定真源、#194 诊断、#196 Runtime+类型化 Pulse、#197 sprite-sheet、#199 金币迁移、#200 交付护栏、#201 Persistent Slot、#202 工作台、#203 gold-flight、#204 删旧金币壳；粒子/弹道两库见 `Assets/Notes/粒子特效库-…-2026-08-11.md` 与 `Assets/Notes/弹道特效库-…-2026-08-12.md`（非权威交付说明）。

## 不变量与坑

- **业务禁直调** `IVfxSystem.RequestCue`/`SetSlot`/播放器 `StartPulse`——一律经 `TriggerPulseHub.PulseVfx` 或既有权威发射点（#200 护栏；`VfxStructureGuardTests` 源码扫描**仍在生效**，违规会挂 EditMode 测试，见《10》）。
- **BindingKey 禁运行时 UID/Transform/坐标**；`visual_effects.json` 只是 Editor 素材索引，不参与运行时默认值合并。
- **播放器不改共享视觉所有权**：相机、Canvas、卡级 SortingGroup、L0–L3 塔一律不碰；附着排序经域宿主 `TryGetSortingBounds` 协商，独立型固定 Main/5000(+delta)。
- **独立型不随宿主死亡中断**；停用绑定只抑制后续请求，不强杀在播实例。
- `AttachedHostLost` 是正常结束不是故障；排查"特效消失"先看 EndReason 分类。
- 绑定延迟路径立即返回 `Played` 但 `InstanceId` 为空——调用方不能假设拿得到实例句柄。
- particle：loop 预设只许 State、一次性预设只许 Pulse（播放器启动时硬校验）；projectile 挂 State 绑定按登记表直接 `PlayerUnavailable`。
- `VfxSystem` 的 Tick 依赖 SceneRoot.Update（仅局内 mInstalled 时执行）——见《01》的主菜单 Tick 缺口备注。
- 预设表 ↔ Content `Vfx*PresetIds` 一一对应由契约测试护栏（`VfxParticlePlayerContractTests` / `VfxProjectilePlayerContractTests` 的 `PresetLibrary_MatchesContentPresetIdTable`）**仍在守护**——新增预设只改一侧会挂测试（见《10》）。
