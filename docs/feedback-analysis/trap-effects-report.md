# 策划试玩反馈三方比对报告：机关卡血量显示 + 攻甲图腾效果

> 范围：`Assets/Docs/九宫格登神`（设计案）× `CONTEXT.md` + `docs/adr/`（权威决策）× `NineGrid.Core` / `NineGrid.Presentation` / 内容 JSON（代码实施）
> 方法：只探查，未改动任何文件。行号基于本次读取。

---

## 结论先行

| 反馈条目 | 判定类型 | 一句话结论 |
|---------|---------|-----------|
| 1.「机关卡的血量不清晰，看不懂有多少血」 | **硬错误（表现层 Binder 缺 Trap 分支）** + 模板默认值误导 | 机关卡 JSON 有血量 6、卡面模板有「血量数值」槽、投影快照也有 HP——唯独 `CardFacePresentationBinder.ApplyStats` 没有 `Trap` 分支，数值从不写到卡面，血量槽永远显示模板烘焙的固定值「2」，受伤不更新，与设计案「机关卡显示攻/甲/血实时反映战斗数值」直接冲突 |
| 2.「护甲图腾不加甲，攻击图腾不加攻」 | 护甲图腾：**设计冲突（效果语义在 ADR-0028 三层护甲模型下落空）**；攻击图腾：**落地理解偏差 + 真实覆盖缺口（需要进一步分析）** | 护甲图腾把 +1 加在「有效护甲」层，而战斗中可消耗/可显示的是「当前护甲」（只在关卡开始初始化）→ 战斗中零效果零显示；攻击图腾对「图腾入场时已在板」的怪有效（含卡面刷新），但**后补牌/后生成的怪邻接图腾拿不到光环**（refresh 只在 CardMoved 时重挂，补牌发的是 CardDealt），行为不一致 |

---

## 条目 1：机关卡的血量不清晰

### 1a. 设计案原文

- `04-敌人侧卡牌信息/机关卡.md:7-17`：机关卡数据表——滚石/攻击图腾/护甲图腾/恢复图腾/倒刺/捕熊陷阱/治疗泉/烈焰/复活石**统一 血量 6、攻击 0、护甲 0**；机关卡有血量属性是设计基线。
- `08-UI/界面布局.md:19`：**怪物卡**「显示的攻/甲/血包含技能加成，实时反映实际战斗数值」。
- `08-UI/界面布局.md:21`：**机关卡**「显示的攻/甲/血包含技能加成，实时反映实际战斗数值」——设计案明确要求机关卡卡面实时显示攻/甲/血。

### 1b. ADR / CONTEXT 权威

- **ADR-0017**：机关卡是一等公民 Kind（`CardKind.Trap` / `CardPresentationKind.Trap = 7`）；**卡面五套**＝底盘 + 玩家 / 怪物 / 道具 / 遗物 / **机关**（`CardChassisPaths.TrapFacePrefab`，adr/0017 决策点 6）；内容 id `trap.*`。
- **CONTEXT 卡面消费 / 投影提交**：卡面模板只消费表现投影里需要的字段；卡面数值只经战斗锚点排期器经卡面数值处理器提交，不得在队列外直读 Core。
- **CONTEXT 卡面显示值**：卡面「甲」= 当前护甲；攻/甲/血由结算指令在表演锚点驱动。

### 1c. 代码实现路径

**内容数值（正确）**
- `StreamingAssets/ContentVisual/cards/trap_*.json`：全部 `stats.hp: 6 / attack: 0 / armor: 0`（如 `trap_attack_totem.json:13-19`、`trap_armor_totem.json:13-19`、`trap_rolling_stone.json:13-19`）——与设计案表格一致，无配置缺失。

**卡面模板（槽位存在，但默认值误导）**
- `CardChassisPaths.cs:23`：`TrapFacePrefab = "Assets/Resources/Prefabs/机关卡标准模版.prefab"`（ADR-0017 五套之一）。
- 预制体 `机关卡标准模版.prefab` 节点名（YAML `m_Name` 解码）：**血量 / 血量数值** 存在；**无** 攻击数值、护甲数值、行动计数节点。
- 「血量数值」节点（fileID 9148092475904047470）挂 `TMPro.TextMeshPro`，序列化 `m_text: 2`（模板烘焙默认文字为「2」）。
- 对照组 `怪物卡标准模板.prefab`：攻击/攻击数值、护甲/护甲数值、行动/行动计数、血量/血量数值全套。
- 槽位解析表 `CardFaceSlotNodeMap.cs:47`：`Hp → ["血量数值","Hp","Life"]`——机关模板的「血量数值」节点带 TMP，`TryFindText`（`:146-168` 节点自身/子树找 `TMP_Text`）**可以**命中该槽，即槽位链路本身通。

