# 场上空格补牌（FillEmptySlots）触发路径与守卫矩阵

> 探查日期：2026-08-06（只读，未改任何代码）
> 范围：`NineGrid.Core`（动作/相位系统）+ `NineGrid.Presentation`（导演/时间线/调度器/命令查询）

## 0. 结论先行

1. **补牌没有任何 Core 统一收口**。`FillEmptySlotsAction` 在 Core 生产代码中只有 3 个入队点（开局管线 1 处 + 两个相位分拍内部各 1 处），全部由表现层导演脚本**逐条剧本手工排队**；`TriggerSystem` 上没有挂补牌触发器，pipeline 收尾没有自动 Fill。`BoardSystem.FillEmptySlots()` / `DeckSystem.SetupNode()` 是**无调用者的遗留 API**。
2. **触发矩阵有 5 类"空槽事件"有补牌跟随**（交战击杀 / 探索 / 翻面 / 用道具 / 融合 / 敌方阶段收尾），**拾取**走命令内整拍补牌（非导演分拍），**清关**按设计禁止补牌（ADR-0026）。
3. **漏点（无任何补牌跟随的路径）**：
   - **A. 攻击批内"非目标击杀/非击杀移除"**：`AttackIntentScriptFactory` 只用 `ContainsCardKilled(..., targetUid)` 判目标死亡（`IntentBatchProjection.cs:53-73`），目标没死但同批效果弄死/移除了**别的怪**时走非击杀分支；该分支**没有** DrainRefill（攻击工厂没有 `DrainRefillScheduler`，与 `UseItemIntentScriptFactory` 不对称）。若同时场上没有可参加敌方行动的怪（`EnemyActionPhaseScheduler.Append` 的 `HasParticipatingEnemy` 早退，`EnemyActionPhaseScheduler.cs:51-54`），收尾补牌也不会发生 → 空槽一直挂到下一次 explore/reveal/useItem/kill。
   - **B. Fill 批次内部的 OnDeal/OnEnter 连锁移除**：`FillEmptySlotsAction.GetPostTriggerPoints` 派发 OnDeal+OnEnter（`BoardDeckActions.cs:410-413`）；若刚被补进来的卡其 OnEnter 效果又移除/杀死另一张盘面卡，该槽在**本次批次内不会再补**（没有链式回填），要等下一次补牌机会。
   - **C. 攻击批内击杀发生在旋转批之后的融合/移除**：结构性延迟而非永久漏（下一次互动会补），与 A 合并描述。
4. **守卫会跳过补牌的路径**：清关（`IsNodeCleared`，双保险：Presentation 剧本短路 + Core 分拍内部）；终局相位（`IsTerminalPhase`，Victory/Defeat）；非 InteractionLoop 相位（只有 Drain/Fusion 两个调度器检查，`ResolvePostKillFillInternal` 只查"非终局"）；牌堆空（drain 判定 + FillEmptySlotsAction 内 `break`）；无空位（drain/fusion 判定）。详见 §3。

---

## 1. `FillEmptySlotsAction.Apply` 内部前提（关键行照抄）

文件：`Assets\Scripts\NineGrid.Foundation\NineGrid.Core\Domain\Actions\BoardDeckActions.cs`（类声明 L339）

