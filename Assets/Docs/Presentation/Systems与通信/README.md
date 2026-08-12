# Presentation · Systems 与通信（权威代码事实文档）

> 本区块覆盖 `Assets/Scripts/NineGrid.Presentation/` 下**除顶层 `Flow/` 与 `Cards/` 之外**的全部 183 个 .cs 文件（2026-08-12 实测扫盘计数，含当日 ADR-0044/0045/0046 落地后的维护更新；注意 `Tests/Flow/` 是 Tests 的子目录，属本区块）。`Flow/`、`Cards/` 由兄弟区块负责：[../Flow/](../Flow/)、[../Cards/](../Cards/)。
> 定位：上线后遇到恶性 bug 时，**顺着调用链找责任方**的核心地图。以当下代码实际实现为准；长期不变量以 `docs/adr/` 为准，目录级摘要以 `docs/code-map/presentation.md` 为准。

## 分篇目录

| 篇 | 覆盖 | 何时读 |
|----|------|--------|
| [01-装配与场景绑定](./01-装配与场景绑定.md) | `Setup/`（3 文件） | 想知道"这个 System/Hook/Handler 是谁、什么时候接上的" |
| [02-输入门禁与平台守护](./02-输入门禁与平台守护.md) | 根 2 文件 + 门禁 Systems 5 + `Platform/` 2 | 点击没反应/点了两次/忙时怪态/Win 掉帧卡死 |
| [03-Systems-核心系统](./03-Systems-核心系统.md) | `Systems/` 核心 16 文件 | 会话/几何/交战/流程壳/卡实体/语言偏好的权威归属 |
| [04-音频系统](./04-音频系统.md) | `Systems/` 音频 10 文件 | 没声音/声音重复/BGM 串轨/音量不对 |
| [05-VFX系统](./05-VFX系统.md) | `Systems/VfxSystem+两契约` + `Systems/Vfx/` 共 27 文件 | 特效不播/播错位置/播不停/金币与弹道 |
| [06-Commands与Queries](./06-Commands与Queries.md) | `Commands/` 27 + `Queries/` 10 文件 | 逐条查"这个写入/读取从哪来到哪去" |
| [07-Controllers](./07-Controllers.md) | `Controllers/` 21 文件 | Hook 委托挂在谁身上、输入桥断在哪 |
| [08-Ui与Cheat](./08-Ui与Cheat.md) | `Ui/` 8 + `Cheat/` 6 文件 | 面板打不开/按钮点不中/F12 后门/场景标签本地化 |
| [09-Editor工具](./09-Editor工具.md) | `Editor/` 14 文件 | 预览/日志导出/打包/资产批处理 |
| [10-Tests现状](./10-Tests现状.md) | `Tests/` 32 文件（根 7 + `Tests/Flow/` 25） | 现存自动化测试（行为/护栏/卫生/规则回归）与验证门槛 |

---

## 通信范式全景

表现层一共只有 **6 种跨模块通信机制**。定位 bug 时先判断问题走的是哪条道，再进对应篇：

### 1. Command（写入，QF `SendCommand`）

一切"改状态"的请求。三个亚型：

- **意图型**（`Submit*IntentCommand`）：包装 `InputIntent` 交给 IntentIntake 收口，**自身不做门禁判断**；
- **Core 转发型**（`SubmitSelectRoomCommand` 等）：门禁在 Controller 层做完，Command 直转 Core Command；
- **表现写入型**（门禁四条、占格四条、ExternalHold 三条、Shell 相位等）：写表现层 System 自己的状态。

全清单与发起方/处理方 → 《06》。

### 2. Query（读取，QF `SendQuery`）

一切"只读裁决/查询"。几何快照、slot↔uid、Zone 归属、先手还击、击杀预估等 11 条 → 《06》。另有 `GetSystem<T>()` 直读只读接口（如 `IGameFlowShellSystem.State`）。

### 3. struct Event（下行广播，QF `SendEvent`/`RegisterEvent`）

System/Command 向表现消费者单向广播：`DamageNumberRequested`、`RelicHudSyncRequestedEvent`、`GameFlowShellStateChangedEvent`、`TeardownPresentationDirectorRequested`、`BattleSessionSettlementReadyEvent`/`BattleSessionEndedEvent`、`Evt_PresentationBatchOpened`、各 `*RejectedEvent`。事件类型多数定义在 `Flow/Presentation/`（兄弟区块），本区块是主要生产/消费方。

### 4. IntentIntake 两轴门禁（输入唯一收口，ADR-0004）