**投影 Commit（对 Trap 通，但到 Binder 即断）**
- `CoreCardPresentationMapper.TryRead`（`CoreCardPresentationMapper.cs:68-95`）：按 `StatId.Hp` 读 effective 值，无卡种裁剪；`Armor` 取当前护甲（`:89-90` 注释「卡面护甲 = 当前护甲」）。
- 结算指令：`DealCard`/`SpawnCard`（Settled）→ `CardFaceStatHandler.ApplySpawnFace`（`CardFaceStatHandler.cs:230-244`）把 `RemainingHp=6` 写进投影快照；`UpdateHp`（`CardFaceStatHandler.cs:64-72`）在受伤后提交剩余血量——**对 Trap 卡 uid 均会执行**（`TryResolveCard` 无 kind 过滤，`:314-326`）。

**Binder（断层点）**
- `CardFacePresentationBinder.ApplyStats`（`CardFacePresentationBinder.cs:437-459`）：
  - `case Avatar`（:441-445）：只写攻击+护甲；
  - `case Monster`（:447-453）：写攻击+护甲+血量+行动计数；
  - **`default`（:455-457）＝ 道具/遗物/机关…：一条 `SetNumeric` 都不调**。`Trap` 落 default → 机关卡卡面数值（含血量）永远不被写入。
- 结果：机关卡「血量数值」TMP 保持预制体烘焙默认「2」，不受 Spawn/受伤/死亡指令影响；受伤后血量变化无任何卡面反馈。

### 1d. 三方比对判定

**硬错误（表现层 Binder 缺 Trap 分支）。**

- 设计案要求机关卡实时显示攻/甲/血（`08-UI/界面布局.md:21`）；机关卡数据表有血量 6（`机关卡.md`）。
- 内容数值、模板血量槽位、投影 Commit 快照三处都齐备，**唯一断点在 `CardFacePresentationBinder.ApplyStats` 的 switch 无 `Trap` 分支**（`CardFacePresentationBinder.cs:455-457` 落入 default 空实现）。
- 现状说明：玩家看到的机关卡血量是模板写死的「2」，与真实 6 血及战斗中扣减完全脱节——「看不懂有多少血」是准确的用户观察。修复方向（不实施，仅指出）：在 `ApplyStats` 增加 `case Trap`（机关模板有血量槽，可复用 `SetNumeric(Hp)`；模板无攻/甲槽，按模板实际槽位写即可）。

---

## 条目 2：护甲图腾不加甲，攻击图腾不加攻

### 2a. 设计案原文

- `04-敌人侧卡牌信息/机关技能.md:15-16`：**攻击提升**＝「[场上]正交相邻格上的怪物卡与玩家卡攻击+1（离开相邻格后该加成消失）」。
- `04-敌人侧卡牌信息/机关技能.md:18-19`：**护甲提升**＝「[场上]正交相邻格上的怪物卡与玩家卡护甲+1」。
- `01-核心概念/属性.md:12-14`：三层护甲——基础护甲（静态）/ 有效护甲（基础+遗物+套装+**技能永久加成**，**用于关卡开始时初始化当前护甲**）/ 当前护甲（本关资源，受伤优先扣它）；`属性.md:14`「关卡开始时 当前护甲 = 有效护甲」。
- `04-敌人侧卡牌信息/机关卡.md:9-14`：攻击图腾/护甲图腾/恢复图腾为常规机关（白），每关开局随机三张注入（ADR-0030）。
- 设计语义要点：图腾给**相邻格上的怪物卡与玩家卡**（玩家侧）加属性；攻击提升注明「离开相邻格后该加成消失」（光环式）；护甲提升未注明「临时」→ 按 `机关技能.md:2`「属性增减未注明临时或条件限制的，默认为本关卡永久获得」。

### 2b. ADR / CONTEXT 权威