| 前提 | 代码 | 行号 |
|---|---|---|
| fillOrder 固定为顺时针路径（1→2→3→6→9→8→7→4） | `IReadOnlyList<SlotId> fillOrder = RotateBoardClockwiseAction.ClockwisePath;` | L358 |
| 开局相位附带 Avatar 出场事件（仅发事件，不影响补牌本身） | `if (run.Phase.Value == GamePhase.DealOpeningCards && board.AvatarUid.Value > 0)` | L360 |
| Avatar 槽与已有卡的槽跳过 | `if (slot == board.AvatarSlot.Value || !board.IsEmpty(slot)) continue;` | L374 |
| 抽牌堆空 → 停止补牌（`break`，剩余槽保持空） | `if (!deck.TryPeekDrawPile(out uid)) break;` | L380-383 |
| 出牌 + 落格 + 记数 | `deck.RemoveUid(uid); board.PlaceCard(card, slot); filled++;` | L386-388 |
| 每次最多补几张 | **无上限**：一轮补满所有空槽（至多 8−Avatar），只受牌堆枯竭中断 | L371-397 |
| 补牌数事件 + 牌堆枯竭事件 | `SlotsFilled`(L399) / `DrawPileExhausted`(L402-405) | L399-405 |
| **相位门禁** | **动作内部没有相位门禁**；门禁全部在调用方（见 §3.2） | — |
| **清关门禁** | **动作内部没有清关门禁**；门禁在调用方 `ResolvePostKillFillInternal`/`ResolveFillEmptySlotsBatchInternal` | — |
| OnDeal / OnEnter 触发 | `GetPostTriggerPoints` 返回 `AfterAction, OnDeal, OnEnter` | L341-346, L410-413 |

## 2. 触发矩阵：空槽事件 → 补牌跟随 → 排队者 → 守卫

### 2.1 有补牌跟随的路径

