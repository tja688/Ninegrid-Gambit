# 09 效果 DSL 与技能装配（Effects）

> 本篇覆盖：`Effects/` 全部 12 个文件与 `Domain/Actions/EffectActions.cs`。文件基准路径：`Assets/Scripts/NineGrid.Foundation/NineGrid.Core/`。

## 职责综述

帮助卡 / 遗物 / 怪物技能 / 机关技能共用的**数据驱动效果引擎**：JSON 定义（模板实参已由 NineGrid.Content 替换进 JSON）→ 解析为 `EffectDefinition` → 校验（结构 + 原子 schema + requires 自陈）→ `Activate` 为运行时实例（按 Kind 分三型）→ 触发时经原子（Trigger/Condition/Target/Action 四类，反射发现注册）构建 GameAction 序列交管线执行。含未触发探查（ADR-0010）、倒计时投影（ADR-0035）、背面惰性（ADR-0016）、魔免过滤（ADR-0026）。

## 关键类型表

| 类型 | 文件 | 一句话职责 |
|---|---|---|
| `EffectKind` / `EffectContainerType` / `EffectAtomKind` / `EffectAtomAttribute` | `Effects/EffectEnums.cs` | 三种效果型（Triggered/Modifier/RuleModifier）、四类容器、原子四类与注册特性 |
| `EffectDefinition` / `EffectDefinitionParser` / `EffectValidator` | `Effects/EffectDefinition.cs` | 定义 DTO（trigger/conditions/requires/target/action/modifier/ruleModifier）、JSON 解析、结构校验（必填/互斥）与黄金快照 |
| `EffectDslNode` / `EffectJson` | `Effects/EffectDslNode.cs` | 无依赖手写 JSON 解析器与动态节点访问（大小写不敏感键） |
| `IEffectAtom` 四接口 / `EffectAtomRegistry` / `ICountdownProjectionTrigger` / `CountdownScope` | `Effects/EffectAtoms.cs` | 原子契约与反射注册表；倒计时投影触发契约（projectKey/counterKey/scope）与离战重置作用域策略（机关默认 Battle、遗物默认 Run） |
| `EffectAtomLibrary`（约 75 个原子） | `Effects/EffectAtomLibrary.cs` | 全部内建原子：触发 20+、条件 20+、目标 15+、动作 25+，以及 `TargetResolver` / `EffectValueExpression` 数值表达式 |
| `EffectAtomSchemas` | `Effects/EffectAtomSchemas.cs` | 逐原子参数 schema 校验（必填字段/取值范围/嵌套动作图/禁上下文开关参数/怪技 OnBattle 须带范围声明） |
| `EffectRequireTokens` / `EffectRequiresValidator` / `EffectRequiresRuntime` / `EffectContextSwitchParams` | `Effects/EffectRequires.cs` | ADR-0010 自陈：7 个 token 词表、装配期 mount 校验、运行时区域门闩（含移除帧特例与道具格使用族）、禁参数表 |
| `IEffectSystem` / `EffectSystem` / `EffectInstance` | `Effects/EffectSystem.cs` | 引擎本体：Activate/Deactivate、触发反应注册、CanTrigger 链、魔免目标过滤、背面被动压制、倒计时投影动作生成 |
| `EffectRuntimeContext` | `Effects/EffectRuntimeContext.cs` | 原子求值上下文：架构/实例/触发上下文 + Owner/Avatar/事件访问便捷层 |
| `EffectNonTriggerProbe(Result)` | `Effects/EffectNonTriggerProbe.cs` | 未触发探查：分诊 Requires / TriggerMismatch / Conditions / Other（快照恢复计数器避免副作用） |
| `FragmentRecombineDedup` | `Effects/FragmentRecombineDedup.cs` | 无头骷髅/骷髅头重组：同批相邻对只合并一次 |
| `HelpCardStatBonusUtility` | `Effects/HelpCardStatBonusUtility.cs` | 卡店"道具数值强化"运行时加成：仅道具卡自有固定数值叠加（派生数值不叠） |
| `ExecuteEffectAction` / `EmitEffectTriggeredAction` | `Domain/Actions/EffectActions.cs` | 效果触发的动作化包装（EffectTriggered 事件 + follow-up 展开）/ 无实例时的等价事件（ADR-0018 基础触发表现）；ExecuteEffectAction 是全部 Triggered 效果反应的唯一漏斗——结算窗口（交战窗/敌方行动阶段）内产出的盘面位移动作经 `IBattleScopeSystem.TryDeferBoardMotion` 挂起，其余动作照常插在结算链原位（ADR-0044） |
| `CommitEffectCountdownRemainingAction` / `ClearEffectCountdownRemainingAction` / `ResetBattleScopedCountdownsAction` | 同上 | 倒计时剩余提交 / 清除投影 / 离战把 Battle 作用域倒计时复位为阈值（ADR-0035 #157） |
| `KillIfDeadAction` / `ConditionalDealDamageIfAliveAction` / `ForceBattleAction` | 同上 | 血≤0 补击杀 / 攻击方活着才出手 / 强制交战（自开交战作用域，先手裁决同交战） |
| `MoveCardAction` / `SetBoardMarkAction` / `ShuffleIntoDrawPileAction` / `ShuffleCardIntoDrawPileAction` / `SpawnCardAction` / `ShuffleRandomContentIntoDrawPileAction` / `ExchangeWithDrawPileAction` | 同上 | 位移（入场落地按 CardDealt）/ 祝福标记 / 生成洗入（可延后补牌）/ 现有卡洗回 / 生成落位（同槽竞态先到先得；亡语占原槽）/ 随机内容洗入 / 与牌堆换牌 |
| `AddStatModifierAction` / `AddRuleModifierAction` / `RemoveRuleModifiersBySourceAction` | 同上 | 挂属性修正 / 挂规则修正（组合条件）/ 按来源清规则——三者引发的卡面数值刷新均由统一对账缝自动提交（ADR-0045，旧手工补扫与 `CommitPermanentAttackFaceAction` 已删除） |
| `ReplayHelpCardEffectsAction` / `DeactivateEffectAction` / `DeactivateOwnerEffectsAction` / `SyncAdjacentBorrowedArmorAction` / `MarkLeaveTrapBrokenAction` | 同上 | 倍增塔重放他卡用牌效果 / 卸载效果（含倒计时投影清除、遗物撤持有）/ 按 owner 反激活 / 邻接图腾借甲（基线记账，离邻回收未耗部分）/ 置清关标志（「离开」技能终点） |

