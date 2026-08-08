# 策划试玩反馈三方比对分析：牌面朝向与发牌

> 日期：2026-08-08 ｜ 性质：只读探查报告（未改动任何文件）
> 比对范围：`CONTEXT.md` + `docs/adr/` + `docs/code-map/`（权威）↔ 代码实施（`NineGrid.Core` / `NineGrid.Presentation` / `NineGrid.Content`）↔ 策划设计案（`Assets/Docs/九宫格登神/`）

---

## 结论摘要

### 条目 1：「潜伏近战攻击完不翻面，有时候莫名在其他位置又翻面了，发牌直接是反面的也有」

**判定：多机制叠加 —— ①内容配置问题（攻击模式配错）＋ ②设计冲突（「起来」随机双向翻面）＋ ③按设计的机制行为被玩家误读（刺客领袖发牌翻面）＋ ④两处表现层潜在错位（新视图时翻面指令丢失、开局不重放翻面事件）**

- ①「攻击完不翻面」主因：翻面卡组三只近战怪（近战4/特7/特8）的 JSON 同时配了攻击模式 `attackPattern: "普通近战"`（每 3 互动攻击、**不带翻面**）和技能「潜伏近战」（每 5 互动攻击＋翻面）。玩家看到的"攻击"多数是普通近战模式攻击，自然不翻面。**Core 的潜伏近战每 5 拍翻面本身实现正确且有单测**，不是翻面管线坏了。
- ②「莫名在其他位置又翻面」：远程4/boss4 的技能「起来」每次打伤玩家就**随机翻转一张其他怪物**（`[翻面]`=toggle，会翻正也会翻背）；若随机目标是刚攻击完翻下去的潜伏近战怪，会把它**翻回正面**——玩家视角就是"攻击完没翻面"。这是设计案字面（"使一张其他怪物卡[翻面]"）与翻面卡组循环意图（应只翻起背面怪）的**设计冲突**，需策划定夺。
- ③「发牌直接是反面的」：**按设计如此**——boss4「刺客领袖」正面在场时，所有被发怪物卡以背面打出（怪物技能.md）。玩家不知晓该被动，叠加 ④ 的表现错位后观感像 bug。
- ④ 表现层潜在错位（次要但真实）：
  - a. 翻面指令若在卡视图尚未创建时被冲刷，会被消费但**不落地**（`CardFaceFlipBeatHandler` 查不到卡直接返回 true）；补牌批里"同一批新造卡＋同批被翻面"即触发。
  - b. 开局路径的 `CardFaceGenerationBootstrap` **不重放 `CardFaceChanged`**，开局被刺客领袖翻成背面的怪在表现层以正面显示（Core 背面 vs 表现正面，反向错位）。

### 条目 2：「发牌目前也是乱发的，下一张我看到是道具卡，结果直接发了怪物卡反过来的情况也有」

**判定：硬错误 —— 表现层卡组视觉顺序与 Core 抽牌堆顺序失同步（视觉"下一张" ≠ Core 实际下一张）＋ 叠加条目 1③的刺客领袖发牌即背面**

- 玩家看到的"下一张"＝战场下方卡组堆最左的卡：卡组卡视觉默认**正面显示**（`CardPresentationSnapshot.FaceUp = true`），槽 0（最左）即下一次发牌视觉上的来源。
- 实际发牌永远以 **Core uid** 为准（表现层按 uid 在卡组里找卡飞上场），所以"发出的是谁"没有错——错在**卡组堆的视觉顺序**。
- 失同步机制：Core 的 `ShuffleIntoDrawPileAction` 非顶插入会**整堆重洗**（如离开机关洗入 `DeckSystem.cs:140`、呼唤/献火/吟唱等），而表现层只把新卡插进**随机视觉槽位**、**不重排既有卡的视觉顺序** → 视觉最左 ≠ Core 抽牌堆顶 → 玩家预期落空（"看到道具卡，发出来却是怪物卡"）。
- 「怪物卡反过来」＝条目 1③：刺客领袖在场时发怪即背面，属设计行为，叠加失同步后更显"乱"。

---

## 条目 1 详细比对

### 三方证据