| # | 空槽事件 | 补牌跟随 | 排队者（类/方法） | 守卫条件（if 判定） |
|---|---|---|---|---|
| P1 | 玩家交战击杀（Attack kill） | ✅ `ResolvePostKillFill` 分拍（独立 batch） | `AttackIntentScriptFactory.EnqueueKillAftermath`（分支条件 `mLastHitKilledTarget`，由 `ResolveHitAndProject`→`IntentBatchProjection.ContainsCardKilled(..., targetUid)` 判定，`AttackIntentScriptFactory.cs:413`） | ① 剧本层：`IsNodeCleared()` 为真 → 跳过 Fill/Fusion/敌方阶段，只走旋转批（`AttackIntentScriptFactory.cs:215-227`）；② Core 层：`IsTerminalPhase`（`PhaseSystem.cs:1800`）+ `IsNodeCleared`（`PhaseSystem.cs:1807`） |
| P2 | 探索点空格（ClickEmpty，OnInteract 效果可移除/击杀） | ✅ 无条件脚本批次 `ResolvePostKillFill` | `ExploreIntentScriptFactory.BuildScript`：clickGate → interactGate → fillGate → rotateGate → FusionRefill → EnemyActionPhase（`ExploreIntentScriptFactory.cs:92-125`） | 无脚本层守卫（无条件排）；Core 层 `IsTerminalPhase`/`IsNodeCleared` 兜底（`PhaseSystem.cs:1800-1810`） |
| P3 | 主动翻面（RevealFace，翻出机关等触发效果可移除/击杀） | ✅ 无条件脚本批次 `ResolvePostKillFill` | `RevealFaceIntentScriptFactory.BuildScript`：revealGate → interactGate → fillGate → rotateGate → FusionRefill → EnemyActionPhase（`RevealFaceIntentScriptFactory.cs:80-124`） | 同上（无脚本层守卫） |
| P4 | 使用道具（UseItem）— 击杀 | ✅ `ResolvePostKillFill` 分拍 | `UseItemIntentScriptFactory.EnqueueKillAftermath`（分支 `mLastUseKilledTarget`，由 `ContainsAnyCardKilled` 判定**任意**卡死亡，`UseItemIntentScriptFactory.cs:201`、`251-263`） | ① `IsNodeCleared()` 短路（`UseItemIntentScriptFactory.cs:148-160`）；② Core 层同上 |
| P5 | 使用道具 — 非击杀移除（drain 退场，如 RemoveCard 原子） | ✅ `ResolveDrainRefill` 独立批次 | `UseItemIntentScriptFactory` → `mDrainRefill.AppendAfterPresentIfNeeded` → `DrainRefillScheduler.EnqueueRefillBatches`（`UseItemIntentScriptFactory.cs:131-140`、`207-209`）；命令包装 `ResolveDrainRefillBatchCommand` / `ShouldDrainRefillQuery` | `DrainRefillScheduler.ShouldRefill`（`DrainRefillScheduler.cs:82-102`）：① `phase != InteractionLoop → false`（L90-93）；② `DrawPileUids.Count <= 0 → false`（L96-99）；③ 无空位 `HasEmptyBoardSlot`（L101）；外加 `mLastUseNeedsDrainRefill = !killed && !fusion && ShouldRefill`（`UseItemIntentScriptFactory.cs:207-209`）。Core 侧 `ResolveFillEmptySlotsBatchInternal` 再查 `skipFill || IsTerminalPhase || IsNodeCleared`（`PhaseSystem.cs:1839`） |
| P6 | 融合（SkeletonFusion：`skill.recombine_head`/`recombine_body`/`strong_combo`，`SkeletonFusionPresentationScanner.cs:38-40`） | ✅ `ResolveFusionRefill` 独立批次（排除结果 UID 重排牌堆，Flow 策略，Core 只跑 `FillEmptySlots`） | ① 剧本门：`FusionRefillScheduler.AppendAfterRotatePresent`（`FusionRefillScheduler.cs:70-117`），被 Attack/Explore/Reveal/UseItem 四个工厂调用；入队走 `FusionRefillScheduler.EnqueueRefillBatches`（L119-166）；② 兜底：`FusionRefillAftermath.TrySchedule`（`FusionRefillAftermath.cs:34-48`），由 `BoardPresentationPlayer` PresentEnd 调用（`BoardPresentationPlayer.cs:1416-1425`） | ① `hadFusion` 分支（`mLastRotateHadFusion` 等，由 `FusionRefillPlanner.TryCollectResultUids` 扫 `EffectTriggered`+融合 skill 判定，`FusionRefillPlanner.cs:14-58`）；② `IsRefillScheduled` 防重（`FusionRefillScheduler.cs:36-39`、`FusionRefillAftermath.cs:36`）；③ 执行时 `phase != InteractionLoop → SkipFill`、`drawCount <= 0 → SkipFill`、`emptyCount <= 0 → SkipFill`（`FusionRefillScheduler.cs:193-224`）；④ 全部候选被 exclude → **回退为不过滤补牌**（L226-241） |
| P7 | 敌方行动阶段（单向打击致死不会空槽；阶段内反伤/反甲**击杀怪**会空槽） | ✅ 阶段收尾统一补牌 `ResolveEnemyActionFinale`（内部 = `ResolvePostKillFillInternal` + `CompleteNodeIfCleared`，不旋转） | `EnemyActionPhaseScheduler.EnqueueFinale`（`EnemyActionPhaseScheduler.cs:198-218`，命令 `ResolveEnemyActionFinaleCommand` → `PhaseSystem.ResolveEnemyActionFinale`）；整拍路径由 `RunEnemyActionPhaseInternal` 循环后统一收尾（`PhaseSystem.cs:1569-1579`） | ① `HasParticipatingEnemy` 早退（`EnemyActionPhaseScheduler.cs:51-54`、`300-326`）→ **没怪参加就没有收尾补牌**；② Core 侧 `IsTerminalPhase` 早退（`PhaseSystem.cs:1717-1720`）；③ 阶段内盘面冻结，报名/逐条不补牌（ADR-0012，`PhaseSystem.cs:1680-1711`） |
| P8 | 拾取帮助卡（Pickup，`PickupCardAction` 腾空槽） | ✅ 补牌，但**不是导演分拍**：命令内整拍 `ResolveInteractiveRotation`（计数+补牌+旋转+敌方行动焊在一起） | `PhaseSystem.ExecutePickupItem`（`PhaseSystem.cs:617-649`）：`shouldRotate = requireInteractionLoopRotate && CurrentPhase == InteractionLoop`（L639）→ `ResolveInteractiveRotation()`（L1552-1567） | `InteractionLoop` 才补牌+旋转（L639）；非互动相位拾取不补牌（符合 ADR-0020：房间选项不算互动） |
| P9 | 开局（StartNode 管线内 `OpeningDealAction` 后仍有空槽） | ✅ 管线内直接 `FillEmptySlotsAction` | `PhaseSystem.StartNode`（`PhaseSystem.cs:172`），随后 `CompleteNodeIfCleared`（L177） | 无（开局相位；Avatar 出场事件由 L360 分支发出） |
| P10 | 清关（离开机关击破） | ❌ **按设计禁止补牌**（ADR-0026） | —（跳过 Fill 直接旋转批收场） | 双层守卫：① 剧本层 `AttackIntentScriptFactory.cs:215` / `UseItemIntentScriptFactory.cs:148`；② Core 层 `ResolvePostKillFillInternal`（`PhaseSystem.cs:1807`）/ `ResolveFillEmptySlotsBatchInternal`（`PhaseSystem.cs:1839`）/ `ResolvePostKillRotateInternal` 转 `CompleteNodeIfCleared`（`PhaseSystem.cs:1825-1828`） |