## 核心流程与数据流

### 效果三型的激活（EffectSystem.Activate）

| Kind | 激活行为 |
|---|---|
| `Triggered` | 创建 Trigger/Target/Action 原子；`OnActivate` 触发点则立即求值入队动作（跳过区域 requires——造卡时还在暂存池）；否则向 TriggerSystem 注册 `(Point, Timing)` 反应 |
| `Modifier` | 立即解析 Target、对每个目标挂 `StatModifier`（记录以便 Deactivate 回收）；卡面攻刷新由统一对账缝自动提交（ADR-0045） |
| `RuleModifier` | 构建 `RuleModifier` 挂入全局注册表（AttackTargetRestriction 自动以 Owner uid 为值；target=Self/Player 生成 TargetUidCondition） |

### 触发链（CanTrigger）

```
EffectTriggerReaction.React(triggerCtx):
  1. requires 自陈通过（EffectRequiresRuntime.Passes：区域门闩/实体有无）
  2. 背面惰性：owner 背面且无 ActiveWhileFaceDown → 拒
  3. Trigger.Matches(runtime)   // 可能推进倒计时计数器（OnInteract/OnSelfMove/OnCumulative）
  4. 全部 conditions.IsMet
  5. FragmentRecombineDedup.Passes
  全过 → ExecuteEffectAction(instanceId, ctx, 预构建动作)
  未过但倒计时已推进且有 projectKey → 只发 CommitEffectCountdownRemaining（「还剩N次」逐次上屏）
构建动作（BuildActionsForMatchedInstance）：
  倒计时提交先入列 → fireCount 次（ITriggerFireCount，OnCumulative 跨阈值多发）：
    Target.Resolve → FilterMagicImmuneTargets（魔免剔除；owner 自身放行）→ Action.BuildActions
```

