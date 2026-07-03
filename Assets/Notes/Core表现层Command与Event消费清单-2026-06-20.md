# Core 表现层 Command 与 Event 消费清单（2026-06-20）

## 接入结论

表现层当前应以 `CoreCommandDispatcher` 作为唯一入口，发送 Core Command，消费返回的 `PresentationBatch`，播放完批次后发送 `PresentationFinishedCommand(batchId)`。

推荐流：

```csharp
var dispatcher = new CoreCommandDispatcher(NineGridArchitecture.Current);
var result = dispatcher.Send(new AttackCommand(slot));

if (result.BatchOpened && result.Batch.RequiresAcknowledgement)
{
    // 表现层播放 result.Batch.Instructions
    dispatcher.Send(new PresentationFinishedCommand(result.Batch.BatchId));
}
```

表现层不要直接调用 Model 写方法、不要直接 `pipeline.Execute(...)`、不要直接构造 GameAction 改状态。查询展示状态优先用 `CoreViewSnapshot`，需要有效属性时用 Core Query/System 的只读入口。

## Command 清单

| Command | 参数 | GameCommandKind | 合法阶段 | 主要结果 |
|---|---|---|---|---|
| `StartNodeCommand` | `NodeDeckOptions options` | `StartNode` | `None`、`BuildEnemyPool`、`NodeCompleted`，且未锁输入 | 建池、开局发牌、补格、`NodeStarted`、进入 `InteractionLoop` |
| `AttackCommand` | `SlotId targetSlot` | `Attack` | `InteractionLoop`，且未锁输入 | 校验怪物/相邻/嘲讽，结算双方伤害，击杀后旋转/补牌/通关检查 |
| `PickupItemCommand` | `SlotId targetSlot` | `PickupItem` | `InteractionLoop`、`RewardItemChoice`、`RoomChoice`，且未锁输入 | 拾取非怪物卡到道具槽，互动期会旋转/补牌 |
| `ClickEmptyCommand` | `SlotId targetSlot` | `ClickEmpty` | `InteractionLoop`，且未锁输入 | 点击空格，触发互动计数、旋转、补牌 |
| `UseItemCommand` | `int itemUid` | `UseItem` | `InteractionLoop`、`RewardItemChoice`、`RoomChoice`，且未锁输入 | 触发 `UseItemAction` 和 `OnUseHelpCard` 效果 |
| `SelectRewardCommand` | `int optionIndex` | `SelectReward` | `RewardItemChoice`，且未锁输入 | 选择奖励，清奖励选择，进入房间选择并给出房间选项 |
| `SkipHelpChoiceCommand` | 无 | `SkipHelpChoice` | `RewardItemChoice`，且未锁输入 | 跳过帮助卡选择，结算跳过收益，进入房间选择 |
| `SelectRoomCommand` | `int optionIndex` | `SelectRoom` | `RoomChoice`，且未锁输入 | 选择房间，进入 `RoomEvent` |
| `EnterRoomCommand` | 无 | `EnterRoom` | `RoomEvent`，且未锁输入 | 结算房间，推进节点，可能进入 `NodeCompleted` / `Victory` |
| `PresentationFinishedCommand` | `int batchId` | `PresentationFinished` | 仅在 `PresentationSyncSystem.IsInputLocked == true` 时合法 | 校验 batch id，解锁 Core 输入 |

### Command 注意事项

- `PhaseSystem.LegalCommands` 是最终合法集合。表现层可以提前灰掉按钮，但 Core 仍会裁决。
- 输入锁期间只发送 `PresentationFinishedCommand`。如果表现层在锁定期间误发普通 Command，Core 会拒绝；`CoreCommandDispatcher` 不会打开新 batch。
- `UseItemAction` 内部支持 `selectedCardUids` 和 `selectedOption`，但公开的 `UseItemCommand` 目前只传 `itemUid`。需要选目标/选项的帮助卡表现交互，应先补 Command 参数。
- `StartNodeCommand` 可传自定义 `NodeDeckOptions`，用于测试/模拟；生产流程通常应由房间/节点规则构造 options。
- `CoreCommandResult.Accepted == false` 时，原因在 `Reason`；同时 `ActionRejected` 会进入 EventLog，QFramework 也会发 `Evt_ActionRejected`。

## PresentationBatch 消费契约

