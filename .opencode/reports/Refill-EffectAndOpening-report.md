# 补牌调查报告：效果驱动的卡牌移除/击杀补牌 + 节点开局补牌漏失

日期：2026-08-06
范围：只读代码分析（Core 与 Presentation），未改动任何文件。

## 结论先行

1. **没有任何效果原子（Kill / RemoveCard / ShuffleInto / MoveToDrawPile / ExchangeWithDrawPile / Spawn 等）会自行 Enqueue `FillEmptySlotsAction`。** 补牌永远是「分拍/批次」级的显式命令，由 `PhaseSystem` / `DeckSystem` / 表现层 intent 脚本在效果执行**之后**单独发起。
2. **触发点决定洞由谁补，且存在一条「补不到」的路径：**
   - 效果在 `OnKill` / `OnBattle` / `AfterAction`（玩家交互 intent 内）触发 → 洞由该 intent 后续的 `ResolvePostKillFill` 命令补（表现层 Attack/Explore/UseItem/RevealFace 流程都这么排）；战中被敌方行动收尾 `ResolveEnemyActionFinale → ResolvePostKillFillInternal` 再兜底一次。
   - 效果在 **FillEmptySlots 自身的 Post 触发（OnDeal/OnEnter）** 触发（捕熊陷阱就是）→ 洞出现在 fill 循环**之后**，本批内无人补；战中靠敌方行动收尾兜底，**但 StartNode 开局批次没有收尾 fill → 洞一直留到玩家第一次行动**。
3. **用户报告的「节点三进入时有空缺却不补牌」机制成立，根因是捕熊陷阱（trap.bear_trap）在开局 FillEmptySlots 的 Post 触发里打怪 + 自移除，产生开局洞，且 StartNode 批次之后没有任何补牌命令。** 表现层开局是忠实回放 Core 盘面的（不是自己一套发牌视图），所以 Core 的空槽就是玩家看到的空槽。
4. **「开局盘面 < 8 且抽牌堆非空」不是张数不足造成的**（节点 3 装填约 20+ 张，fill 必然能铺满 8 格），而是开局批次内效果移除（bear trap 击杀 + 自移除）造成的；`OnNodeStart` 的遗物注入（药水袋/飞刀袋 Spawn 进抽牌堆）发生在 fill 之后，只会让「抽牌堆非空」成立，不产生洞。

---

## 线 A：效果侧补牌

### A1. 会让场上卡离场的原子清单（按实际代码命名）

| 原子（template 名） | 落地的 Action | 行为 | 位置 |
|---|---|---|---|
| `RemoveCard` | `RemoveCardAction` | 板上卡 → `ZoneId.Removed`（放逐），发 `CardRemoved`；门保护下直接无效；`clearResidual*` 原因不派 OnRemove/OnCumulative | `EffectAtomLibrary.cs:3756`；`CoreActions.cs:477` |
| `Kill`（非原子，由伤害管线产出） | `KillAction` / `KillIfDeadAction` | 板上卡 → Graveyard，发 `CardKilled` + `CardRemoved`；`KillIfDeadAction` 是 `DealDamageAction` 致死 follow-up | `CoreActions.cs:558`；`EffectActions.cs:83`；致死挂接 `CoreActions.cs:177-184` |
| `MoveToDrawPile` | `ShuffleCardIntoDrawPileAction` | 板上卡 → 抽牌堆（top/洗牌），发 `CardDealt` | `EffectAtomLibrary.cs:3273`；`EffectActions.cs:406` |
| `ShuffleInto` | `ShuffleIntoDrawPileAction` | 新建 N 张卡 → 抽牌堆（不回板，不产生洞，但会进洞的坑） | `EffectAtomLibrary.cs:3248`；`EffectActions.cs:326` |
| `ExchangeWithDrawPile` | `ExchangeWithDrawPileAction` | 板上卡与抽牌堆一张交换（换出进堆，换入占原格，不产生洞） | `EffectAtomLibrary.cs:3432`；`EffectActions.cs:794` |
| `Spawn` | `SpawnCardAction` | 造卡进 DrawPile/Board/ItemSlots/池；`OnNodeStart`+PlayerCardPool+HelpCard 时 `nodeStartDrawPileGrant` → 直接 AddToDrawPile | `EffectAtomLibrary.cs:3298`；`EffectActions.cs:477`（grant 分支 522-537） |
| `ForceBattle` | `ForceBattleAction` | 交战（伤害管线），可致死 | `EffectAtomLibrary.cs:3472`；`EffectActions.cs:159` |
| （无 `Banish`/`Transform` 专用原子；放逐即 `RemoveCard destination=Removed`，最接近 Transform 的是 `ExchangeWithDrawPile`） | | | |

