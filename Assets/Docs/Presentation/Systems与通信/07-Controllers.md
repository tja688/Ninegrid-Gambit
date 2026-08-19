# Controllers（21 个）

> 覆盖范围：`Controllers/` 全部 21 个文件。
> Controller 是 **静态 Hook（Cards/Flow 侧接线缝）↔ QF Command/System** 之间的薄桥：把场景宿主登记进 System、把 Hook 委托指向自己的 Handle 方法。**Controller 不持业务状态。**

## 统一模式

- 基类 `PresentationController`：`Awake→OnBind`、`OnDestroy→UnRegisterAll+OnUnbind`；`GetArchitecture()` 返回 `NineGridArchitecture.Interface`。
- 接线自举：`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 把 `XxxHook.WireController = Wire` 注册为装配回调；生产装配由 `PresentationSceneRoot.WireHosts` 触发 `RequestWire`，Hook 内部再调 `WireController`。Wire 时找现有实例（GetComponent → FindObjectOfType），没有则 new GameObject 挂一个。
- 卸载纪律：Clear 时**先比对委托是否还是自己**再置 null，防止误清后来者。

## 关键类型表与职责

### 输入类（Hook → Intake/Command）

| Controller | 文件 | Hook | 一句话职责 |
|-----------|------|------|-----------|
| `AttackInputController` | `AttackInputController.cs` | `AttackInputHook.TrySubmitAttack` | 怪物格点击 → 先查目标是否背面卡（是则 `SubmitRevealFaceIntentCommand` 主动翻开，ADR-0016），否则 `SubmitAttackIntentCommand` |
| `ExploreInputController` | `ExploreInputController.cs` | `ExploreInputHook.TrySubmitExplore` | 空槽点击 → `SubmitExploreIntentCommand` |
| `BoardWalkInputController` | `BoardWalkInputController.cs` | `BoardWalkInputHook.TrySubmitBoardWalk` + `IsEnabled`（读 `IAvatarWalkSystem.IsEnabled`） | 非战斗跳格 → `SubmitBoardWalkIntentCommand`（ADR-0019） |
| `PickupInputController` | `PickupInputController.cs` | `PickupInputHook.TryApplyPickup` | 拾取（#234）：Intake 两轴裁决 → idle 交导演锁步剧本（返回 `RoutedToDirector` 摘要）；busy strict-drop；不再持锁直写 Core |
| `UseItemInputController` | `UseItemInputController.cs` | `UseItemInputHook.TrySubmitUseItem` | 手牌拖放/多选提交 → `SubmitUseItemIntentCommand` |
| `RecycleItemInputController` | `RecycleItemInputController.cs` | `RecycleItemInputHook.TrySubmitRecycleItem` | 拖入回收区 → `SubmitRecycleItemIntentCommand` |
| `RoomChoiceInputController` | `RoomChoiceInputController.cs` | `RoomChoiceCoreHook.SelectRoom/EnterRoom` | 选房/进房：`TryIntakeModal`（固定 targetSurface=ChoiceOverlay）→ `SubmitSelectRoomCommand` / `SubmitEnterRoomCommand` |
| `RewardChoiceInputController` | `RewardChoiceInputController.cs` | `RewardChoiceCoreHook.SelectReward/SkipHelpChoice/RefreshShop` | 奖励/出店/刷新：targetSurface **跟随 CurrentOwner**（ChoiceOverlayActive ? ChoiceOverlay : ProtectedField——房内场地板是受保护场地，硬编码 ChoiceOverlay 会 ownerMismatch）→ 对应 Submit 命令 |

### 视图登记类（场景宿主 → QF System）

| Controller | 文件 | 一句话职责 |
|-----------|------|-----------|
| `GroundFieldGeometryController` | `GroundFieldGeometryController.cs` | `GroundFieldGeometryHook.Wire` → `IGroundFieldGeometrySystem.Bind(field)`；并设 `Hook.ResolveField` |
| `FieldBattlePresentationController` | `FieldBattlePresentationController.cs` | `FieldBattlePresentationHook.Wire` → `IFieldBattlePresentationSystem.Bind(battle)`；并设 `Hook.ResolveBattle` |
| `CardEntityLifecycleController` | `CardEntityLifecycleController.cs` | `CardEntityLifecycleHook.Wire` → `ICardEntityLifecycleSystem.Bind(cards, hand, deck)`；并设 Hook 的 Resolve 三缝 + `TryGet` + `NotifyCardReleased` |
| `BattleSessionPresentationController` | `BattleSessionPresentationController.cs` | `WireSession(session)`（静态直调，无 Hook）→ `IBattleSessionSystem.Bind` |
| `GameFlowShellController` | `GameFlowShellController.cs` | `GameFlowShellHook.WireController`：Bind 场景 `GameFlowController` 到 Shell System（若未绑）；订阅 `BattleSessionSettlementReadyEvent`/`BattleSessionEndedEvent` → `SignalGameFlowCommand`；`GameFlowShellHook.SetState` 镜像路径已停用（写相位直接走 Command） |

### 输出/装配类

| Controller | 文件 | 一句话职责 |
|-----------|------|-----------|
| `DamageNumberOutputController` | `DamageNumberOutputController.cs` | `DamageNumberHook.Spawn` → `RequestDamageNumberCommand` → `DamageNumberRequested` 事件（FX 只订阅事件）；`EnsureInstalled` 供 SceneRoot |
| `TriggerPulseOutputController` | `TriggerPulseOutputController.cs` | 持 `TriggerPulseHub` 生命周期：`ConfigureProductionDefaults` 三线装配（FX=`CardEffectTriggerPulseSink`、audio=`DebouncingTriggerPulseSink(AudioTriggerPulseSink, 0.05s)`、VFX=`VfxTriggerPulseSink`）；`ResetHubFx` 只重置旧 FX 通道（ADR-0036/0040 应用会话语义） |
| `DiagnosticOutputController` | `DiagnosticOutputController.cs` | 持 Recorder 接线生命周期：生产 Attach 由 InBattle 经 `DiagnosticOutputHook` 显式触发（FieldTrace/PerfTrace/RegistryTrace 三组 Sink Handler）；`AttachRecordersForTests` 注入可观测假 handler；Detach 全清 |
| `RelicHudController` | `RelicHudController.cs` | `RelicHudHook` 七缝全挂：SyncFromCore（同步遗物栏 + **顺带刷 PlayerInfoHud**——遗物改有效护甲，ADR-0028）/ Clear / TryDiscardRelic（Intake→`SubmitDiscardRelicCommand`，拒绝 Reason 走简要解释 Notice）/ TryBeginDragRelic / TryInspectRelic（`PointerHitRegistry` 反查 `ContentIconSlotHitProxy` 最高 sort 的 relic → 右键详述）/ Commit·ClearCountdownRemaining（遗物图标"还差几次"TMP，ADR-0035）；另消费 `RelicHudSyncRequestedEvent` |
| `DescriptionOutputController` | `DescriptionOutputController.cs` | **已退役空壳**：把 `DescriptionDisplayHook` 四缝全部置 null；`EnsureInstalled` 返回 null。动态 HUD 描述管道已砍（ADR-0020 / #141），禁复活 |
| `ZoneOwnershipQueryController` | `ZoneOwnershipQueryController.cs` | `CardZoneOwnershipHook.IsCoreItemSlots/IsCoreDrawPile` → 两条 Zone Query（Cards 生产路径只读桥）；OnUnbind 刻意不清 Hook（防测试夹具误伤，下一局 EnsureWired 重置） |
| `AvatarBoardFacingController` | `AvatarBoardFacingController.cs` | 每帧：解析 Avatar（优先 Core `BoardModel.AvatarSlot` 占格 → Geometry 查卡；回退 AvatarUid 反查）→ 指针世界 X 相对卡面图标 X → `AvatarBoardFacingState.SetFacing` → `CardSpriteAnimPlayer.SetMirrorX`（带 uid 缓存） |
| `PresentationController`（基类） | `PresentationController.cs` | 见"统一模式" |

## 核心流程与数据流

典型输入路径（code-map「Controllers」节的展开）：

```
场景 Host / HitProxy → 静态 Hook（Cards/Flow 侧定义）
→ Controller.Handle*（本篇）
→ SendCommand（《06》A/B/C 组）
→ IntentIntake（《02》两轴）→ Director / Core Command
```

输出路径：

```
排期器 Handler / Cards Hook（如 DamageNumberHook.Spawn）
→ Controller → Command → struct Event → 表现消费者（DamageNumberManagerSingleton 等）
```

## 对外通信面

- **上游**：静态 Hook 矩阵（`AttackInputHook` 等约 20 个，定义在 Cards/Flow 目录，见兄弟文档；本篇 Controller 是 Hook 的**唯一生产实现方**）。
- **下游**：《06》的 Command / Query；`CardInspectOverlayPresenter`、`BoardBriefTipPresenter`、`PlayerInfoHudPresenter`（Flow 侧）。

## 关联 ADR

ADR-0004（门禁纪律：输入类 Controller 全部过 Intake）、ADR-0016（背面卡点击分流主动翻开）、ADR-0019/0020/0025/0027/0028/0035（各交互语义）。

## 不变量与坑

- **Hook 是装配缝不是业务 Sink**（code-map 明令）：Controller 在 Hook 上只挂"提交/查询/同步"委托，禁止经 Hook 跨层读写规则状态；新交互优先 Command/Query/Event/System。
- `RewardChoiceInputController` 的 owner 跟随 `CurrentOwner` 是**修过的坑**（店内 ownerMismatch），`RoomChoiceInputController` 则固定 ChoiceOverlay（选房确实在覆层下）——两者不一致是刻意的。
- `AttackInputController.WireToBattle` 等 Wire 函数里仍有 `FindObjectOfType` 兜底——这是 Hook 装配路径的自愈逻辑，不违反 #141（#141 禁的是 `BattleSessionController` 依赖解析用 Find）。
- `DescriptionOutputController` 空壳保留是为了 `SubsystemRegistration` 时清 Hook；删除文件会让旧引用悬挂。
- `RelicHudController.ResolveRelicManager` 有 `FindFirstObjectByType` 回退（`BindRelicManager` 未被调用时）；生产期望 SceneRoot 路径先行。
- 输入类 Controller 的 Handle 方法是 **EditMode 直驱共用入口**（注释明示），测试可绕 Hook 直接调。