| 方 | 证据 | 关键内容 |
|----|------|----------|
| 设计案 | `Assets/Docs/九宫格登神/02-机制/翻面机制.md:5-7` | 所有卡默认正面打出（除非效果另有说明）；`[翻面]`=正面↔背面切换；背面双向惰性 |
| 设计案 | `Assets/Docs/九宫格登神/04-敌人侧卡牌信息/怪物卡.md:42-46` | 翻面卡组近战4/特7/特8 技能为「潜伏近战＋跳杀/休养/盗取」，**未列普通近战**；boss4=普通近战＋刺客领袖＋起来 |
| 设计案 | `…/怪物技能.md:52-53` | 潜伏近战：每互动5次、正交相邻时打伤玩家，**并[翻面]** |
| 设计案 | `…/怪物技能.md:58-59` | 起来：每次本卡对玩家造成伤害，使**一张其他**怪物卡[翻面] |
| 设计案 | `…/怪物技能.md:67-68` | 刺客领袖：正面时，**所有打出到格子上的怪物卡以正面朝下** |
| 权威 | `CONTEXT.md` 牌面朝向 | 背面默认双向惰性；`FlipCardAction` 切换朝向；`CardFaceChanged`→`UpdateFaceUp`→Settled→`CardFaceFlipBeatHandler` |
| 权威 | `docs/adr/0016-card-face-orientation.md:9-21` | 翻牌时序门控：默认通道 Begin 前 `FlushUpdateFaceUp` 并等 `FlipPlaybackCoordinator.IsIdle`；战斗通道延后到命中后 |
| 权威 | `docs/adr/0011-monster-attack-pattern-intrinsic.md`（README 摘要） | 攻击模式为怪物内生必填属性，不进效果 DSL |
| 代码 | `Assets/Arts/ContentVisual/cards/monster_smart_orc.json:11,77-90`（紫蝎/近战4） | `attackPattern: "普通近战"` ＋ 装配 `skill.ambush_melee.interact` ＋ `skill.leap_kill.flip` |
| 代码 | 同上 `monster_young_orc.json` / `monster_veteran_orc.json`（特7/特8） | 同样 `普通近战` 模式 ＋ 潜伏近战 |
| 代码 | `…/NineGrid.Core/Domain/AttackPattern.cs:31-44` | OrthogonalMelee 频率=3；Ranged 频率=5 |
| 代码 | `…/NineGrid.Core/Systems/PhaseSystem.cs:1633-1665,1713-1734` | 背面怪报名跳过、−1 冻结；结算时已背面不开火、不 Reset；普通近战模式攻击＝纯 `DealDamage`，**无翻面** |
| 代码 | `Assets/StreamingAssets/ContentVisual/tables/effect_templates.json` `tpl.skill.ambush_melee.interact` | OnInteract every 5 ＋ IsFaceUp＋Adjacent 条件 → Sequence[DealDamage(→玩家), Flip(自)] |
| 代码 | `…/NineGrid.Core.Tests/Batch2HardSkillsRegressionTests.cs:318-356` | 单测：第 5 拍造成伤害且 `host.FaceUp == false`（翻面）；背面后不再触发——**Core 技能本身正确** |
| 代码 | `…/NineGrid.Core/Domain/Actions/FaceOrientationActions.cs:35-57` | `FlipCardAction` 为 **toggle**（`FaceUp = !FaceUp`），发 `CardFaceChanged` |
| 代码 | `effect_templates.json` `tpl.skill.rise_up.damage` | `OnSelfDamageDealtToPlayerCumulative(threshold 1)` → `FilteredCards{Monster,Board,exclude Self,random}` → Flip（**随机、双向**） |
| 代码 | `effect_templates.json` `tpl.skill.assassin_leader.rule` | OnDeal ＋ IsFaceUp(自) ＋ EventFilter(CardDealt, Monster) → 翻被发怪（trap 豁免） |
| 代码 | `…/NineGrid.Core.Tests/Batch3HardSkillsRegressionTests.cs:64-166` | 刺客领袖四条边沿测试齐全（正面才翻、trap 不翻、领袖背面/移除后不发翻等）——Core 行为符合设计 |

### 实施路径（潜伏近战攻击→翻面）