`IIntentIntake.Submit(intent, targetSurface)`：

- **轴一 MainlineBusy**：唯一互斥真相 = `PresentationDirector.IsMainlineBusy`，经 `PresentationRuntimeSystem.MainlineBusy`（BindableProperty）→ `PresentationInputStateSystem` 投影。忙时棋盘动作 strict-drop。
- **轴二 InputOwner**：`ChoiceOverlay > Opening > BoardSelect > ProtectedField` 四表面，`targetSurface != CurrentOwner` 即拒。
- 附加：Core 合法性（`BoardIntentLegality`）、冲动轻点（`IAccelerationSink`，预留）、模态在 ChoiceOverlay 持有时的忙时放行例外。

任何输入路径**禁止绕过**它。细节与裁决顺序 → 《02》。

### 5. 静态 Hook 矩阵（装配缝，不是业务 Sink）

约 20 个 `*Hook` 静态类（定义在 Cards/Flow 目录）是"Cards/Flow 代码 ↔ Presentation Controllers"的窄接线，存在原因是历史环依赖。纪律：

- Hook 上只挂**委托**（提交/查询/同步），由《07》的 Controller 在 `SubsystemRegistration`/`OnBind` 注册、`PresentationSceneRoot.WireHosts` 统一触发；
- **禁止新增业务静态 Sink**（跨层读写规则状态）；`CombatHitSink` 已删、`DescriptionDisplayHook` 已 no-op，禁复活；
- `TriggerPulseHub` 是特例：旧 FX / audio / 类型化 VFX 三通道装配缝，脉冲发即完成、可降级；生产装配在 `TriggerPulseOutputController.ConfigureProductionDefaults`（主菜单阶段完成）；局内关停只重置 FX 通道，audio/VFX 保持应用会话（ADR-0036/0040）。
- `BattleBeatHook` 是排期器报点缝：`PresentationCompositionRoot.InstallBattleBeatScheduler` 把 `BattleBeatScheduler` 的方法挂上去，Cards/Flow 剧本经它报 Impact/Settled（ADR-0005/0007）。

### 6. BindableProperty（只读状态订阅）

`GameFlowShellSystem.State`、`PresentationRuntimeSystem.MainlineBusy`、输入门禁三索取声明等；消费者 `Register/RegisterWithInitValue` 订阅，写入仍走 Command。

### 输出侧统一管线（与输入收口对偶，ADR-0005/0007）

一批 Core 结算指令 → `BattleBeatScheduler`（唯一分发出口）→ 按报点交给第一个认领的 `IBattleBeatHandler`：

```
PlayerInfoHudBeatHandler → DamageFloaterBeatHandler → CardFaceStatHandler
→ CardFaceFlipBeatHandler → EffectTriggerPulseBeatHandler → GoldGainBeatHandler
```

顺序即语义（飘字须在卡面前旁路接 Healed）；装饰处理器不占主线 ack。注册点在《01》，Handler 本体在 Flow/（兄弟区块）。

### 装配如何把这一切接起来（一图流）

```
MainScene 加载
└─ PresentationSceneRoot.Awake（-100）
   ├─ PlayerAudioSettings / Language / Music / Vfx System 注册（应用会话，跨主菜单存活）
   ├─ WireHosts：BattleSessionController.BindSceneHosts + 全部 *Hook.RequestWire
   │   └─ 各 Controller 被 Wire：把 Handle* 挂上 Hook、把场景 View Bind 进 System
   ├─ TriggerPulseOutputController.ConfigureProductionDefaults（FX/audio/VFX 三线）
   └─ GameFlowController.Awake 自绑 GameFlowShellSystem（BGM=MainMenu）

开局（BattleSessionController 经 lifecycle 委托）
└─ PresentationSceneRoot.EnsureInstalled
   └─ PresentationCompositionRoot.Install(bindings)
      ├─ 6 条 Present 通道 → BattleSessionSystem
      ├─ 8 路意图剧本工厂 → RoutingIntentScriptFactory
      ├─ InputState / IntentIntake / Shell / AvatarWalk … EnsureRegistered
      ├─ BattleBeatScheduler（6 Handler，固定顺序）→ BattleBeatHook
      └─ PresentationRuntimeSystem.Start → new PresentationDirector

每帧：SceneRoot.Update → Runtime.Tick（Director/Timeline）+ VfxSystem.Tick

回主菜单 / 胜负：SetGameFlowShellStateCommand → TeardownPresentationDirectorRequested
└─ ShutdownRuntime：拆排期器 → ResetFx（只 FX）→ Vfx.ClearSceneInstances（只附着型）
   → ClearPresentChannels → Runtime.Stop
```