- **ADR-0030**：常规机关装填——正式卡组生成阶段随机注入三张 White Trap（滚石/攻甲恢复图腾/倒刺/捕熊陷阱六张）；QuickTest `\1–\9` 定向注入叠加。
- **ADR-0028**：三层护甲权威——基础（`StatId.Armor` base）/ 有效（+Modifier）/ 当前（`StatId.CurrentArmor`，受伤优先扣，归零扣血）；关卡/节点开始 Avatar 当前护甲重置为有效护甲；`GainArmor`/`TransferArmor` 只动当前护甲；**卡面甲 = 当前护甲，玩家信息 HUD 甲 = 有效护甲**。
- **CONTEXT**：卡面显示值（甲=当前护甲）、当前护甲（本关可消耗；关卡开始重置为有效护甲；无回合清甲）。
- **ADR-0016**：背面惰性——背面怪不推进/不开火/被动默认不生效（`ActiveWhileFaceDown` 豁免）；`EffectSystem.IsBlockedByFaceDown`（`EffectSystem.cs:670-689`）实现。
- **CONTEXT 卡面显示值 / ADR-0005**：卡面数值只经结算指令提交（`BaseStatModified` → Settled → `ModifyBaseStat`）。

### 2c. 代码实现路径

**内容与模板（装配齐全）**
- `trap_attack_totem.json:77-102`：4 条装配全挂——`trap.attack_totem.aura`（Monster 光环）+ `refresh`（Monster 刷新）+ `aura_player` + `refresh_player`；护甲图腾同构（`trap_armor_totem.json:77-102`）。
- 效果模板（`StreamingAssets/ContentVisual/tables/effect_templates.json`）：
  - `tpl.trap.attack_totem.aura`（:1347-1353）：`OnEnter` → 目标 `FilteredCards kind=Monster zone=Board exclude Self` → `AddModifier Attack+1 layer=Conditional scope=Permanent activeWhileAdjacentTo=Self replaceSameSource`——与「离开相邻后失效」一致。
  - `tpl.trap.attack_totem.refresh`（:1355-1361）：`OnEvent eventType=CardMoved` → 同上重挂。
  - `_player` 变体（:1299-1313）：目标 `Player`（玩家卡）。
  - 护甲图腾四件（:1315-1329, :1363-1377）：同构但 `stat: Armor`（基础/有效护甲层）。
- 回归测试 `TrapBatch3RegressionTests.cs:78-132`：Spawn 顺序下邻怪/邻玩家攻甲 +1、离开失效、非邻不加、邻机关不加——**Core 机制本身在「先怪后图腾」时序下通过**。

**触发与执行链（正常）**
- 效果激活：`ContentSystem.ActivateCardEffects`（`ContentSystem.cs:197-243`），`SetupNodeDeckAction.CreateConfiguredCard`（`BoardDeckActions.cs:83-88`）造卡即挂。
- 触发：发牌 `FillEmptySlotsAction`（`BoardDeckActions.cs:386-412` 发 `CardDealt`）→ post-triggers `OnDeal/OnEnter`（`:354-359`）→ `OnEnterTrigger`（`EffectAtomLibrary.cs:699-723`，匹配本卡 `CardDealt`）→ `AddModifier` → `AddStatModifierAction`（`EffectActions.cs:1086-1188`）。
- 邻接条件动态求值：`SourceAdjacentToUidCondition.IsMet`（`StatConditions.cs:222-265`，每次查询实时判 `IsAdjacentTo`）——光环随位移天然生效/失效。
- 卡面刷新（仅攻击）：`AddStatModifierAction`（`EffectActions.cs:1173-1184`）对 `Stat==Attack && Scope==Permanent` 发 `AppendPermanentAttackFaceCommit` → `BaseStatModified` → `ModifyBaseStat`（Settled，`PresentationEventMap.cs:79`）→ `CardFaceStatHandler.ApplyBaseStat`（`CardFaceStatHandler.cs:141-180`）→ 怪物/玩家卡面攻击数值更新。

**问题 A：护甲图腾效果落在「有效护甲」层，战斗中零后果（设计冲突）**
- 伤害吸收只用**当前护甲**：`DealDamageAction`（`CoreActions.cs:100` `var armor = StatArmorUtility.GetCurrentArmor(target)`；ADR-0028 公式「拟甲伤 = min(当前护甲, 原始伤害)」）。
- 当前护甲只在**关卡开始**重置为有效护甲（`ResetCurrentArmorAction`，`PhaseActions.cs:57-75`；`PhaseSystem.cs:167-183` 节点开始入队），且仅重置 Avatar。
- 图腾战斗中入场 → 护甲光环只抬「有效护甲」：当前护甲不动、卡面甲（=当前护甲）不动、伤害吸收不变。按 `属性.md:13`「有效护甲用于关卡开始时初始化当前护甲」的语义，战斗中加有效护甲**没有可玩后果**。
- 表现侧也无任何刷新：`AddStatModifierAction` 的卡面旁路只处理 Attack（`EffectActions.cs:1174`），护甲修饰不产 `BaseStatModified`；HUD 有效护甲也不会因此更新。
- 结论：护甲图腾当前是「加了，但加在没人看、战斗中也不起作用的层」——玩家观察「不加甲」**属实**。实现忠实照字面「护甲+1」落到了基础/有效层，但与该游戏三层护甲模型（ADR-0028 / `属性.md`）冲突：设计案 `机关技能.md` 的「护甲提升」按字面是「本关永久」的叠甲，应落在可玩层（当前护甲 / `GainArmor` 语义），或设计案需明确该效果只在下关开局生效（显然不可能是意图）。