1. 玩家互动 → `AdvanceInteractionCountCommand`（`AttackIntentScriptFactory.cs:269-291` 独立开批）。
2. 第 5 拍：`tpl.skill.ambush_melee.interact` 触发 → `DealDamage`＋`FlipCardAction(自)`；`CardFaceChanged` 事件入批。
3. 表现：`PresentStep`（`BatchLockstepSteps.cs:158-163`）默认通道 Begin 前 `FlushUpdateFaceUp` → `CardFaceFlipBeatHandler`（`CardFaceFlipBeatHandler.cs:38-81`）Commit 快照＋`FlipPlaybackCoordinator` 串行播翻；等 Idle 后才 Begin/ack。
4. 此路径中潜伏近战怪**已在场上**（视图存在），翻面正常落地。

### 真实情况判定

| 子反馈 | 判定 | 说明 |
|--------|------|------|
| 攻击完不翻面 | **内容配置问题**（次因：起来随机翻转可把翻下的怪翻回） | 近战怪被配成每 3 拍普通近战攻击（不带翻面），与设计案"仅潜伏近战每 5 拍攻击＋翻面"不符；攻击频率也被抬高。Core 潜伏近战翻面逻辑本身正确 |
| 莫名在其他位置翻面 | **设计冲突 / 需策划确认** | 「起来」随机翻一张其他怪＝toggle 双向；设计案字面如此，但翻面卡组循环意图应只翻起背面怪（翻面机制.md 的惰性语义暗示背面怪才是目标） |
| 发牌直接是反面的 | **按设计行为（玩家不知情）＋ 表现层两处潜在错位** | 刺客领袖被动即设计内容；表现错位 a：新视图时翻面指令被消费不落地（`CardFaceFlipBeatHandler.cs:27-31`）；错位 b：开局不重放 `CardFaceChanged`（`CardFaceGenerationBootstrap.cs:79-92`）→ 开局 Core 已背面的怪正面显示（反向错位） |

---

## 条目 2 详细比对

### 三方证据

| 方 | 证据 | 关键内容 |
|----|------|----------|
| 设计案 | `Assets/Docs/九宫格登神/02-机制/发牌机制.md:14` | 空格补牌：从战斗卡组**顶部顺序**抽一张；战斗卡组为**洗混后的固定顺序牌堆，不随机抽取** |
| 权威 | `docs/adr/0034-board-stabilization-refill-batch-ack.md` | Core `IBoardStabilizationSystem` 唯一补牌权威；`FillEmptySlotsAction` 每轮至多一轮；表现层 `BoardStabilizationScheduler` 逐轮 Resolve→Present→ack |
| 权威 | `CONTEXT.md` 占格权威 vs 几何注册 | 逻辑占格真相永在 Core；表现侧只持几何注册 |
| 代码 | `…/NineGrid.Core/Domain/Actions/BoardDeckActions.cs:339-429` | `FillEmptySlotsAction`：按 FillOrder 1,2,3,6,9,8,7,4,5 从**抽牌堆顶**取牌落格 |
| 代码 | `…/NineGrid.Core/Domain/Actions/EffectActions.cs:459-549` | `ShuffleIntoDrawPileAction`：`Top=false` 时把**整堆**洗乱（:514-517） |
| 代码 | `…/NineGrid.Core/Systems/DeckSystem.cs:140` | 离开机关洗入：`ShuffleIntoDrawPileAction(LeaveTrapDefId, Trap, 1, Top:false, "leaveTrap.insert")` —— 非顶插入即整堆重洗 |
| 代码 | `…/NineGrid.Presentation/Flow/BattleSession/BoardPresentationPlayer.cs:496,1005-1009` | 补牌按 **Core uid** 发（视觉栈里找该 uid）；洗入新卡只 `AddCardAtFromOriginAsync(RandomInsertIndex)` 插**随机视觉槽**，**不重排既有卡顺序** |
| 代码 | `…/NineGrid.Presentation/Cards/CardDeckManagerSingleton.cs:277-287,1439+` | 卡组视觉槽 0（最左）＝"下一张"；随机插入槽位解析 `ResolveDeckInsertIndex` |
| 代码 | `…/NineGrid.Presentation/Flow/CoreCardPresentationMapper.cs:445-454` | 视觉快照默认 `FaceUp = true` → **卡组堆以正面显示**，玩家可见"下一张"是什么 |
| 代码 | `…/NineGrid.Presentation/Flow/Presentation/ShuffleIntoDeckPresentationScanner.cs:56-102` | 表现层只把 `shuffleInto:` 事件当"新卡入组"，不携带 Core 重洗后的全局顺序 |