## bug 定位速查

| 症状 | 先查 | 篇 |
|------|------|----|
| 点击无反应 / 忙时行为怪 | Console 搜 `[IntentIntake] Reject`；两轴哪轴拒的 | 02 |
| 快速连点触发两次 | 是否有路径绕过 Intake（strict-drop 下不应发生） | 02/06 |
| 表演卡死不推进 | MainlineBusy 粘死？ExternalHold 未释放？`PresentationMainlineHold` 配对 | 02/03 |
| 卡面数值不对 / 不更新 | 排期器 Handler 认领链与顺序；禁旁路直读 Core | 01 + Flow 区块 |
| 没声音 / 声音重复 | AudioCueResult outcome（Unbound/Suppressed/Cooldown/BackendFailure）+ PerfTrace `AudioCue*` | 04 |
| BGM 重叠 / 幽灵 BGM | MusicSystem 审计（unknown sources）+ 切歌代数 | 04 |
| 特效不播 / 播不停 | VfxCueResult outcome + `VfxLifecycle*` PerfTrace + EndReason 分类 | 05 |
| 占格错乱 | 逻辑占格看 Core BoardModel；几何镜像看 GeometrySystem；禁 force sync | 03 |
| 面板打不开 / 按钮点不中 | 失活面板自举（AfterSceneLoad Install）+ PointerHitRegistry 注册 | 08 |
| Win 打包版掉帧 / 卡死 | `-ng-no-rawinput` 排除法；`HangReports` 目录取证 | 02 |
| 流程相位 / BGM 切换错 | `GameFlowShellSystem` 相位映射 + `SetGameFlowShellStateCommand` | 03/06 |

## 关联 ADR 索引（本区块直接落地的）

0001（时间线/Batch-ack）、0003（chainId/choreoSeqId 诊断关联）、0004（两轴门禁）、0005/0007（锚点提交/统一管线）、0006（Win 鼠标兜底）、0016（翻面）、0019（跳格）、0020（场地即交互面/描述退役）、0023（格位认领）、0025/0027（回收/遗物丢弃）、0028（护甲联动 HUD）、0034（补牌锁步）、0035（倒计时投影）、0036（音频）、0039（战败收口）、0040（VFX）、0041（存档）、0042（教学）、0043（平台桥——本区块不含实现，业务门面在 Flow/Platform）。

---

## 文件覆盖清单（178 个，逐一对账）

> 路径相对 `Assets/Scripts/NineGrid.Presentation/`。"篇"为本目录归属文档编号。

### 根目录（2）

| 文件 | 说明 | 篇 |
|------|------|----|
| `PresentationInputGates.cs` | 门禁读口/ExternalHold 写口静态门面 | 02 |
| `PresentationMainlineHold.cs` | 主线租约 TryAcquire/Release 封装 | 02 |

### Setup/（3）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Setup/PresentationSceneRoot.cs` | 场景组合根：宿主引用、Hook 接线、Runtime 生命周期与 Tick | 01 |
| `Setup/PresentationCompositionRoot.cs` | 生产装配：Present 通道×剧本工厂×排期器×System 注册 | 01 |
| `Setup/PresentationSceneBindings.cs` | 场景宿主绑定不可变快照 | 01 |

### Platform/（2）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Platform/WindowsHighPollingMouseMitigation.cs` | Win Player 高回报率鼠标兜底（NOLEGACY+轮询注入） | 02 |
| `Platform/WindowsHangWatchdog.cs` | Win Player 卡死看门狗（心跳+minidump 取证） | 02 |

### Systems/ 输入门禁（5）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Systems/InputOwner.cs` | 所有权轴枚举（四表面，序即优先级） | 02 |
| `Systems/IPresentationInputStateSystem.cs` | 所有权轴只读提供者接口 | 02 |
| `Systems/PresentationInputStateSystem.cs` | 三索取布尔折算 CurrentOwner + busy 投影 + ResetGates | 02 |
| `Systems/IIntentIntake.cs` | 唯一意图收口接口 | 02 |
| `Systems/IntentIntakeSystem.cs` | 两轴裁决+合法性+冲动轻点+拒绝诊断 | 02 |