### 2.2 效果原子（EffectAtomLibrary）在各类批次中的覆盖

| 原子 | 是否空槽 | 所在批次 | 覆盖情况 |
|---|---|---|---|
| `DealDamage`（→ `KillIfDeadAction` follow-up，`CoreActions.cs:181-184`） | 击杀时空槽 | UseItem 批 | P4（任意卡击杀）✅ |
| | | Attack 批 | 目标死 → P1 ✅；**非目标死 → 漏点 A** ❌ |
| | | Explore/Reveal 批 | 后续无条件 Fill 批 P2/P3 ✅ |
| | | Fill 批内（OnDeal/OnEnter 连锁） | **漏点 B** ❌ |
| `RemoveCard`（`EffectAtomLibrary.cs:3755-3780` → `RemoveCardAction`） | 移除即空槽（门保护除外，`CoreActions.cs:515-523`） | UseItem 批 → P5（drain）✅；Explore/Reveal 批 → P2/P3 ✅；Attack 批非目标 → **漏点 A** ❌；Fill 批内 → **漏点 B** ❌ |
| `MoveToDrawPile`（L3273-3296 → `ShuffleCardIntoDrawPileAction`） | 回抽牌堆空槽 | 同上（按批次归属，与 RemoveCard 同表） | |
| `Move`（`MoveCardAction`，`EffectActions.cs:225-282`） | 可能腾空/填掉槽 | 同上 | 同表 |
| `ExchangeWithDrawPile`（L794-876） | 不空槽（换入卡占位，除非牌堆无候选 no-op） | — | ✅ 无需补牌 |
| `Spawn` / `ShuffleInto` | 填槽/洗入 | — | 不产生空槽（Spawn 占格） |
| `MarkLeaveTrapBroken`（`EffectActions.cs:1457-1467`） | 置清关标志 | 任何批 | 触发 P10 清关 → 补牌被禁 |

### 2.3 机关（Trap）触发致死/移除

- 常规机关：效果在交战/翻面/探索批次内结算 → 落入对应批次覆盖（P1/P2/P3/P4）。
- 离开机关（`trap.leave`）：门 + 魔免 + 离开；只吃交战伤害（`CoreActions.cs:77-82` 门保护、`DeckSystem.cs:22` 常量、`EffectActions.cs:1457`）；击破 → 清关 → P10。
- 机关在 Fill 批次内被补上场并自触发（OnDeal/OnEnter）→ 漏点 B 范围。

## 3. 漏点与守卫跳过清单（重点）

### 3.1 漏点（无补牌跟随或补牌会被永久跳过）