### 真实情况判定

- **硬错误**：Core 在 `ShuffleIntoDrawPileAction`（非顶插入，含离开机关洗入、呼唤、献火等）后**整堆重排**，表现层卡组堆只把新卡插到随机视觉槽、既有卡视觉顺序不随 Core 重排 → 「视觉最左（玩家读作下一张）」与「Core 抽牌堆顶（实际发出）」失同步。玩家据此预判必然落空（看到道具卡、发出怪物卡）。
- 补牌本身（`FillEmptySlotsAction`）按 Core 抽牌堆顶执行，与设计案"顶部顺序抽牌"一致；`BoardStabilizationSystem.ResolveNextSlice` 补完还恢复剩余牌堆原序（`BoardStabilizationSystem.cs:81-117`），不会自我失序。
- 发出"怪物卡反面"＝条目 1③（刺客领袖按设计发怪即背面），属设计行为但玩家无预期。
- 附带设计疑问：发牌机制.md 的"固定顺序牌堆"在实现中被多路径"洗入即重洗"改变（离开机关设计案本身就要求"洗入随机位置"，见 CONTEXT「离开机关插入」），一旦 Core 重洗，表现层卡组堆没有对账/重建机制。

---

## 判定汇总表

| # | 反馈 | 判定类型 | 根因位置 |
|---|------|----------|----------|
| 1a | 潜伏近战攻击完不翻面 | 内容配置问题（＋设计案未定攻击模式列） | 翻面卡组 3 只近战怪 `attackPattern:"普通近战"`（monster_smart_orc/young_orc/veteran_orc.json）——每 3 拍攻击无翻面；设计案该三怪只有潜伏近战（每 5 拍＋翻面） |
| 1b | 攻击完不翻面（被翻回） | 设计冲突（起来随机 toggle） | `tpl.skill.rise_up.damage`：打伤玩家→随机翻一张其他怪；可把刚翻下的潜伏近战怪翻回正面（PhaseSystem 敌方阶段同拍结算） |
| 1c | 莫名在其他位置翻面 | 设计冲突（起来随机 toggle 双向） | 同上；玩家无"起来"因果可见性（随机目标、无指向性表现） |
| 1d | 发牌直接是反面的 | 按设计行为（刺客领袖）＋表现层潜在错位 | `tpl.skill.assassin_leader.rule`（怪物技能.md:67-68 即设计）；潜在错位：新视图时翻面指令丢（CardFaceFlipBeatHandler.cs:27-31）、开局不重放 CardFaceChanged（CardFaceGenerationBootstrap.cs:79-92） |
| 2 | 发牌乱发／下一张道具卡发成怪物卡 | **硬错误**（表现层卡组视觉顺序失同步） | `ShuffleIntoDrawPileAction` 非顶插入整堆重洗（EffectActions.cs:514-517；离开机关 DeckSystem.cs:140）vs 表现层随机插槽不重排（BoardPresentationPlayer.cs:1005-1009） |

## 建议后续动作（未实施，仅列方向）

1. 确认翻面卡组近战怪的攻击模式应配什么（设计案未给该列）：若意图即潜伏近战每 5 拍攻击＋翻面，应改为 `attackPattern: "无"`（None，不开火）或新增"潜伏"模式，避免每 3 拍白打。
2. 策划确认「起来」的目标语义：随机任意怪（字面）还是仅背面怪（循环意图）；若仅背面怪，需在模板目标过滤器加 `IsFaceDown` 条件（影响 `rise_up` 装配与卡面描述）。
3. 表现层卡组堆需在 Core 重洗后重建/对账视觉顺序（或改为卡背显示，玩家不再预判"下一张"）。
4. 补牌批翻面指令在视图缺失时应保留待视图就绪后落地（或由发牌完成后的表现层二次消费），并让开局路径重放 `CardFaceChanged`。