| 类型 | 作用 | 表现层用法 |
|---|---|---|
| `CoreCommandDispatchResult` | 一次 Command 的返回 | 读 `Accepted`、`Batch`、`BatchOpened` |
| `PresentationBatch` | 本次 Command 的表现批次 | 按 `Instructions` 顺序播放，结束后按需回执 |
| `PresentationInstruction` | 单条表现指令 | 读 `Kind` 分发动画，读 `Event` 获取 payload |
| `PresentationEventMapEntry` | Core event 到表现指令的映射 | 读 `Category`、`RequiresPlayback`、`LocksInput` |
| `CoreViewSnapshot` | 批次后状态快照 | 用于刷新棋盘、阶段、金币、奖励/房间选择 |
| `ActionLogRow` | 调试/战报投影 | 用于 debug 面板、回放日志、QA 对账 |

`PresentationBatchFactory` 只把 `RequiresPlayback == true` 的事件放入 batch instructions。`ActionStarted/ActionFinished` 默认不进表现播放，但会留在 EventLog 和 ActionLog。

## CoreEventType 清单

### Action / Rejection

| CoreEventType | InstructionKind | Category | LocksInput | 常用字段 |
|---|---|---|---|---|
| `ActionStarted` | `MarkActionStarted` | `ActionLifecycle` | 否 | `ActionId`、`ActionName`，调试用 |
| `ActionFinished` | `MarkActionFinished` | `ActionLifecycle` | 否 | `ActionId`、`ActionName`，调试用 |
| `ActionRejected` | `ShowRejectedIntent` | `Rejection` | 是 | `Amount` = command kind，`CardUid`，`FromSlot/ToSlot`，`Message` = reason |

### Damage / Stat / Economy

| CoreEventType | InstructionKind | Category | LocksInput | 常用字段 |
|---|---|---|---|---|
| `DamageDealt` | `ShowDamage` | `Damage` | 是 | `ActorUid`、`TargetUid`、`CardUid`、`Amount` = 规则后伤害，`Delta` = 实际扣减，`RemainingHp/Armor` |
| `HpChanged` | `UpdateHp` | `Stat` | 是 | `CardUid`、`Delta`、`RemainingHp/Armor` |
| `ArmorChanged` | `UpdateArmor` | `Stat` | 是 | `CardUid`、`Delta`、`RemainingHp/Armor` |
| `Healed` | `UpdateHp` | `Stat` | 是 | `CardUid`、`Amount` = 规则后治疗量，`Delta` = 实际恢复，`RemainingHp/Armor` |
| `GoldModified` | `UpdateGold` | `Economy` | 是 | `Amount` = 当前金币，`Delta` = 金币变化，`Message` = 原因 |
| `BaseStatModified` | `ModifyBaseStat` | `Stat` | 是 | `CardUid`、`Amount` = stat enum，`Delta`，`Message` |

### Card / Board Movement

| CoreEventType | InstructionKind | Category | LocksInput | 常用字段 |
|---|---|---|---|---|
| `CardMoved` | `MoveCard` | `Move` | 是 | `CardUid`、`FromSlot`、`ToSlot`、`SourceDefId/Cause` |
| `CardSwapped` | `SwapCards` | `Move` | 是 | `FromSlot`、`ToSlot` |
| `BoardRotated` | `RotateBoard` | `Rotate` | 是 | `Amount` = 1 / -1，`Message` = clockwise / counterClockwise |
| `CardDealt` | `DealCard` | `Deal` | 是 | `CardUid`、`FromSlot`、`ToSlot`、`Amount` |
| `CardSpawned` | `SpawnCard` | `Deal` | 是 | `CardUid`、`ToSlot`、`Message` = defId |
| `SlotsFilled` | `FillSlots` | `Deal` | 否 | `Amount` = 本次补入数量 |
| `DrawPileExhausted` | `ShowDrawPileExhausted` | `Deal` | 否 | 无核心 payload |
| `BoardMarked` | `MarkBoard` | `Board` | 是 | `FromSlot/ToSlot`、`Amount` = mark enum，`Delta` = 标记/取消 |

### Kill / Remove / Item