1. **漏点 A —— Attack 批内非目标死亡 / 非击杀移除（无 drain 补牌）**
   - 证据：`AttackIntentScriptFactory` 只有 `mFusionRefill` + `mEnemyAction` 两个调度器（`AttackIntentScriptFactory.cs:28-29`），**没有 `DrainRefillScheduler`**；非击杀分支 = `EnqueueNonKillInteractionAdvance` + `AppendEnemyActionPhase`（`AttackIntentScriptFactory.cs:261-265`）。
   - 杀伤判定只看目标：`IntentBatchProjection.ContainsCardKilled(pipeline, startIndex, targetUid)` 精确匹配 targetUid（`IntentBatchProjection.cs:53-73`，调用点 `AttackIntentScriptFactory.cs:413`）；对比 UseItem 路径的 `ContainsAnyCardKilled`（`UseItemIntentScriptFactory.cs:201、251-263`）——**两条路径判定口径不一致**。
   - 若同批效果杀死/移除的是另一张怪（AOE、OnDamageTaken 自毁、OnKill 召唤类自移除等），且场上没有参与敌方行动的怪（`EnemyActionPhaseScheduler.cs:51-54` 早退），**收尾补牌也不会发生** → 空槽滞留到下一次有 fill 的互动。
2. **漏点 B —— Fill 批次内 OnDeal/OnEnter 连锁移除不链式回填**
   - 证据：`FillEmptySlotsAction.GetPostTriggerPoints` 含 OnDeal/OnEnter（`BoardDeckActions.cs:410-413`）；批次内再无第二个 Fill（同一批次 `ResolvePostKillFillInternal` 只 enqueue 一次，`PhaseSystem.cs:1812-1814`）。
   - 语义后果：新入场卡的 OnEnter 效果移除别的卡 → 该槽空到下一次补牌机会（下一次互动的 fill/refill/finale）。
3. **漏点 C —— UseItem 击杀/非击杀路径均无敌方行动阶段与互动计数**
   - 证据：`UseItemIntentScriptFactory.EnqueueKillAftermath`（`UseItemIntentScriptFactory.cs:143-184`）与非击杀分支（L108-140）都没有 `AppendEnemyActionPhase` / `AdvanceInteractionCount`；Core 侧 `ExecuteUseItem` 也不跑（`PhaseSystem.cs:831-845`）。用道具不推进互动计数、不触发齐射——**与 ADR-0012「每次九宫格互动的结算链末尾追加敌方行动阶段」的互动定义如何对齐存疑**（可能是有意：道具不算九宫格互动）。若为有意，则道具击杀后空槽靠 P4 的 Fill 覆盖，不会漏。

### 3.2 守卫会让补牌被跳过的路径（非漏点，但需知晓）

| 守卫 | 代码位置 | 后果 |
|---|---|---|
| `skipFill || IsTerminalPhase || IsNodeCleared` | `PhaseSystem.cs:1839`（Drain/Fusion 共用收口） | 终局（Victory/Defeat）或清关后任何 drain/fusion refill 空转（开空批 ack） |
| `IsTerminalPhase` | `PhaseSystem.cs:1800`（PostKillFill） | 战败/胜利后不补牌 |
| `IsNodeCleared` | `PhaseSystem.cs:1807`、`1825`；剧本层 `AttackIntentScriptFactory.cs:215`、`UseItemIntentScriptFactory.cs:148` | 离开机关击破当拍不补牌（ADR-0026，防发牌飞行卡死主线） |
| `phase != InteractionLoop` | `DrainRefillScheduler.cs:90-93`、`FusionRefillScheduler.cs:193-202` | **Drain/Fusion 只在 InteractionLoop 补牌**；注意 `ResolvePostKillFillInternal` 无此检查（只查非终局）——若未来有脚本在非互动相位发 Fill 命令，Core 不会拦 |
| `DrawPileUids.Count <= 0` | `DrainRefillScheduler.cs:96-99`、`FusionRefillScheduler.cs:204`、`FillEmptySlotsAction.cs:380-383`（break） | 牌堆枯竭：drain/fusion 直接 skipFill；Fill 动作提前 break，剩余空槽保持空（此时通常已接近清关） |
| 无空位 | `DrainRefillScheduler.cs:101`、`FusionRefillScheduler.cs:215-224` | 空批 ack，不写 Core |
| 候选全被 exclude（融合结果） | `FusionRefillScheduler.cs:226-241` | **不跳过**，回退为不过滤补牌（防止静默空批） |