**证据：`FillEmptySlotsAction` 的全部 Enqueue 点（全仓库仅 6 处，其中 3 处在测试）：**
- `PhaseSystem.cs:172`（StartNode）、`PhaseSystem.cs:1813`（ResolvePostKillFillInternal）、`PhaseSystem.cs:1845`（ResolveFillEmptySlotsBatchInternal / fusion·drain refill）
- `DeckSystem.cs:51`（SetupNode 独立入口）
- `BoardSystem.cs:89`（同步 Execute 工具入口）
- 测试：`Batch3HardSkillsRegressionTests.cs:94`、`TrapBatch2RegressionTests.cs:152/235/346`

即：**效果原子层完全不负责补牌**，全部依赖外部分拍。`ActionPipelineSystem.ResolveAction` 把 Apply → Post 触发 → follow-up 在**同一次 RunToCompletion 内**同步消化（`ActionPipelineSystem.cs:135-155`），而 Fill 是单独的 action/command，时序上「谁先谁后」完全由调用方决定。

### A2. 两个玩家可见 case 的效果装配

**① 捕熊陷阱（trap.bear_trap）**
- 装配：`trap_bear_trap.json:77-87` → `effectAssemblies[0]` `trap.bear_trap.fill` ← `tpl.trap.bear_trap.fill`，args `{"amount":10,"reason":"trap.bear_trap"}`
- 模板：`effect_templates.json:1275-1281`
  - trigger：`OnDeal`
  - conditions：`ActionSource(action=FillEmptySlots)` + `EventFilter(eventType=CardDealt)` + `Adjacent(left=Self, right=EventCard)`
  - target：`EventCard`（本批第一个 CardDealt 事件的卡 uid，`EffectRuntimeContext.cs:122-134`；`Adjacent` 取该卡当前格，`EffectAtomLibrary.cs:1948-1967`）
  - action：`Sequence[ Conditional(Monster→DealDamage 10 actor=Self；else HelpCard→RemoveCard(EventCard))，RemoveCard(Self→Removed) ]`
- 语义：**在 fill 补牌的 Post 触发窗内**，若真怪/道具被发到陷阱正交邻格 → 打 10 或移除道具，随后陷阱必然自移除。10 伤致死 → `KillIfDeadAction → KillAction`（同 run 内 follow-up）。
- 该效果产生的空格**由哪条分拍补**：
  - 战中的 fill 批：本批不补（fill 循环已跑完）；由本 intent 尾部敌方行动收尾 `ResolveEnemyActionFinaleInternal → ResolvePostKillFillInternal`（`PhaseSystem.cs:1713-1726, 1798-1815`）兜底补。
  - **开局 StartNode 批：没有任何后续补牌命令 → 洞永久残留到玩家第一次行动**（详见线 B）。