### Systems/ 核心（16）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Systems/IPresentationRuntimeSystem.cs` | 表现运行时接口（意图+Start/Stop/HardClear） | 03 |
| `Systems/PresentationRuntimeSystem.cs` | Director 持有壳，MainlineBusy 发布源 | 03 |
| `Systems/IBattleSessionSystem.cs` | 局内会话单一所有者接口（40 方法地图） | 03 |
| `Systems/BattleSessionSystem.cs` | 会话转发壳（委托 BattleSessionExecutor）；SyncBoard 已断言禁用 | 03 |
| `Systems/BoardSelectionSystem.cs` | BoardSelect/拖放校验窄门面（接口+实现） | 03 |
| `Systems/ChoicePresentationSystem.cs` | 奖励/残留结算 Present 窄门面（接口+实现） | 03 |
| `Systems/IGroundFieldGeometrySystem.cs` | 场地几何/运动/认领接口 | 03 |
| `Systems/GroundFieldGeometrySystem.cs` | 几何镜像+运动执行实现（OccupancyIndex+MotionExecutor） | 03 |
| `Systems/GroundPresentation.cs` | 导演用场地表现封闭 API（internal） | 03 |
| `Systems/IFieldBattlePresentationSystem.cs` | 交战表现接口 | 03 |
| `Systems/FieldBattlePresentationSystem.cs` | 交战表现实现（委托 Executor） | 03 |
| `Systems/ICardEntityLifecycleSystem.cs` | 卡实体查找+净土域交接接口 | 03 |
| `Systems/CardEntityLifecycleSystem.cs` | 三 Manager 引用+Evict/Admit 实现 | 03 |
| `Systems/IGameFlowShellSystem.cs` | 流程壳只读投影接口 | 03 |
| `Systems/GameFlowShellSystem.cs` | 流程权威：相位/QuickTest/教学/存档对齐/BGM 提交 | 03 |
| `Systems/LanguageSettingsSystem.cs` | 语言偏好唯一读写口（PlayerPrefs + 翻译表重载 + Changed，ADR-0046） | 03 |

### Systems/ 音频（10）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Systems/AudioSystem.cs` | SFX 唯一策略模块（解析/冷却/变体/排期/工作台缝） | 04 |
| `Systems/MusicSystem.cs` | BGM 期望状态机（代数/淡出/审计/试听） | 04 |
| `Systems/MMSoundManagerAudioPlaybackAdapter.cs` | 唯一播放 Adapter（双轨+诊断+淡出协程） | 04 |
| `Systems/PlayerAudioSettingsSystem.cs` | 玩家三路音量偏好（PlayerPrefs+Mixer 应用器） | 04 |
| `Systems/AudioDiagnostics.cs` | SFX 诊断数据契约+诊断 Adapter 接口 | 04 |
| `Systems/AudioDiagnosticsService.cs` | Dev SFX 轨巡检+持续播放异常检测 | 04 |
| `Systems/AudioDiagnosticsTicker.cs` | Dev SFX 巡检驱动（1s DDOL） | 04 |
| `Systems/MusicDiagnostics.cs` | Music 审计契约（快照/审计结果/重叠异常） | 04 |
| `Systems/MusicDiagnosticsTicker.cs` | Dev Music 巡检驱动（只报不停） | 04 |
| `Systems/MMSoundManagerBootstrap.cs` | MMSoundManager 运行时宿主兜底（RuntimeHost） | 04 |

### Systems/ VFX 契约与系统（3）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Systems/VfxSystem.cs` | VFX 运行时（Cue 管线+State 槽+Tick+工作台+默认工厂） | 05 |
| `Systems/VfxPulseContracts.cs` | Pulse 契约（StartRequest/PresentationPlan/StartResult/播放器接口） | 05 |
| `Systems/VfxStateContracts.cs` | State 契约（SlotResult/SlotOwner/退出模式） | 05 |