| CoreEventType | InstructionKind | Category | LocksInput | 常用字段 |
|---|---|---|---|---|
| `CardKilled` | `KillCard` | `Kill` | 是 | `ActorUid`、`TargetUid`、`CardUid`、`FromSlot` |
| `CardRemoved` | `RemoveCard` | `Remove` | 是 | `CardUid`、`FromSlot`、`RemovedAttack/Armor`、`Message` = reason |
| `ItemPicked` | `PickItem` | `Item` | 是 | `CardUid`、`FromSlot`、`Message` |
| `EmptyClicked` | `ClickEmpty` | `Interaction` | 否 | `FromSlot/ToSlot` |
| `ItemUsed` | `UseItem` | `Item` | 是 | `CardUid` = item uid，`TargetUid` = 首个选择目标，`Message` = option/cards |
| `InteractionChanged` | `UpdateInteractionCount` | `Interaction` | 否 | `Amount` = 当前互动次数，`Delta` |

### Effect / Content

| CoreEventType | InstructionKind | Category | LocksInput | 常用字段 |
|---|---|---|---|---|
| `EffectTriggered` | `TriggerEffect` | `Effect` | 是 | `CardUid` = owner uid，`Message` = effect id，`SourceDefId/Cause` |
| `EffectModifierApplied` | `ApplyModifier` | `Effect` | 否 | `CardUid`，`Amount` = stat/rule enum，`Delta`，`Message` = source |
| `EffectDeactivated` | `DeactivateEffect` | `Effect` | 否 | `CardUid`，`Message` = instance id，`SourceDefId/Cause` |
| `SkillGranted` | `GrantSkill` | `Content` | 否 | `Message` = skill defId |
| `RelicGranted` | `GrantRelic` | `Content` | 否 | `Message` = relic defId |
| `ContentLoaded` | `LoadContent` | `Content` | 否 | 内容加载提示 |

### Phase / Node / Reward / Room

| CoreEventType | InstructionKind | Category | LocksInput | 常用字段 |
|---|---|---|---|---|
| `PhaseChanged` | `ChangePhase` | `Phase` | 是 | `Amount` = phase enum |
| `NodeStarted` | `StartNode` | `Node` | 是 | `Amount`/`Message` 按 action 填充 |
| `NodeCompleted` | `CompleteNode` | `Node` | 是 | 节点完成事实 |
| `NodeAdvanced` | `AdvanceNode` | `Node` | 是 | `Amount` = node index，`Delta` 可表达 floor/node 变化 |
| `RewardOffered` | `OfferReward` | `Reward` | 是 | `Amount` = option count，`Message` = pool id |
| `RewardSelected` | `SelectReward` | `Reward` | 是 | `Amount` = option index，`Message` = reward defId |
| `RewardSkipped` | `SkipReward` | `Reward` | 是 | `Message` = reason |
| `RoomChoicesOffered` | `OfferRooms` | `Room` | 是 | `Amount` = option count |
| `RoomSelected` | `SelectRoom` | `Room` | 是 | `Amount` = option index / room enum，`Message` |
| `RoomResolved` | `ResolveRoom` | `Room` | 是 | `Amount` = room enum，`Message` |

## CoreViewSnapshot 字段（加厚版）

`CoreViewSnapshotFactory.Capture()` 投影全部 6 个 Model（含 `DeckModel`），并经由 `IStatSystem` 计算 effective stats。Flow 播放时以 `FlowPlaybackScope.Snapshot` 只读解析；卡面数字显示优先用 `Effective*` 字段；顶层 `Phase`/`Coins`/`BoardSlots` 等旧字段为兼容薄包装，读同一数据源。

### 顶层

| 字段 | 用法 |
|---|---|
| `Version` | 快照版本：`Board + Registry + Deck + Run + Player + PendingChoice` 的 `Version` 求和 |
| `Run` | 跑图元数据 |
| `Player` | 玩家 HUD + 养成 |
| `Board` | 棋盘 9 格 + 化身引用 |
| `Deck` | 牌区有序 uid 列表 |
| `Choice` | 奖励/房间选择态 |
| `Cards` | 可见区内卡的 `uid → CardView` 字典 |
| `TryGetCard(uid)` | 按 uid 查卡视图 |

### `RunView`

| 字段 | 来源 |
|---|---|
| `Phase` | `RunModel.Phase` |
| `NodeIndex` | `RunModel.NodeIndex` |
| `Floor` | `RunModel.Floor` |
| `Room` | `RunModel.Room` |
| `Seed` | `RunModel.Seed` |

### `PlayerView`

| 字段 | 来源 |
|---|---|
| `Coins` / `InteractionCount` | `PlayerModel` |
| `RelicDefIds` / `SkillDefIds` | `PlayerModel` 已获得遗物/技能 |
| `AvatarStats` | 化身 `CardInstance` + `StatSystem`（base + effective） |