## 4. Core 有无统一收口的补牌不变量？——结论：没有，完全依赖表现层导演手工排队

**结论**：Core **没有**任何「批处理收尾统一 Fill」的机制。补牌是**逐条剧本、逐批次手工排队**，由表现层 `IntentScriptFactory` 决定何时发 `ResolvePostKillFillCommand` / `ResolveDrainRefillCommand` / `ResolveFusionRefillCommand` / `ResolveEnemyActionFinaleCommand`。

证据链：

1. **FillEmptySlotsAction 生产入队点全枚举**（`grep -n "new FillEmptySlotsAction()"` 全仓 9 处）：
   - `PhaseSystem.cs:172`（StartNode 管线）
   - `PhaseSystem.cs:1813`（`ResolvePostKillFillInternal`）
   - `PhaseSystem.cs:1845`（`ResolveFillEmptySlotsBatchInternal`，被 `ResolveFusionRefill`/`ResolveDrainRefill` 共用）
   - `DeckSystem.cs:51`（`SetupNode` 内）与 `BoardSystem.cs:89`（`FillEmptySlots()`）——**无任何调用者**（`grep '\.FillEmptySlots()'` / `grep '\.SetupNode('` 均零命中），属遗留 API。
   - 其余 5 处在测试里。
2. **触发器系统没有补牌挂钩**：`DeckSystem.RebindSystemTriggers`（`DeckSystem.cs:31-43`）只挂了 leave-trap 插入（OnKill Post → `ShuffleIntoDrawPileAction`），**没有 OnKill→Fill 的系统级触发器**。没有 `TriggerPoint` 上自动 Fill 的机制。
3. **pipeline 收尾没有自动 Fill**：`IActionPipelineSystem.RunToCompletion` 只跑已入队动作；`FillEmptySlotsAction` 必须显式入队。
4. **PresentationDirector 无链尾兜底**：`PresentationDirector.cs:78、198` 只是把 intent 交给 `BuildScript`；唯一接近「兜底」的是 `FusionRefillAftermath.TrySchedule`（融合 Present 后的补挂），且仅针对融合、且要 `IsRefillScheduled == false` 才生效（`FusionRefillAftermath.cs:34-48`）——不是通用空槽兜底。
5. **补牌前置判定也散落在两处**：Core 侧只有「非终局 + 非清关」（`PhaseSystem.cs:1800、1807、1839`）；InteractionLoop 要求只在 Presentation 的 `DrainRefillScheduler.ShouldRefill`（`DrainRefillScheduler.cs:90`）与 `FusionRefillScheduler.ResolveAndProject`（`FusionRefillScheduler.cs:193`）里。即「InteractionLoop 才补牌」这个规则**根本不是 Core 不变量**，只是表现层调度器的选择。
6. **表现层清关短路是双份**：剧本层（`AttackIntentScriptFactory.cs:215`、`UseItemIntentScriptFactory.cs:148`）与 Core 层（`PhaseSystem.cs:1807、1839`）各有一份 `IsNodeCleared` 检查——再次证明无单一收口，靠约定重复防守。

**推论**：新增一种会产生空槽的交互（新的 intent 类型、新的效果原子）时，**补牌不会自动跟随**；必须在该路径的剧本里显式挂 Fill/Drain/Fusion 批次，否则空槽会滞留（漏点 A/B 即此类缺口）。

## 5. 遗留问题