### Systems/Vfx/（24）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Systems/Vfx/VfxDiagnostics.cs` | 生命周期诊断数据契约（阶段/EndReason/快照） | 05 |
| `Systems/Vfx/VfxDiagnosticsTracker.cs` | 诊断实现（明细环/帧环/聚合/峰值/PerfTrace） | 05 |
| `Systems/Vfx/VfxWorkbenchContracts.cs` | 工作台快照 DTO（#202） | 05 |
| `Systems/Vfx/IVfxMaterialFrameLoader.cs` | 素材帧加载缝（默认委托 Content VfxMaterialFrameSource） | 05 |
| `Systems/Vfx/VfxIndependentSpatialRoot.cs` | 独立型 VFX DDOL 世界锚点根 | 05 |
| `Systems/Vfx/VfxSpriteSheetPlayerFactory.cs` | sprite-sheet 工厂（Pulse+State） | 05 |
| `Systems/Vfx/VfxSpriteSheetPulsePlayer.cs` | 精灵表一次性播放器 | 05 |
| `Systems/Vfx/VfxSpriteSheetStatePlayer.cs` | 精灵表持续播放器（segment 退出段） | 05 |
| `Systems/Vfx/VfxSpriteSheetPlayback.cs` | 帧推进纯数学（fps/loop/offset/双时间基） | 05 |
| `Systems/Vfx/VfxSpriteSheetVisual.cs` | 池化实例视图（ResetVisual） | 05 |
| `Systems/Vfx/VfxSpriteSheetVisualPool.cs` | DDOL 视图池 | 05 |
| `Systems/Vfx/VfxSpriteSheetPoolDiagnostics.cs` | Dev 池事实计数 | 05 |
| `Systems/Vfx/VfxGoldFlightPlayer.cs` | gold-flight 程序化飞币播放器+GoldFlightTiming 纯数学 | 05 |
| `Systems/Vfx/VfxGoldFlightPlayerFactory.cs` | gold-flight 工厂（仅 Pulse） | 05 |
| `Systems/Vfx/VfxParticlePresetLibrary.cs` | 粒子预设真源（21 Pulse+6 Loop，对应 VfxParticlePresetIds） | 05 |
| `Systems/Vfx/VfxParticleEmitterRig.cs` | ParticleSystem 程序化装配+TryResolveMount 空间挂接 | 05 |
| `Systems/Vfx/VfxParticleTextureBank.cs` | 程序化粒子贴图/材质库（五形×双混合） | 05 |
| `Systems/Vfx/VfxParticlePulsePlayer.cs` | particle 一次性播放器 | 05 |
| `Systems/Vfx/VfxParticleStatePlayer.cs` | particle 持续播放器（排空退出） | 05 |
| `Systems/Vfx/VfxParticlePlayerFactory.cs` | particle 工厂（Pulse+State） | 05 |
| `Systems/Vfx/VfxProjectilePresetLibrary.cs` | 弹道预设真源（8 条，轨迹/弹头/拖尾/爆点参数） | 05 |
| `Systems/Vfx/VfxProjectileFlightRig.cs` | 弹道装配（弹头+拖尾+爆点；首达/末达计划） | 05 |
| `Systems/Vfx/VfxProjectilePulsePlayer.cs` | projectile 播放器（源→靶，缺靶演示飞行） | 05 |
| `Systems/Vfx/VfxProjectilePlayerFactory.cs` | projectile 工厂（仅 Pulse） | 05 |

### Commands/（27）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Commands/SubmitExploreIntentCommand.cs` | 探索意图 → Intake | 06 |
| `Commands/SubmitAttackIntentCommand.cs` | 攻击意图 → Intake | 06 |
| `Commands/SubmitRevealFaceIntentCommand.cs` | 主动翻开意图 → Intake | 06 |
| `Commands/SubmitBoardWalkIntentCommand.cs` | 跳格意图 → Intake | 06 |
| `Commands/SubmitUseItemIntentCommand.cs` | 用牌意图 → Intake（BoardSelect 分流） | 06 |
| `Commands/SubmitRecycleItemIntentCommand.cs` | 回收意图 → Intake | 06 |
| `Commands/SubmitSelectRoomCommand.cs` | 选房 → Core SelectRoom | 06 |
| `Commands/SubmitEnterRoomCommand.cs` | 进房 → Core EnterRoom | 06 |
| `Commands/SubmitSelectRewardCommand.cs` | 选奖励 → Core SelectReward | 06 |
| `Commands/SubmitSkipHelpChoiceCommand.cs` | 跳过/出店 → Core SkipHelpChoice | 06 |
| `Commands/SubmitRefreshShopCommand.cs` | 刷新货架 → Core RefreshShop | 06 |
| `Commands/SubmitDiscardRelicCommand.cs` | 丢弃遗物 → Core DiscardRelic + HUD 同步 + UpdateGold 冲刷 | 06 |
| `Commands/ApplyPickupItemCommand.cs` | 拾取 Apply + EventLog 投影摘要 | 06 |
| `Commands/BeginGameFlowRunCommand.cs` | 开局 → Shell.BeginRun | 06 |
| `Commands/ReturnToMainMenuCommand.cs` | 回主菜单 → Shell.ReturnToMainMenu | 06 |
| `Commands/SignalGameFlowCommand.cs` | SettlementReady/BattleEnded 信号投递 | 06 |
| `Commands/SetGameFlowShellStateCommand.cs` | 相位写入+BGM 提交+Teardown 触发 | 06 |
| `Commands/PresentationInputGateCommands.cs` | 门禁写入四条（Opening/Overlay/BoardSelect/Reset） | 06 |
| `Commands/DirectorExternalHoldCommands.cs` | 主线租约三条（Begin/End/ForceEnd） | 06 |
| `Commands/GroundFieldOccupancyCommands.cs` | 几何占格四条（Place/Vacate/Relocate/Clear） | 06 |
| `Commands/HandoffCardBetweenZonesCommand.cs` | 净土域 Hand/Deck/Battle 交接 | 06 |
| `Commands/EnqueueShuffleIntoDeckFromEventLogCommand.cs` | 洗回牌库 EventLog 扫描入 sink | 06 |
| `Commands/RequestDamageNumberCommand.cs` | 飘字请求 → 事件 | 06 |
| `Commands/SyncRelicHudCommand.cs` | 遗物栏同步请求 → 事件 | 06 |
| `Commands/RequestDescriptionShowCommand.cs` | （退役残留）描述展示 → 事件 | 06 |
| `Commands/RequestDescriptionShowTextCommand.cs` | （退役残留）原始文案展示 → 事件 | 06 |
| `Commands/RequestDescriptionClearCommand.cs` | （退役残留）描述清除 → 事件 | 06 |

