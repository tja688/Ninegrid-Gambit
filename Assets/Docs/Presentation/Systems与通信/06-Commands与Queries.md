# Commands 与 Queries 全清单

> 覆盖范围：`Commands/` 27 个文件（30 个 Command 类）+ `Queries/` 10 个文件（11 个 Query 类）。
> QF 读写约定：**写 → `SendCommand`，读 → `SendQuery` / `GetSystem<T>()` 只读 API**（`docs/code-map/presentation.md`「读写约定」）。

## Commands（按职责分组）

### A. 意图提交类（→ IntentIntake 唯一收口，ADR-0004）

这组 Command 是"输入 → 意图"的标准包装：构造 `InputIntent`，`intake.Submit(intent, targetSurface)`，Reject 时发对应 `*RejectedEvent`。

| Command | 文件 | 参数 | 发起方 | 处理链 |
|---------|------|------|--------|--------|
| `SubmitExploreIntentCommand` | `SubmitExploreIntentCommand.cs` | groundSlot | `ExploreInputController` | Intake(ProtectedField) → Runtime.TrySubmitIntent → Explore 剧本；拒发 `ExploreIntentRejectedEvent` |
| `SubmitAttackIntentCommand` | `SubmitAttackIntentCommand.cs` | groundSlot | `AttackInputController` | 同上（Attack）；拒发 `AttackIntentRejectedEvent` |
| `SubmitRevealFaceIntentCommand` | `SubmitRevealFaceIntentCommand.cs` | groundSlot | `AttackInputController`（点到背面卡时分流） | 同上（RevealFace，主动翻开） |
| `SubmitBoardWalkIntentCommand` | `SubmitBoardWalkIntentCommand.cs` | groundSlot | `BoardWalkInputController` | 同上（BoardWalk，非战斗跳格，ADR-0019）；接受/拒绝均打 Debug 日志 |
| `SubmitUseItemIntentCommand` | `SubmitUseItemIntentCommand.cs` | itemUid, selectedCardUids[], selectedOption | `UseItemInputController` | targetSurface 按 BoardSelect 激活且带已选卡切 `BoardSelect`，否则 ProtectedField；`RouteToBoardSelect` 返回 false（转选卡模式）；拒发 `UseItemIntentRejectedEvent` |
| `SubmitRecycleItemIntentCommand` | `SubmitRecycleItemIntentCommand.cs` | itemUid | `RecycleItemInputController` | 同上（RecycleItem，#110/ADR-0025） |

### B. 模态/房间/商店类（IntentIntake 门禁在 Controller 层，Command 直转 Core）

| Command | 文件 | 参数 | 发起方 | 处理链 |
|---------|------|------|--------|--------|
| `SubmitSelectRoomCommand` | `SubmitSelectRoomCommand.cs` | optionIndex | `RoomChoiceInputController` | → Core `SelectRoomCommand` |
| `SubmitEnterRoomCommand` | `SubmitEnterRoomCommand.cs` | — | `RoomChoiceInputController` | → Core `EnterRoomCommand` |
| `SubmitSelectRewardCommand` | `SubmitSelectRewardCommand.cs` | optionIndex | `RewardChoiceInputController` | → Core `SelectRewardCommand` |
| `SubmitSkipHelpChoiceCommand` | `SubmitSkipHelpChoiceCommand.cs` | — | `RewardChoiceInputController` | → Core `SkipHelpChoiceCommand`（出店/放弃） |
| `SubmitRefreshShopCommand` | `SubmitRefreshShopCommand.cs` | — | `RewardChoiceInputController` | → Core `RefreshShopCommand` |
| `SubmitDiscardRelicCommand` | `SubmitDiscardRelicCommand.cs` | relicDefId | `RelicHudController`（Intake Allow 后） | → Core `DiscardRelicCommand`；成功后 `RelicHudHook.RequestSync` + `BattleBeatFlush.PresentEventLogSliceOnly(UpdateGold)`（ADR-0027） |

### C. 拾取（特殊三段式：Allow → ExternalHold → Apply）

| Command | 文件 | 参数 | 发起方 | 处理链 |
|---------|------|------|--------|--------|
| `ApplyPickupItemCommand` | `ApplyPickupItemCommand.cs` | groundSlot | `PickupInputController`（Intake Allow + ExternalHold 之后） | `IPhaseSystem.ApplyPickupItem` → `BoardPresentationStepProjector.Project`（EventLog 切片投影 Steps/Moves/Deals/Removed）→ 回填 `PickupItemPresentationResult`（AcquiredToHand / RemovedWithoutHand / NodeClearedOrRewardPhase）→ `BattleSessionController.PresentPickupPostApplyEffects`；拒发 `PickupItemRejectedEvent` |

### D. 流程壳类