**②「杀够 N 只怪就移除一只怪」遗物 = 恐怖面罩（relic.terror_mask）**
- 装配：`relic_terror_mask.json:78-96` 三个 assembly：
  - `relic.terror_mask.attack` ← `tpl.relic.wood_sword.base`（Modifier 攻击+1，`effect_templates.json:419-425`）
  - `relic.terror_mask.node_start` ← `tpl.relic.terror_mask.node_start`（`OnNodeStart` → SetCounter 清零，`effect_templates.json:1707-1713`）
  - `relic.terror_mask.remove` ← `tpl.relic.terror_mask.remove`（`effect_templates.json:1675-1681`）：trigger `OnCumulative(metric=monsterRemoved, threshold=6, counterKey=relic.terror_mask.remove)`；target `FilteredCards(kind=Monster, zone=Board, excludeBoss, random, count=1)`；action `RemoveCard(destination=Removed, reason=relic.terror_mask)`。
- `monsterRemoved` 计数来源：`RemoveCardAction` / `KillAction` 的 Post 触发点都含 `OnCumulative`（`CoreActions.cs:479-484, 560-566`），`OnCumulativeTrigger.MeasureDelta` 按 `CardRemoved` 事件累计真怪（`EffectAtomLibrary.cs:901-914`；`clearResidual*` 除外）。
- **该效果产生的空格由哪条分拍补**：它只在「击杀发生的那个批次内」触发（kill → Post OnCumulative → RemoveCard reaction 在同一次 RunToCompletion 内消化，`ActionPipelineSystem.cs:151-152, 178-194`）。若击杀发生在玩家交互 intent（Attack/Explore/UseItem/RevealFace）里，洞由该 intent 后续 `ResolvePostKillFill`（`AttackIntentScriptFactory.cs:229-240`、`ExploreIntentScriptFactory.cs:96`、`UseItemIntentScriptFactory.cs:164`、`RevealFaceIntentScriptFactory.cs:94`）补——因为 fill 命令是独立批，晚于效果 run。若击杀发生在 Fill 批自身的 Post 链里（如 bear trap 在 fill 里打死的怪，其 OnKill→terror_mask 又在同 run 里拆走另一只怪），则和 bear trap 一样：本批不补、战中等敌方收尾、开局不补。

### A3. 触发点是否影响补牌（证据）

- 效果触发永远发生在**触发它的 Action 的 RunToCompletion 内**（`ActionPipelineSystem.cs:147-152`：Apply 后先 `DispatchTriggers(Post)` 再 `ResolveTriggeredActions(follow-ups)`；Post 触发产生的 `ExecuteEffectAction` 经 reaction stack 在同一 run 内消化，`ActionPipelineSystem.cs:178-194`）。
- Fill 步骤不是 Action 的一部分：`FillEmptySlotsAction` 只在命令层被 enqueue（A1 的 6 处）。因此：
  - `AfterAction / OnKill / OnBattle` 触发的效果 → 一定早于表现层后续发出的 `ResolvePostKillFill` 命令 → 洞会被该命令补。✓
  - `OnDeal / OnEnter`（且 ActionSource=FillEmptySlots，即捕熊陷阱）触发的效果 → 晚于 fill 循环 → 本批不补。✗
  - `OnNodeStart`（NodeStartedAction Post，`PhaseActions.cs:77-97`）触发的效果 → 晚于 StartNode 里唯一的 Fill（fill 在 NodeStarted 之前，`PhaseSystem.cs:172 vs 174`）→ 不补、也不产生洞（只有注入类效果）。

---

## 线 B：节点开局（StartNode）补牌

### B1. StartNode 的流水线（`PhaseSystem.cs:164-178`）

```
ClearPendingChoices → ChangePhase(BuildEnemyPool) → SetupNodeDeckAction
→ ChangePhase(ResetNode) → ChangePhase(DealOpeningCards) → OpeningDealAction
→ FillEmptySlotsAction   ← 整个 StartNode 只有这一处 fill
→ ResetCurrentArmorAction → NodeStartedAction（Post 触发 OnNodeStart）
→ ChangePhase(InteractionLoop)
```