### Queries/（10）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Queries/GroundFieldQueries.cs` | 几何快照 + uid→slot 两条 | 06 |
| `Queries/TryGetCardAtSlotQuery.cs` | slot→ManagedCard | 06 |
| `Queries/TryGetManagedCardQuery.cs` | uid→ManagedCard（经 Lifecycle） | 06 |
| `Queries/IsFieldBusyQuery.cs` | 场地自身忙碌 | 06 |
| `Queries/IsCardInDrawPileQuery.cs` | uid 是否在抽牌堆 | 06 |
| `Queries/IsCardInItemSlotsQuery.cs` | uid 是否在道具卡格 | 06 |
| `Queries/HasShuffleExistingSinceQuery.cs` | EventLog 起点后有无 ExistingCard 洗回 | 06 |
| `Queries/EstimateWillKillQuery.cs` | 击杀预估（Lethal Profile 选择用） | 06 |
| `Queries/MonsterStrikesFirstQuery.cs` | 先手还击裁决（委托 Core Phase） | 06 |
| `Queries/ResolvePlayerAttackTargetQuery.cs` | 攻击实际目标解析（嘲讽重定向） | 06 |

### Controllers/（21）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Controllers/PresentationController.cs` | Controller 基类（OnBind/OnUnbind+UnregisterList） | 07 |
| `Controllers/AttackInputController.cs` | 怪物格点击（背面卡分流翻开） | 07 |
| `Controllers/ExploreInputController.cs` | 空槽探索点击 | 07 |
| `Controllers/BoardWalkInputController.cs` | 非战斗跳格点击 | 07 |
| `Controllers/PickupInputController.cs` | 拾取三段式（Allow→Hold→Apply） | 07 |
| `Controllers/UseItemInputController.cs` | 用牌提交 | 07 |
| `Controllers/RecycleItemInputController.cs` | 回收提交 | 07 |
| `Controllers/RoomChoiceInputController.cs` | 选房/进房（ChoiceOverlay 门禁） | 07 |
| `Controllers/RewardChoiceInputController.cs` | 奖励/出店/刷新（owner 跟随 CurrentOwner） | 07 |
| `Controllers/GroundFieldGeometryController.cs` | Field View → Geometry System 登记 | 07 |
| `Controllers/FieldBattlePresentationController.cs` | Battle View → Battle System 登记 | 07 |
| `Controllers/CardEntityLifecycleController.cs` | 三 Manager → Lifecycle System 登记 | 07 |
| `Controllers/BattleSessionPresentationController.cs` | Session View → Session System 登记 | 07 |
| `Controllers/GameFlowShellController.cs` | Shell Bind + 会话事件→Signal 转发 | 07 |
| `Controllers/DamageNumberOutputController.cs` | 飘字 Hook→Command→Event 桥 | 07 |
| `Controllers/TriggerPulseOutputController.cs` | TriggerPulseHub 三线装配与生命周期 | 07 |
| `Controllers/DiagnosticOutputController.cs` | 诊断 Recorder 接线生命周期 | 07 |
| `Controllers/RelicHudController.cs` | 遗物栏七缝（同步/丢弃/拖拽/详述/倒计时） | 07 |
| `Controllers/DescriptionOutputController.cs` | （退役空壳）动态描述 Hook 置空 | 07 |
| `Controllers/ZoneOwnershipQueryController.cs` | Zone 归属 Hook→Query 只读桥 | 07 |
| `Controllers/AvatarBoardFacingController.cs` | Avatar 朝向（指针相对卡面 X 镜像） | 07 |