| Command | 文件 | 参数 | 发起方 | 处理链 |
|---------|------|------|--------|--------|
| `BeginGameFlowRunCommand` | `BeginGameFlowRunCommand.cs` | GameFlowRunOptions | `GameFlowShellController` / `GameFlowController`（主菜单开局） | `GameFlowShellSystem.BeginRun` → Orchestrator.Start |
| `ReturnToMainMenuCommand` | `ReturnToMainMenuCommand.cs` | — | 局内功能菜单 / `GameFlowShellController` | `GameFlowShellSystem.ReturnToMainMenu`（停循环回主菜单） |
| `SignalGameFlowCommand` | `SignalGameFlowCommand.cs` | GameFlowSignal（静态工厂 `SettlementReady()` / `BattleEnded(victory)`） | `GameFlowShellController`（订阅会话事件转发） | `GameFlowShellSystem.Signal` |
| `SetGameFlowShellStateCommand` | `SetGameFlowShellStateCommand.cs` | GameFlowShellState | Orchestrator（经 Controller.HandleSetState） | 同值 no-op；否则 `ApplyState` + `SubmitMusicForShellState`（BGM 唯一提交点之一）+ 发 `GameFlowShellStateChangedEvent`；MainMenu/胜负 Notice 另发 `TeardownPresentationDirectorRequested`（Defeat→`IntentClearReason.Defeat`，Victory→PhaseChange，其余→LayerChange） |

### E. 输入门禁写入类（轴二写入唯一通路）

四条均定义于 `PresentationInputGateCommands.cs`，发起方为 `PresentationInputGates` 门面（更上游是 Opening 表演、ChoiceOverlay Presenter、BoardSelect 控制器）：

| Command | 参数 | 效果 |
|---------|------|------|
| `SetOpeningPresentationGateCommand` | active | `IPresentationInputStateSystem.SetOpeningPresentationActive` |
| `SetChoiceOverlayGateCommand` | active | `SetChoiceOverlayActive` |
| `SetBoardSelectModeGateCommand` | active | `SetBoardSelectModeActive` |
| `ResetPresentationInputGatesCommand` | reason | `ResetGates`（三布尔清零 + BoardSelect 静态会话 End + ForceEndExternalHold） |

### F. 导演主线租约类（`DirectorExternalHoldCommands.cs`，三条）

| Command | 返回 | 效果 |
|---------|------|------|
| `BeginDirectorExternalHoldCommand` | bool | Runtime 已启动时 `TryBeginExternalHold(reason)`（Pickup/Drain/RewardDrain 等阻塞输入表演取租约） |
| `EndDirectorExternalHoldCommand` | — | `EndExternalHold` |
| `ForceEndDirectorExternalHoldCommand` | — | `ForceEndExternalHold`（仅 Reset/清场路径） |

### G. 场地几何写入类（`GroundFieldOccupancyCommands.cs`，四条）

发起方：Cards 侧运动/收敛代码与导演剧本；全部要求 Geometry System 已 Bind。

| Command | 参数 | 效果 |
|---------|------|------|
| `PlaceGroundOccupancyCommand` | slot, ManagedCard, skipBusyGuard | `RequestPlaceCard`（几何登记 Place） |
| `VacateGroundOccupancyCommand` | slot, skipBusyGuard | `ClearSlotOccupancy`（仅清占格不销毁视图） |
| `RelocateGroundOccupancyCommand` | uid, toSlot, snapToAnchor, skipBusyGuard | `RequestRelocateOccupancy` |
| `ClearGroundOccupancyCommand` | force | `ClearField`（清场） |

### H. 卡牌交接 / 表现输出类

| Command | 文件 | 参数 | 发起方 | 处理链 |
|---------|------|------|--------|--------|
| `HandoffCardBetweenZonesCommand` | `HandoffCardBetweenZonesCommand.cs` | card, from, to（`SanctuaryEndpoint`：Hand/Deck/Battle） | Cards 净土域 C 阶段交接调用点 | Lifecycle System Evict→Admit；Hand/Deck 端要求 IsBound，Battle 端只动 Transform；Lifecycle 缺失时就地注册 |
| `EnqueueShuffleIntoDeckFromEventLogCommand` | `EnqueueShuffleIntoDeckFromEventLogCommand.cs` | sink, startIndex, isAlreadyInDeck?, scheduler? | 洗回牌库表现路径（BattleSessionExecutor） | `ShuffleIntoDeckScheduler.EnqueueFromEventLog` 扫 EventLog 切片入导演 sink（不立刻 Present），返回入队数 |
| `RequestDamageNumberCommand` | `RequestDamageNumberCommand.cs` | worldPosition, amount, DamageNumberKind | `DamageNumberOutputController.HandleSpawn`（Cards Hook 转发） | 发 `DamageNumberRequested` 事件（FX 只订阅事件，不改规则真相） |
| `SyncRelicHudCommand` | `SyncRelicHudCommand.cs` | clear | 遗物授予/丢弃收口各处 | 发 `RelicHudSyncRequestedEvent`，`RelicHudController` 消费执行表现侧同步 |
| `RequestDescriptionShowCommand` | `RequestDescriptionShowCommand.cs` | defId, route | （历史动态描述管道，现无生产消费者） | 发 `DescriptionShowRequested` 事件 |
| `RequestDescriptionShowTextCommand` | `RequestDescriptionShowTextCommand.cs` | text, route | 同上 | 发 `DescriptionShowTextRequested` |
| `RequestDescriptionClearCommand` | `RequestDescriptionClearCommand.cs` | route | 同上 | 发 `DescriptionClearRequested` |