- 全部 enqueue 后再 `RunToCompletion()`（`PhaseSystem.cs:176`），fill 与开牌同批执行。
- `DeckSystem.SetupNode`（`DeckSystem.cs:45-53`）是另一个等价入口（SetupNodeDeck → OpeningDeal → Fill），同样只有一次 fill。

### B2. 开局发牌张数 / 进堆方式

- `OpeningDealAction`（`BoardDeckActions.cs:179-337`）：
  1. `PlacePlayerCardsDirectly`：把玩家侧池前 `PlayerOpeningCount`（=3）张直接放上盘面随机空格（`BoardDeckActions.cs:233-271`）；
  2. `SelectCards`：从敌侧池选 `EnemyOpeningCount`（=3）张进抽牌堆（`BoardDeckActions.cs:207-214, 273-303`）；
  3. `DrainStagingPool`：玩家侧池、敌侧池**剩余全部**并进抽牌堆（`BoardDeckActions.cs:217-218, 305-322`）；
  4. 洗牌。
- `FillEmptySlotsAction`（`BoardDeckActions.cs:339-414`）：按 `fillOrder`（1,2,3,6,9,8,7,4，8 格，跳过 Avatar 格）**只走一圈**，抽牌堆空即 `break`（`BoardDeckActions.cs:380-383`）；发 `SlotsFilled` / `DrawPileExhausted`。
- 装填数量（`RewardSystem.BuildNodeDeckOptions`，`RewardSystem.cs:279-328`）：
  - 玩家侧：`ItemDeckCapacity`（默认 6，`PlayerModel.cs:9`）+ 固定卡 + 房间注入 ≈ 6~8 张；
  - 敌侧：节点规则 `node_deck_rules.json:4` node 3 = seq1 6 + seq2 4 + seq3 2 = **12 张怪** + 常规机关 `RegularTrapPool.PerBattleCount=3` 张（`RewardSystem.cs:771-798`，`RegularTrapPool.cs:17`）= 15 张；
  - 合计约 21~23 张；开局 3 张上板后，抽牌堆 ≥ 18 张 → **fill 必然铺满 8 格且堆里还剩 10+ 张**。
- 结论：**「开局盘面 < 8 且抽牌堆非空」不可能由张数不足产生**；fill 只在抽牌堆为空时留洞（洞 + 空堆），而现在堆是满的。

### B3. 那洞是怎么来的：开局批次内效果移除

- 开局 fill 的 Post 触发（`FillEmptySlotsAction.GetPostTriggerPoints` = AfterAction/OnDeal/OnEnter，`BoardDeckActions.cs:341-346`）会派发 `OnDeal`，而捕熊陷阱的条件 `ActionSource(action=FillEmptySlots)` + `Adjacent(Self, EventCard)` 正好命中这个窗（触发系统无相位门禁，`TriggerSystem.cs:128-161`；效果系统也无 GamePhase 检查）。
- 填满 8 格的过程中，陷阱格与后续发牌格相邻是常态（fillOrder 1→2→3→6→9→8→7→4 中，1 邻 2/4、2 邻 3、3 邻 6、6 邻 9、9 邻 8、8 邻 7、7 邻 4）。陷阱被发到较前位置时几乎必触发：
  - 相邻发到真怪 → `DealDamage 10` → 低血怪（层 1 弱怪普遍 <10 血）→ `KillIfDeadAction → KillAction`（`CoreActions.cs:177-184, 558`）→ **洞 +1**，同时 OnKill 链（`DeckSystem` 出门机关进度/插入 `trap.leave`，`DeckSystem.cs:99-142`；`RewardSystem.ReactToKill`，`RewardSystem.cs:513-558`；sling/terror_mask 等遗物）全部照常触发；
  - 无论发到谁，陷阱**随后必自移除** → **洞 +1**（`effect_templates.json:1280` 的 `RemoveCard(Self)` 无任何条件）。