1. **UseItem 不触发互动计数/敌方行动阶段**（§3.1 漏点 C）：是有意（道具不算九宫格互动）还是遗漏？若为有意，建议在 ADR 中显式记录；与 ADR-0012「每次九宫格互动……」的表述对齐。
2. **攻击路径与用牌路径的击杀判定口径不一致**：`ContainsCardKilled(targetUid)` vs `ContainsAnyCardKilled`（`IntentBatchProjection.cs:53` vs `UseItemIntentScriptFactory.cs:251`）。攻击批内非目标死亡是否应触发补牌（现行为：不触发 → 漏点 A）。
3. **Fill 批内 OnDeal/OnEnter 连锁移除**（漏点 B）当前无测试覆盖；需确认内容侧是否存在 OnEnter 自移除/移除其他卡的卡，若存在则需链式回填或在 ADR 中定义「补牌批内移除不追补」。
4. `ResolvePostKillFillInternal` 无 InteractionLoop 检查（`PhaseSystem.cs:1798-1815`）：若未来剧本在 RoomChoice/RewardItemChoice 相位误发 `ResolvePostKillFillCommand`，Core 会照常补牌（仅拦终局与清关）。
5. `BoardSystem.FillEmptySlots()` / `DeckSystem.SetupNode()` 无调用者（`BoardSystem.cs:87-90`、`DeckSystem.cs:45-53`）：建议清理或注明遗留，避免将来被当成「统一收口」误用。
6. 拾取路径补牌是命令内整拍（`PhaseSystem.cs:639-648` 的 `ResolveInteractiveRotation`），与其余路径的导演分拍风格不同（ADR-0012 已声明分拍为模板）——是否把 Pickup 也拆成分拍属于重构议题，非本次范围。

## 附：关键文件行号索引

- `BoardDeckActions.cs` — FillEmptySlotsAction L339-414
- `PhaseSystem.cs` — StartNode L148-179；Attack L207-284；ApplyCombatHit L435-492；ExecutePickupItem L617-649；ExecuteUseItem L816-845；ResolveInteractiveRotation L1552-1567；RunEnemyActionPhaseInternal L1569-1579；ResolveNextEnemyActionInternal L1645-1711；ResolveEnemyActionFinaleInternal L1713-1726；ResolvePostKillFillInternal L1798-1815；ResolveFillEmptySlotsBatchInternal L1837-1847；CompleteNodeIfCleared L1849-1872；IsTerminalPhase L1973-1976
- `CoreActions.cs` — DealDamageAction L9-242（KillIfDeadAction follow-up L177-184）；KillAction L558-629；RemoveCardAction L477-556
- `AttackIntentScriptFactory.cs` — L206-259（EnqueueKillAftermath）、L261-302（非击杀分支）、L399-426（ResolveHitAndProject）
- `ExploreIntentScriptFactory.cs` — L81-125；`RevealFaceIntentScriptFactory.cs` L80-124
- `UseItemIntentScriptFactory.cs` — L103-141（剧本）、L143-184（击杀分支）、L186-216（ResolveUseAndProject）
- `DrainRefillScheduler.cs` — ShouldRefill L82-102；EnqueueRefillBatches L41-80
- `FusionRefillScheduler.cs` — AppendAfterRotatePresent L70-117；EnqueueRefillBatches L119-166；ResolveAndProject L168-332；SkipFill L334-351
- `FusionRefillPlanner.cs` — TryCollectResultUids L14-58；BuildRefillDrawOrder L121-144
- `FusionRefillAftermath.cs` — TrySchedule L34-48；CreateProductionScheduler L51-133
- `EnemyActionPhaseScheduler.cs` — Append L21-65；HasParticipatingEnemy L300-326；EnqueueFinale L198-218
- `BoardPresentationPlayer.cs` — FusionRefillAftermath.TrySchedule 调用点 L1416-1425
- `IntentBatchProjection.cs` — ContainsCardKilled L53-73
- `PresentationCompositionRoot.cs` — 工厂装配 L108-155；Aftermath 注册 L139-146
- `SkeletonFusionPresentationScanner.cs` — 融合 skill 清单 L38-52
- ADR：`docs/adr/0012-enemy-action-phase-volley.md`（收尾补牌 L19、冻结 L21）、`docs/adr/0026-leave-trap-sole-clear-condition.md`（击破当拍禁 Fill L17）