### 原子速查（EffectAtomLibrary 按类）

- **触发**：OnBattle（带 targetKind/sourceAction/maxActionDepth）、OnKill、OnDeal、OnEvent（任意 CoreEventType）、OnRemove/OnSelfRemoved、OnAnyCardRemoved、OnUseHelpCard/OnSelfUsed、OnOtherHelpCardUsed、OnAnyHelpCardUsed、OnActivate、OnNodeStart/End、OnRotate、OnInteract（every/projectKey/scope）、OnSelfMove（every/requireAdjacentTo/来源过滤）、OnCardRhythmFire（禁 every）、OnMoveToSlot、OnMoveToBoardMark、OnEnter、OnFlip、OnArmorBreak、OnDamageTaken、OnFatalDamage、OnCumulative（metric×7/threshold/形态子类 OnSelfArmorLostCumulative、OnSelfDamageDealtToPlayerCumulative）、OnSelfDamageDealtToPlayer。
- **条件**：AdjacentHasCard、AtSlot、Adjacent、SelfNotDealtThisBatch / NotInOpeningDeal（ADR-0034 补记豁免）、CardZone、IsFaceUp、HpBelow、StatAtLeast、HasCard、CardCounter、TargetCount、BoardMarkCount、SelectedOption、EventFilter 族（8 个形态化变体：ActorIsPlayer/TargetIsSelf/TargetNotSelf/组合/SourcePrefix/ExcludeCause）、ActionSource、OwnsRelicSet、LevelParity。
- **目标**：Self、Player、EventCard(s)、EventTarget、Actor（事件行动者——反伤类「对攻击者」；行动者缺失时目标集为空）、MovedEventCards、BoardMarkEventCard、RandomMonster、FilteredCards（多轴过滤 + include/exclude 引用 + 随机取样）、AllMonsters（可交战桶 ∧ 正面）、SelectedCards（玩家选牌；kind=Monster 走可交战桶，trueMonsterOnly 走真怪）、OrthoAdjacent、SlotCard、Column、AdjacentCard。
- **动作**：Sequence / WeightedRandom / Repeat / Conditional（组合子，可各带子 target）、DealDamage（ignoreArmor）、Heal、GainArmor、SyncAdjacentBorrowedArmor、TransferArmor、ModifyGold、ModifyBaseStat、OfferRewardChoice、GrantRewardFromPool、GrantRelic、Move、Swap、Rotate、Flip、ConcealFace、ModifyActionCountdown、ShuffleInto（deferRefill）、MoveToDrawPile、Spawn（slot=EventFromSlot 亡语占原槽）、ShuffleRandomContent、ExchangeWithDrawPile、ForceBattle、AddModifier（activeWhileAdjacentTo）、AddRuleModifier、ReplayHelpCardEffects、SetBoardMark、RemoveCard、SetCounter、RemoveRuleModifiersBySource、ModifyRelicRunContribution、MarkLeaveTrapBroken、DeactivateSelfEffect。
- **数值表达式（EffectValueExpression）**：常数 / `{op: Add|Subtract|Multiply|Divide|Min|Max|Floor|Negate, values:[…]}` / `{source: Player|Target|Owner|Self|EventTarget|EventCard|Actor|Event(field)|CardCount|BoardMarkCount, stat, effective}`。`source:Event` 支持可选 `eventType` 过滤（如 `{"source":"Event","field":"Delta","eventType":"ArmorChanged"}`）——同一动作可能先发别类 delta 事件（金甲吸收下 GoldModified 先入队），不过滤会取错值；schema 校验非法 eventType。简单 amount 数值自动叠加卡店强化（`HelpCardStatBonusUtility`），表达式派生值不叠。