- 这些洞产生在 fill 循环**之后**、NodeStarted（OnNodeStart）**之前**的同一 run 里；StartNode 批次结束后没有任何 fill 命令：
  - 表现层 `StartBattleNodeInternalAsync`（`BattleSessionExecutor.Opening.cs:96-261`）在 `phase.StartNode(options)`（164 行）之后只做 `CaptureOpeningPresentationPlan` + `PresentOpeningAsync`（217-229 行），**没有任何补牌命令**；唯一例外是「开局即清关」走 `ResolvePostKillBoardFromCore`（249-253 行），与洞无关。
  - 于是「空槽 + 非空抽牌堆」的盘面一直持续到玩家第一次 Attack/Explore/UseItem/RevealFace（intent 的 `ResolvePostKillFill`，`AttackIntentScriptFactory.cs:229-240`）或敌方行动收尾（`EnemyActionPhaseScheduler.cs:213` → `PhaseSystem.cs:1723`）。
- `OnNodeStart` 注入（药水袋 `effect_templates.json:336`、飞刀袋 `384`，经 `SpawnCardAction.NodeStartDrawPileGrant` → `AddToDrawPile`，`EffectActions.cs:522-537`）发生在 fill 之后（`NodeStartedAction` Post，`PhaseActions.cs:77-97`），只让「抽牌堆非空」更成立，不产生洞。

### B4. 表现层开局是否忠实回放

- 是忠实回放，不存在第二套发牌视图：`CaptureOpeningPresentationPlan`（`BattleSessionExecutor.Opening.cs:337-458`）直接读 `BoardModel` 逐格取卡（350-373 行），`PresentOpeningAsync` 按 `GroundSlotTopology.ClockwiseRing` 逐格 `DealCardByUidWithFlightAsync` 就位（635-741 行）；发牌失败的格子只是视图缺失警告（726 行），由 `PresentationSyncSystem` 兜底镜像。
- 因此 Core 的空槽 = 玩家看到的空槽；「Core 有牌但视图没画」或「Core 空但没人补」两类情况，用户看到的现象都一致：开局空位不补。

---

## 遗留问题（供决策）

1. **是否给 StartNode 批次加收尾 fill**：在 `NodeStartedAction` 之后（OnNodeStart 注入落地后）再 enqueue 一次 `FillEmptySlotsAction`（或表现层 `StartBattleNodeInternalAsync` 在 `PresentOpeningAsync` 前补一个 fill 命令）。注意与 ADR-0026「清关后禁止补牌」的 gate 对齐（`PhaseSystem.cs:1805-1810`）。
2. **发牌批内陷阱是否应当开火**：`tpl.trap.bear_trap.fill` 设计上是「补牌时邻格触发」，但开局 fill 也是 FillEmptySlots——开局 10 伤打弱怪 + 自移除是否属于设计意图？需与策划确认，或给开局 fill 批加「机关静默」门禁。
3. **DeckSystem.SetupNode 双入口**（`DeckSystem.cs:45-53`）与 `PhaseSystem.StartNode` 重复同一序列，任何第三方调用（QuickTest/DevTest/未来入口）同样缺尾部 fill。
4. **bear trap 致死会带出 OnKill 链**（出门机关插入/恐怖面罩拆怪/投石袋补刀），这些后续洞在开局同样不补；修 1 时一并覆盖。
5. **实测验证建议**：复现局抓 `[DeckProbe] BoardAfterStartNode` 日志（`BattleSessionExecutor.Opening.cs:52-92`），确认「空槽 + drawPile>0 + bear_trap 参与开局装填」三者同时出现；也可用 QuickTest 定向注入 bear_trap 复现。
6. **测试覆盖缺口**：现有 `TrapBatch2RegressionTests.cs:152/235/346` 覆盖 fill 批内陷阱（战后），未见「StartNode 批内陷阱触发 → 洞未补」的回归测试；`RegularTrapBattleLoadoutContractTests.cs` 只验装填契约。