**问题 B：攻击图腾对「后入场怪」光环不覆盖（覆盖缺口）**
- 光环修饰器只挂给**图腾 OnEnter 时已在板**的怪；`refresh` 模板只在 `OnEvent CardMoved`（任意卡移动）时重挂。
- 战斗中**击杀补牌**发的是 `CardDealt`（`FillEmptySlotsAction`，`BoardDeckActions.cs:405-411`），**不产生 `CardMoved`** → 新入场的邻接怪拿不到修饰器 → 邻接也不加攻，直到下一次移动/旋转/交换（旋转/交换/移位都会触发 refresh 补挂）。
- 效果类**生成怪**（如复活亡者 `Spawn` → `monster.summon.special_omni`）同理：进场不发 `CardMoved`，光环缺失。
- 回归测试只覆盖「怪先上场、图腾后进」顺序（`TrapBatch3RegressionTests.cs:87-110` Spawn nearUid 在先），未覆盖「图腾先、怪后」的反向时序。
- 图腾自身卡面无攻击槽（模板无「攻击数值」节点）、设计本就不给图腾自己加攻——若试玩者盯着图腾卡面看「加攻」，属于对目标对象（相邻怪/玩家）的**理解偏差**；若观察的是后入场怪，则是**真实缺口**。
- 现状说明：攻击图腾对开局同时在板的相邻怪（含玩家卡）生效且卡面实时刷新；对战斗中后补/后生成的相邻怪不生效——「有时加有时不加」的体感来源。

### 2d. 三方比对判定

| 子条目 | 判定 | 依据 |
|--------|------|------|
| 护甲图腾不加甲 | **设计冲突（效果语义在 ADR-0028 三层护甲模型下落空）** | 效果落在有效护甲层（`effect_templates.json:1363-1377` `stat: Armor`），战斗内伤害/卡面/可消耗资源全走当前护甲（`CoreActions.cs:100`、`属性.md:14`、ADR-0028）；当前护甲仅关开始重置（`PhaseActions.cs:57-75`）。玩家观察「不加甲」属实，需设计定夺：应改为加当前护甲（可玩）或改设计案语义 |
| 攻击图腾不加攻 | **落地理解偏差 + 真实覆盖缺口（需要进一步分析）** | 机制与设计一致（`机关技能.md:15-16` 光环式、离邻失效），初始在板怪生效且有卡面刷新（`EffectActions.cs:1173-1184` + 测试 `TrapBatch3RegressionTests.cs:78-110`）；缺口＝refresh 只挂 `CardMoved`（`effect_templates.json:1355-1361`），补牌/生成怪（`CardDealt`/`Spawn`）不入光环，直到下次位移事件 |

---

## 附注（建议后续核对的点）

1. **护甲图腾修复方向**（不实施）：把光环目标从「有效护甲」改为「当前护甲」（或按设计案意图走 `GainArmor` 语义），并补 `Armor` 层的卡面刷新事件（当前 `AddStatModifierAction` 只对 Attack 发 `BaseStatModified`，`EffectActions.cs:1174`）。
2. **攻击图腾覆盖**：可考虑把 refresh 模板触发扩展为 `CardDealt` + `Spawn`（或 OnEnter 目标改为动态求值而非触发时快照）；当前 `FilteredCards` 目标在触发时解析，后续入场卡天然缺失（`EffectAtomLibrary.cs:3613-3646` 触发时 BuildActions）。
3. **机关卡卡面**：`ApplyStats` 增 `Trap` 分支后，模板「血量数值」默认文字「2」应清空（避免无提交时误显）；怪物/机关卡模板数值槽位差异（机关只有血量槽）与设计案 `机关卡.md` 表格（攻击/护甲恒 0）一致。