### requires 词表（ADR-0010）

`HasOwnerEntity` / `NoOwnerEntity`（遗物不得声明前者，卡类不得声明后者——装配期报错）、`ActivatedByUse`、`CardZoneTriggerable`（场上可；道具格仅"使用族"触发或显式 ItemSlots 条件；坟场/移除/牌堆仅本卡移除帧 OnRemove 特例）、`CardZone:Board`、`CardZone:ItemSlots`、`ActiveWhileFaceDown`（背面惰性豁免，非门闩）。卡挂 Triggered 效果 requires 不得为空。上下文开关参数（ownerOnly/excludeSelf/actorIs/targetIs/targetNot/sourcePrefix/excludeCause）在 schema 层被禁——语义拆成独立形态原子。

### 倒计时投影（ADR-0035）

带 every/threshold 的触发实现 `ICountdownProjectionTrigger`：DSL `projectKey`（完整「装配id.键」）非空才投影；`counterKey` 缺省为 `effect.<instanceId>.<后缀>`。触发或未触发但推进 → `CommitEffectCountdownRemainingAction` 读计数器广播 `EffectCountdownChanged`（遗物 OwnerUid=0 时计数器在 Avatar 上、以 relic.* SourceDefId 路由 HUD）。卸载 → `ClearEffectCountdownRemainingAction`。离战 → `ResetBattleScopedCountdownsAction` 只复位有效作用域为 Battle 的（机关默认 Battle、遗物默认 Run、显式 scope 覆盖）。

## 对外通信面

- `IEffectSystem.ProbeWhyNotTriggered` 供诊断工具分诊"为什么没响"。
- 效果触发对表现层的事实是 `EffectTriggered` / `EffectModifierApplied` / `EffectDeactivated` / `EffectCountdownChanged/Cleared` 事件。

## 关联 ADR

- ADR-0009（参数化模板；实参替换在 NineGrid.Content 侧完成）、ADR-0010（责任自陈、单形态原子、未触发探查）、ADR-0016（背面被动压制 SyncOwnerFaceSuppression）、ADR-0026（魔免过滤在目标解析后）、ADR-0034（SelfNotDealtThisBatch / NotInOpeningDeal / EventFilterExcludeCause）、ADR-0035（projectKey 投影与作用域）、ADR-0038（OnCardRhythmFire 禁 every）、ADR-0044（ExecuteEffectAction 内位移挂起漏斗）、ADR-0045（修正器动作卡面刷新走统一对账缝）。

## 不变量与坑

- **原子靠反射发现**：`EffectAtomRegistry.DiscoverLoadedAssemblies` 扫描全部程序集的 `[EffectAtom]` 类——新原子写在任何已加载程序集都能被发现，但 Core 外新增会破坏"Core 自足"的边界，勿滥用。
- **OnActivate 跳过区域 requires**：造卡即激活时卡还在暂存池，区域门闩若在挂载时求值会静默丢弃挂载即生效技能（神圣决斗/远程武器）——这是有意的豁免。
- **`Instances` 属性每次调用都拷贝列表**——遍历中 Deactivate 安全，但高频轮询有分配成本。
- **魔免只过滤"效果管线目标"**：交战裸伤与齐射不经此路径；owner 对自身的效果放行（不拆自身门/离开/魔免）。
- 触发原子的 `Matches` 有副作用（推进计数器）；探查器靠计数器快照/恢复规避，其他调用方不要对同一上下文重复 Matches。
- `every<=1` 且配了 projectKey 的触发不会产出投影提交（Matches 提前 return，`CountdownAdvanced` 未置位）——投影键应只配给 every≥2 的节奏。
- `DeactivateEffectAction` 对遗物容器会顺带 `PlayerModel.RemoveRelic`（自毁式遗物）；一般卸载路径是 `DeactivateOwnerEffectsAction`（卡离场）与 `DiscardRelicAction`（玩家丢弃）。