### Ui/（8）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Ui/PlayerAudioSettingsPanel.cs` | 局内功能菜单总接线（音量/流程按钮/存档模块挂载） | 08 |
| `Ui/RunSaveLoadPanel.cs` | 存档/读档模块（双模式+条目克隆） | 08 |
| `Ui/CharacterSelectPanel.cs` | 人物选择面板 | 08 |
| `Ui/RunSummaryPanel.cs` | 局终结算面板（只读快照+阻塞确认） | 08 |
| `Ui/WorldUiHitButton.cs` | 世界空间按钮通用件 | 08 |
| `Ui/ResourcesSpriteLoop.cs` | Resources 序列帧循环装饰 | 08 |
| `Ui/UiAudioFeedback.cs` | uGUI Selectable 统一声音出口 | 08 |
| `Ui/SceneTextLocalizer.cs` | MainScene 静态 TMP 标签本地化（显式引用 + ui 键，ADR-0046） | 08 |

### Cheat/（6）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Cheat/CheatToolHotkeyHost.cs` | F12 热键常驻宿主+无敌模式 Tick | 08 |
| `Cheat/CheatToolPanelController.cs` | 作弊面板主控（一级八按钮+加卡/加遗物/log 二级） | 08 |
| `Cheat/CheatToolPanelButton.cs` | 一级按钮命中代理 | 08 |
| `Cheat/CheatToolGodMode.cs` | 无敌模式（回血/金币/加攻定时） | 08 |
| `Cheat/CheatToolCardSearchIndex.cs` | 加卡搜索索引（纯逻辑） | 08 |
| `Cheat/CheatToolRelicSearchIndex.cs` | 加遗物搜索索引（纯逻辑） | 08 |

### Editor/（14）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Editor/CardFacePreviewHost.cs` | 卡面预览宿主（IMGUI+离屏 PNG） | 09 |
| `Editor/CardFacePreviewBuilder.cs` | 终态预览构建（底盘+L4+ApplyPresentation） | 09 |
| `Editor/CardFacePreviewRequest.cs` | 预览假投影输入 DTO | 09 |
| `Editor/VisualEffectPreviewHost.cs` | 特效库预览（参照卡+精灵表循环） | 09 |
| `Editor/CardDOTweenSequenceEffectSOEditor.cs` | 特效 SO Inspector（烘焙按钮） | 09 |
| `Editor/CardDOTweenSequenceBakeUtility.cs` | DOTweenAnimation→CardTweenClip 反射烘焙 | 09 |
| `Editor/BattleTracePlayModeExporter.cs` | 退 Play 自动导出四轨日志 | 09 |
| `Editor/DiagTraceEditorWindow.cs` | 诊断日志控制台窗口 | 09 |
| `Editor/ProfilerCaptureAnalyzer.cs` | Profiler 捕获尖峰分析（一次性工具） | 09 |
| `Editor/DevPlayerBuild.cs` | Development Win64 打包队列 | 09 |
| `Editor/ReleasePlayerBuild.cs` | Release Win64 打包队列（#142） | 09 |
| `Editor/FeelTransitionSceneInstall.cs` | Feel 过场 Canvas 场景装配 | 09 |
| `Editor/SmileySansSdfCharsetBaker.cs` | SDF 字体原地补字 | 09 |
| `Editor/UiStrokeThickenBatch.cs` | UI 描边加粗批处理 | 09 |

### Tests/（32 = 根 7 + Tests/Flow 25）