> 描述三条 Command 属**退役通路残留**：`DescriptionOutputController` 已把 `DescriptionDisplayHook` 全部置 null（见《07》），事件无人生产性消费。保留是为兼容测试/历史调用点；新交互一律走简要解释文字框（ADR-0020）或卡面 `Basic_Description` Commit，**勿**复活此管道。

## Queries（全部只读，不改 Core 状态）

| Query | 文件 | 输入 → 输出 | 委托目标 | 典型消费方 |
|-------|------|------------|----------|-----------|
| `GroundFieldSnapshotQuery` | `GroundFieldQueries.cs` | — → `GroundFieldSnapshot` | Geometry System | 编排/诊断读几何快照 |
| `TryGetSlotOfUidQuery` | `GroundFieldQueries.cs` | uid → (ok, slot) | Geometry System | 反查卡所在格 |
| `TryGetCardAtSlotQuery` | `TryGetCardAtSlotQuery.cs` | slot → ManagedCard | Geometry System | 命中/悬停解析 |
| `TryGetManagedCardQuery` | `TryGetManagedCardQuery.cs` | uid → ManagedCard | Lifecycle System | 跨域找卡（替代静态 Instance） |
| `IsFieldBusyQuery` | `IsFieldBusyQuery.cs` | — → bool | Geometry.IsFieldBusy | 场地自身忙碌探查（非门禁） |
| `IsCardInDrawPileQuery` | `IsCardInDrawPileQuery.cs` | uid → bool | CardRegistry.Zone | 卡组吸纳门禁（经 `CardZoneOwnershipHook`） |
| `IsCardInItemSlotsQuery` | `IsCardInItemSlotsQuery.cs` | uid → bool | CardRegistry.Zone | 同上（道具卡格归属） |
| `HasShuffleExistingSinceQuery` | `HasShuffleExistingSinceQuery.cs` | startIndex → bool | `ShuffleIntoDeckScheduler.HasShuffleExistingSince` | 洗回表现判定 |
| `EstimateWillKillQuery` | `EstimateWillKillQuery.cs` | attackerUid, targetUid → bool | StatSystem 有效攻/甲/血估算（怪物含 `EnemyAttackDelta`） | 选 Lethal Profile（交战表演分支） |
| `MonsterStrikesFirstQuery` | `MonsterStrikesFirstQuery.cs` | avatarUid, monsterUid → bool | `IPhaseSystem.MonsterStrikesFirst` | 先手还击裁决（AttackIntentScriptFactory 入队顺序） |
| `ResolvePlayerAttackTargetQuery` | `ResolvePlayerAttackTargetQuery.cs` | intendedTargetUid → int | `IPhaseSystem.ResolvePlayerAttackTargetUid` | 嘲讽重定向等实际目标解析 |

## 核心数据流示意（一次棋盘点击）

```
PointerHitRouter → GroundFieldHitSurface（查认领者）→ 对应 *InputHook
→ *InputController.Handle* → SendCommand(Submit*IntentCommand)
→ IntentIntake.Submit（两轴 + 合法性）→ Runtime.TrySubmitIntent
→ PresentationDirector（BuildScript → Batch 锁步 → Present 通道 → ack）
```

拒绝路径：Intake Reject → Command 发 `*RejectedEvent` + `[IntentIntake] Reject` 日志（含富化诊断后缀）。

## 关联 ADR

ADR-0004（意图类全部经收口）、ADR-0019（BoardWalk）、ADR-0020（房间/描述退役约定）、ADR-0025（Recycle/Pickup 持有语义）、ADR-0027（遗物丢弃）、ADR-0034（洗回/补牌表现纪律）。

## 不变量与坑

- **A 组意图命令不做门禁判断**，门禁全在 Intake——别在 Command 里再加放行条件。
- **B 组的门禁在 Controller 层**（`TryIntakeModal`），Command 本体是 Core 转发——直接 SendCommand B 组会绕过 Intake（现无生产调用这样做；新代码禁止）。
- `ApplyPickupItemCommand` 只能在 **ExternalHold 已取得后**调用（锁失败则 Core 未改，见《02》Pickup 次序纪律）。
- `IntentDisposition.BufferToDirector` 在 strict-drop 修订后实际不再产生，A 组命令仍兼容判断——阅读时勿误以为存在缓冲。
- 描述三命令为退役残留，禁止新增消费者。
- Query 全部无副作用；`EstimateWillKillQuery` 是**估算**（不含全部规则分支），只供表演分支选择，不做规则裁决。