### `BoardView` / `BoardSlotView`

| 字段 | 说明 |
|---|---|
| `Slots` | 9 格（index 1–9） |
| `AvatarUid` / `AvatarSlot` | 化身引用 |
| 每格 `Hp/Armor/Attack/MaxHp` | **base** 值 |
| 每格 `EffectiveHp/EffectiveArmor/EffectiveAttack/EffectiveMaxHp` | **effective** 显示值 |
| `Blessed` | `BoardModel.IsBlessed` |

### `DeckView`

| 字段 | 说明 |
|---|---|
| `DrawPileUids` | 抽牌堆（有序，顶 = index 0） |
| `ItemSlotUids` | 道具手牌槽（有序） |
| `PlayerCardPoolUids` / `EnemyCardPoolUids` | 节点开局暂存池 |

### `ChoiceView`

| 字段 | 说明 |
|---|---|
| `Kind` | `PendingChoiceKind`：None / Reward / Room |
| `PoolId` | 奖励池 id（`OfferReward` 时写入） |
| `RewardOptions` / `RoomOptions` / `SelectedRoom` | 待选与已选 |

### `CardView` / `CardStatView`

收录范围：当前棋盘 occupant + `DrawPile` + `ItemSlots` + 两 CardPool 中的卡。

| 字段 | 说明 |
|---|---|
| `Uid` / `DefId` / `Kind` | 卡身份 |
| `Zone` / `Slot` | 区域与棋盘格（非棋盘格 `Slot = None`） |
| `Stats` | `CardStatView`：六项 stat 的 base + effective 对 |
| `EffectIds` | 卡上绑定的效果 defId |

### 兼容顶层字段（只读转发）

`Phase`、`NodeIndex`、`Coins`、`InteractionCount`、`AvatarUid`、`AvatarSlot`、`PendingChoiceKind`、`BoardSlots`、`RewardOptions`、`RoomOptions`、`SelectedRoom` — 分别转发自 `Run` / `Player` / `Board` / `Choice`。

### 已知未纳入 Snapshot

| 缺口 | 说明 |
|---|---|
| 道具选项覆盖层（如 `stat_boost` 三选一） | Core 无 `PendingItemChoice`；当前由表现层 `SelectionOverlayFsm` 纯本地驱动 |
| 交互合法性 / 邻接 / 嘲讽 | 仍由 `PhaseSystem` 裁决，不进 Snapshot |
| `IsInputLocked` / `ActiveBatchId` | 属于 `PresentationSyncSystem`，非游戏显示态 |
| 活跃 `EffectInstance` 全量 | 卡面 `EffectIds` + effective stats 已够显示对齐；instance 级图标可二期补 |
| 坟场 / Removed 区 | 不在可见区，不投影 |

表现层终态由 **Flow 独占**（spawn/despawn/落位）+ **`StatEventProjection`**（批内 Stat 事件直连 `ApplyEvent` 刷数值）承担；开局一次性 `BuildInitialActors` 铺场。FSM/Shell 按需 `Capture` 查询。读 `snapshot.Deck` + `snapshot.Cards` + `snapshot.Board` + `snapshot.Choice`；卡面数字优先 `Effective*`。

## 表现层使用注意事项

1. **只通过 Command 改状态。** 点击格子、奖励、房间、道具都转为 Command；拖拽、hover、动画状态不要发 Command。
2. **批次播放期间锁输入。** `Batch.RequiresAcknowledgement == true` 时，播放完再发 `PresentationFinishedCommand(batchId)`。
3. **按 Sequence 顺序消费。** EventLog 是 append-only，batch 的 `FromSequence/ToSequence` 可用于 debug 和回放。
4. **不要按 event label 猜逻辑。** 动画可自由演，但逻辑事实以 `CoreGameEvent.Type` 和 payload 为准。
5. **拒绝也可能产生表现事件。** 非法命令会产生 `ActionRejected`，可显示轻提示或输入反馈。
6. **不要直接写 Model。** `BoardModel.PlaceCard`、`PlayerModel.AddCoins`、`Stats.SetBase` 等是 Core 内部 mutation API，不是表现层接口。
7. **UseItem 目标选择要补入口。** 现有公开 Command 尚不能传 selected cards / option；表现层做目标型帮助卡前应先补 Core Command。
8. **调试面板优先用 `ActionLogProjector`。** 它已经把事件 payload 投影为可读 summary，适合作为 QA/战报面板第一版。