| 文件 | 说明 | 篇 |
|------|------|----|
| `Tests/AvatarVitalityInvariantTests.cs` | ADR-0039 Avatar 判死谓词回归（6 用例） | 10 |
| `Tests/CardFaceReconciliationRegressionTests.cs` | ADR-0045 卡面投影对账缝回归（重放==oracle） | 10 |
| `Tests/DeferredBoardMotionRegressionTests.cs` | ADR-0044 结算窗口位移挂起回归（先打再转） | 10 |
| `Tests/HolyDuelMarkRegressionTests.cs` | 神圣决斗标记先罚后转回归 | 10 |
| `Tests/NodeEndTransientResetTests.cs` | 清关即清临时修正与当前甲回落回归 | 10 |
| `Tests/RelicCompositeArmorNodeStartTests.cs` | StartNode 遗物效果自愈重挂回归（复合盔甲） | 10 |
| `Tests/TrapArmorTotemBorrowedArmorTests.cs` | 护甲图腾借甲光环回归（邻接+1/离邻回收） | 10 |
| `Tests/Flow/AudioSystemBehaviorTests.cs` | AudioSystem 冷却/变体/排期/工作台热调音行为 | 10 |
| `Tests/Flow/MusicDiagnosticsBehaviorTests.cs` | MusicSystem 切歌代数/审计/试听恢复行为 | 10 |
| `Tests/Flow/PlayerAudioSettingsBehaviorTests.cs` | 三总线音量/静音/落库重置行为 | 10 |
| `Tests/Flow/AudioDiagnosticsBehaviorTests.cs` | SFX 诊断异常检测与 PerfTrace 载荷 | 10 |
| `Tests/Flow/AudioStructureGuardTests.cs` | 音频结构护栏（源码扫描，禁绕过深模块） | 10 |
| `Tests/Flow/AudioDeliveryHygieneTests.cs` | 正式 audio_bindings.json 与 cue 声明卫生门禁 | 10 |
| `Tests/Flow/AudioAiInitialBinderTests.cs` | AI 初始绑定保人工确认 + 卫生校验器分类 | 10 |
| `Tests/Flow/SkillEffectTrapRelicAudioTests.cs` | 技能/机关/遗物声音选择器优先级与延迟必播 | 10 |
| `Tests/Flow/CardLifecycleAndCombatAudioTests.cs` | 卡牌生命周期/战斗声音顺序（出伤分账无重复） | 10 |
| `Tests/Flow/FlowRoomEconomyAudioTests.cs` | 房间/经济/过场声音 seam 契约 | 10 |
| `Tests/Flow/AudioBindingEditorSessionTests.cs` | 工作台编辑会话（保存/回撤/夹紧/键冲突） | 10 |
| `Tests/Flow/VfxSystemBehaviorTests.cs` | VfxSystem Cue 六类结局/空间所有权/热调音 | 10 |
| `Tests/Flow/VfxPersistentStateBehaviorTests.cs` | 持续状态槽期望态收敛与退出模式 | 10 |
| `Tests/Flow/VfxDiagnosticsBehaviorTests.cs` | VFX 生命周期诊断/Issue 分类/峰值下钻 | 10 |
| `Tests/Flow/VfxSpriteSheetPlayerContractTests.cs` | sprite-sheet 播放器帧序/循环/池复位契约 | 10 |
| `Tests/Flow/VfxGoldFlightPlayerContractTests.cs` | gold-flight 时间窗计划/宿主死亡/图标复位契约 | 10 |
| `Tests/Flow/VfxParticlePlayerContractTests.cs` | particle 播放器预设表一致性与生命周期契约 | 10 |
| `Tests/Flow/VfxProjectilePlayerContractTests.cs` | projectile 播放器预设表一致性与命中计划契约 | 10 |
| `Tests/Flow/VfxStructureGuardTests.cs` | VFX 结构护栏（源码扫描，禁绕过类型化入口） | 10 |
| `Tests/Flow/VfxDeliveryHygieneTests.cs` | 正式 vfx_bindings.json 与声明/播放器/素材卫生 | 10 |
| `Tests/Flow/VfxBindingCatalogTests.cs` | VFX 绑定解析优先级/BindingKey/编辑会话 | 10 |
| `Tests/Flow/VfxCrossSystemContractTests.cs` | Hub 三通道装配 + 金币迁移收口跨系统契约 | 10 |
| `Tests/Flow/GoldGainPresentationMigrationTests.cs` | 金币 Binder 稳定 Cue 迁移与降级行为 | 10 |
| `Tests/Flow/GoldHudNumberWindowTests.cs` | HUD 金币数字时间窗采样 | 10 |
| `Tests/Flow/CardDeckEntryDurationTests.cs` | 发牌入场时长估算公式与入场声音 seam | 10 |

**合计：2 + 3 + 2 + 5 + 16 + 10 + 3 + 24 + 27 + 10 + 21 + 8 + 6 + 14 + 32 = 183 ✅**
