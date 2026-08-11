# 策划试玩反馈三方比对报告：战斗逻辑核查

> 范围：`Assets/Docs/九宫格登神`（设计案）× `docs/adr/` + `CONTEXT.md`（权威决策）× `NineGrid.Core`/`NineGrid.Presentation`/内容 JSON（代码实施）
> 方法：只探查，未改动任何文件。行号基于本次读取。
>
> **2026-08-11 注**：`普通远程` 攻击模式已废止（ADR-0011）；下文若仍写「五取值 / 普通远程」为当时快照。

---

## 结论先行

| 反馈条目 | 判定类型 | 一句话结论 |
|---------|---------|-----------|
| 1.「倒计时还没归零怪物卡也恶意攻击打两次」 | **需要进一步分析（未找到硬错误路径；现象由设计行为叠加构成）** + 附带一项可读性问题 | 未发现「提前开火 / 双次开火」的代码路径；「倒计时 1 显示时本拍开火」与「同拍 交战反击 + 齐射开火」均为 ADR-0012/0013 明确定案的设计语义；体感误读的主要来源是 Counter 通道对「交战反击」与「齐射单向打击」视觉不可区分 |
| 2.「一刀砍掉血量比玩家攻击高的怪物卡和机关卡」 | **落地理解偏差（设计行为）为主** + 一处需进一步分析（条件性常驻攻的卡面滞后拍） | 伤害公式实现与 ADR-0028 逐条一致；「单次伤害 > 卡面显示攻击」全部可归因于设计上的三类「显示值 ≠ 结算权威值」：Temporary 增伤不上卡面（狂暴×2）、规则乘区/平板不进卡面（DamageMultiplier/FlatDelta）、条件性常驻攻在条件翻转拍内滞后 |

---

## 条目 1：行动倒计时未归零提前攻击 / 同拍打两次

### 1a. 设计案原文

- `02-机制/战斗机制.md:18`：**标准结算流程**＝「玩家先对目标造成伤害；若目标未死亡，则目标进行反击造成伤害。目标死亡则不进行反击」——设计案里的「攻击」只有**交战内反击**这一种怪→玩家伤害；**没有**「敌方行动阶段 / 齐射」概念。
- `02-机制/技能系统.md:71,77`：计数规则「每互动N次：以玩家卡互动次数为全局计数器。每次玩家执行九宫格互动（战斗、拾取道具卡、点击相邻空格）完成后 +1」。
- `04-敌人侧卡牌信息/怪物技能.md:47`：**提速**＝「[场上]每次本卡对玩家卡造成一次伤害，其他怪物卡的互动次数+1（倒计时-1）」——设计案原文就允许「别的怪倒计时被加速」。
- `04-敌人侧卡牌信息/怪物技能.md:17`：**远程武器**＝「[战斗时]不会对玩家卡造成本卡攻击的伤害（即不会反击）」。
- `04-敌人侧卡牌信息/怪物卡.md`：各卡组数值表（如近战1: 4血1攻2甲「普通近战」）——该表为旧草案，与生产 JSON 已分叉（见附注）。

### 1b. ADR / CONTEXT 权威

- **ADR-0011**：攻击模式五取值（普通近战=正交、斜角=对角、全向=八向、普通远程=无条件、无）；阈值 3/3/3/5；必填不进 DSL；**不可被运行时改写**（可被改的只有倒计时，ADR-0013 §6）。
- **ADR-0013**：倒计时语义＝「显示还差几次」；**减到 0 即触发并重置为 N**；**开火窗口一次性**——位置不满足即作废并把倒计时重置为阈值，不保留蓄力。
- **ADR-0012**：敌方行动阶段＝报名（全场 −1、归零者按 uid 升序入名单、名单冻结）→ 逐条（复核开火资格：存活/位置/未被禁止行动；通过→一次**单向打击**走 ADR-0028 公式、玩家不反击；不通过→取消并重置倒计时）→ 收尾（统一补牌+通关检查、不旋转）；阶段内盘面冻结；每只怪一拍一个 Core 批次；**一次互动至多旋转一次**。
- **CONTEXT**：行动倒计时（距下一次开火窗口还需经历的九宫格互动次数；归零即开火窗口）、开火窗口（一次性）、行动名单（报名时冻结）、单向打击（玩家不反击，与交战是两个概念）、先手还击（只裁决交战内出手顺序、不产生额外攻击、与攻击模式正交）、背面冻结（ADR-0016：背面怪不推进/不开火，`AttackPatternCountdown` 冻结）。
- **ADR-0005 / CONTEXT**：卡面数值只经结算指令提交（`ActionCountdownChanged` → Settled → `UpdateActionCount`），View 禁止直读 Core 计数器。

### 1c. 代码实现路径（权威真相）

**倒计时生命周期（Core）**
- 进场初始化：`CardDraft.cs:92-99`（`Create` 时 `AttackPatternCountdown = frequency`）；内容路径 `ContentSystem.cs:139-151`（`CreateDraft` 读 JSON `stats.action` + `attackPattern`）。抽查全部 60+ 张怪物 JSON：`stats.action` 与模式频率**全部一致**（近战系 3、远程 5），无「配错导致 counter=0 每拍开火」的配置问题。
- 报名与 −1：`PhaseSystem.cs:1612-1674` `RegisterEnemyActionPhaseInternal`——逐卡判 `Kind==Monster`、正面（ADR-0016 冻结）、`ParticipatesInEnemyAction`（「无」跳过）；`remaining<=0` 分支（提速打到 0 / 停在开火窗）直接入名单不再 −1；否则 −1，归零入名单；名单 `candidates.Sort()`（uid 序）；**每拍先 `mEnemyActionRoster.Clear()`，每怪至多一条**。
- 逐条开火：`PhaseSystem.cs:1676-1742` `ResolveNextEnemyActionInternal`——存活/正面复核；`IsActionBanned`（`RuleId.ActionBanned`，≠`CounterAttackBanned`，远程武器不挡齐射）＋`AttackPatternRules.MeetsPositionRequirement`（`AttackPattern.cs:84-105`）不满足→`EnqueueResetAttackPatternCountdown`（重置为阈值，`PhaseSystem.cs:1757-1776`）；通过→`DealDamageAction(monster→avatar, GetAttackDamage)` 单向打击＋重置倒计时；玩家死亡→名单终止。
- 收尾：`PhaseSystem.cs:1744-1755`（清名单＋通关检查；补位由统一稳定化边界逐批推进）。
- 整拍入口（兼容）：`PhaseSystem.cs:1581-1597` `ResolveInteractiveRotation`（计数+稳定化+旋转+稳定化+敌方行动）与 `RunEnemyActionPhaseInternal` `1599-1610`——**仅**用于：拾取整拍（`ExecutePickupItem` → `ResolveInteractiveRotation`，`PhaseSystem.cs:606-638`）、同步 `Attack(SlotId)` 命令（生产未用，仅测试/Cheat）、开局边缘（`BattleSessionExecutor.Opening.cs:252` 罕见清关补走）。

**表现层分拍（导演路径）**
- `EnemyActionPhaseScheduler.cs`：注册批（`RegisterEnemyActionPhaseCommand`）→ Present → 分支（`HasPendingEnemyAction`）→ 逐拍（`ResolveNextEnemyActionCommand`；有伤害走 Counter 通道 `CounterHit`，无伤害走 Board `EnemyActionMiss`）→ 收尾批 → `mStabilization.Append`（`BoardStabilizationScheduler` 不重跑敌方行动）。`HasParticipatingEnemy` 早退（`:57-78`）只影响收尾补位，不影响开火。
- 剧本组装：`AttackIntentScriptFactory.cs`——交战只发 `CombatHitCommand`（`ApplyCombatHit`，`PhaseSystem.cs:434-491`，**只含单向 DealDamage + 交战作用域，不含敌方行动**）；互动计数独立批（`:269-291`）；**敌方行动只由 `EnemyActionPhaseScheduler` 挂一次**（`:293-304`）。`ExploreIntentScriptFactory` / `RevealFaceIntentScriptFactory` 同构。**不存在「整拍 + 分拍」叠加导致敌方行动跑两遍的路径。**
- 倒计时上卡面：`SetAttackPatternCountdownAction`（`PhaseActions.cs:337-365`，`ActionCountdownChanged` ResultValue=剩余）→ `PresentationEventMap.cs:90`（Settled）→ `CardFaceStatHandler.ApplyActionCount`（`CardFaceStatHandler.cs:84-97`）→ Commit `ActionCount`。
- 提速（加速其他怪）：`EffectAtomLibrary.cs:3295-3328` `ModifyActionCountdown`（delta=-1，跳过背面；经 `SetAttackPatternCountdownAction` 带事件）——触发点 `OnSelfDamageDealtToPlayer`（`effect_templates.json:1208`），与设计案怪物技能.md:47 一致。

### 1d. 三方比对判定

**「倒计时还没归零就攻击」——不是逻辑提前，是语义与直觉的错位。**

按 ADR-0013/CONTEXT 的权威语义，倒计时显示的是「还差几次互动」：显示 1 → 本拍 −1 到 0 → 本拍就是开火窗口。表现层顺序是：注册批 Present 先提交 `ActionCount=0`（`EnemyActionRegister` 步），随后才播打击（`CounterHit` 步）——玩家应当能看到 0 后挨打。玩家把「显示 1」读成「还有一拍安全」，与「显示 1 的这一次互动减到 0 即开火」之间的差异是**展示语义**问题，不是计数或开火判定的硬错误。

**「恶意攻击打两次」——同拍多重伤害的三条合法路径：**

1. **交战反击 + 齐射开火**：玩家攻击了倒计时为 1 的怪 → 该拍内 交战反击（怪打玩家）＋ 报名把倒计时减到 0 进名单 → 齐射再打一次。测试 `EnemyActionVolleyIntentTests.cs:118` 明确断言 `counterProjected >= 2` 且注释「交战回击 + 单向打击均应 Present Counter」——**这是 ADR-0012 的设计意图**，且是「有时」出现的唯一解释（只在倒计时恰为 1 的那一拍发生）。
2. **提速加速**：提速怪（生产卡组含 远程3 等）在反击/齐射中伤到玩家 → 其它怪倒计时 −1 → 一只「显示 1」的怪被提前到 0，同拍或下拍开火。设计案怪物技能.md:47 原文如此，实现一致（ADR-0013 §6 倒计时可被效果加减速）。
3. **多怪同拍齐射**：名单按 uid 序冻结，同拍多怪各开一次火——齐射本体。

**未发现的硬错误路径（均已逐行排除）：**
- 名单每拍重建、每怪每拍至多一条（无同怪双报名）；
- 表现层分拍与 Core 整拍互斥（整拍仅剩拾取/开局边缘，不叠加）；
- 背面冻结（`RegisterEnemyActionPhaseInternal` 与 `ResolveNextEnemyActionInternal` 双处 `FaceUp` 判）正确；
- `AttackPattern.None`（无）不报名不开火；
- `stats.action` 全库与模式频率一致，无「counter=0 每拍开火」的配置洞；
- 开火后倒计时重置为阈值（`PhaseActions.cs` 事件带绝对值），不会出现「连打」。

**归因**：现象 1 的核心是**设计行为叠加**（反击+齐射、提速加速、多怪齐射），代码无 bug 对应——归「需要进一步分析（体感问题）」；但附带一条明确的**可读性缺口**：`CombatCounterPresentChannel` 同时承载「交战反击」与「齐射单向打击」，两者视觉无区分，玩家把「反击+齐射」读成「同一只怪恶意打两次」。建议策划确认是否接受（接受则补一条对齐表现/卡面说明；不接受则需为齐射开火加区分——本报告只指路径，不开方案）。

---

## 条目 2：单次伤害超过玩家攻击力（一刀砍掉高血怪/机关）

### 2a. 设计案原文

- `02-机制/战斗机制.md:5-18`：伤害结算＝原始伤害 = max(0, 攻击力 − 伤害减免)；无视护甲直打血；否则 护甲损失 = min(当前护甲, 原始伤害)、血量损失 = 原始伤害 − 护甲损失；「攻击或技能效果造成的数值」。反伤＝受击时同步回弹，与反击不同。
- `01-核心概念/属性.md:10-16`：攻击＝决定造成伤害的数值；护甲三层（基础/有效/当前）；伤害减免多来源累加。
- `03-玩家侧卡牌信息/道具卡.md`（狂暴类）：`help.brutality_card` 生产 JSON `description`＝「玩家下一次对怪物造成的**普通攻击伤害翻倍**」（`help_brutality_card.json:82-84`，`tpl.help.brutality_card.use`：`DamageMultiplier ×2`，Temporary / Once）。
- `04-敌人侧卡牌信息/怪物技能.md`：增伤技能如 烈焰叠伤、石庇护（对其它怪 −1）等。

### 2b. ADR / CONTEXT 权威

- **ADR-0028**：标准伤害公式（对一次 `DealDamage`）＝ `Amount`（通常为有效攻击/技能伤）→ `afterRules = max(0, round(DamageMultiplier(Amount)) + DamageFlatDelta)`（既有规则乘区/平板，**不是**策划属性「伤害减免」）→ `原始伤害 = max(0, afterRules − 伤害减免)`（受击方 `RuleId.DamageReduction` 累加，不上卡面三围）→ 无视护甲 ? 直打血 : （拟甲伤=min(当前护甲, 原始伤害)、金甲只抵甲伤段、血量损失=max(0, 原始伤害−当前护甲)）。
- **CONTEXT**：权威值（规则内核真相）vs **卡面显示值**（由结算指令在表演锚点驱动，**有意滞后于权威值**）；「卡面显示攻击值」= 有效攻击经提交后的可见值；Temporary 交战加成不上卡面。
- **ADR-0005**：卡面数值只经结算指令赋值，不按 JSON stats 覆盖战中显示值。

### 2c. 代码实现路径

**伤害结算（与 ADR-0028 逐条一致，无发现公式错误）**
- `CoreActions.cs:57-189` `DealDamageAction`：`baseDamage` → `DamageMultiplier`（乘区，含 Once 消耗 `ModifierScope.Once`，`:96-98`）→ `DamageFlatDelta`（平板）→ `DamageReduction`（受击方）→ 无视护甲分支 / 甲吸收 + 金甲（`:120-123`，仅 Avatar）+ 溢出打血。数值最低 0。
- 攻击基数：`PhaseSystem.cs:2025-2034` `GetAttackDamage`＝**有效攻击**（`IStatSystem.GetEffectiveInt(Attack)`，含全部修饰器）+ 怪物侧 `EnemyAttackDelta`——伤害按「权威有效攻」结算，不是卡面显示值。
- 交战各段单次伤害复核：`ApplyCombatHit`（`PhaseSystem.cs:434-491`）一段 `DealDamage`；先手还击分支 `PhaseSystem.cs:252-266`（`CombatEngagementOrder.cs:23-34`，仅裁决出手顺序）；反击由表现层第二段 `CombatHitCommand(monster→avatar)` 承担（`AttackIntentScriptFactory.cs:437`）——**未发现任何「一段结算内伤害重复累加」的路径**。

**卡面攻击显示（Commit vs 权威值）**
- 生成/变更提交有效攻：`CardFaceEventValues.cs:18-37`（`WithFaceAbsolutes`：攻=有效攻）、`ContentActions.cs:47-52`（`ModifyBaseStat` 攻走有效攻旁路，注释明确「否则与伤害结算错位——带遗物/光环时显示落后于真实攻击力」）、`EffectActions.cs:1173-1184`（Permanent 攻修饰器旁路提交）、`EffectSystem.cs:604-610`（kind:Modifier 路径同构）。
- **不上卡面的三类**（设计如此）：
  1. **Temporary / Once 修饰器**（狂暴卡 ×2：`effect_templates.json:48`）——注释明示「Temporary 交战加成不上卡面」（`CardFaceEventValues.cs:11-14`）；
  2. **规则乘区/平板**（`DamageMultiplier` / `DamageFlatDelta`：烈焰叠伤 `effect_templates.json:688`、偶数仇恨 `:240` 等）——按 ADR-0028 属规则层，不进卡面三围；
  3. **Conditional Permanent 攻**（狂战 `relic_berserker_axe`：血量<50% 攻击×2；血之暴力 +2）——授予时提交一次有效攻；条件翻转时伤害立即按新有效攻结算，但卡面只在**授予 / 击杀 / 移除 / 拓扑变更**时补扫提交（`AppendConditionalPermanentAttackFaceCommitsForBoard`：`CoreActions.cs:544,623`、`EffectActions.cs:1591-1608`）——**条件翻转当拍存在「拍内滞后」**，随后任一次击杀即补扫对齐（属 CONTEXT「有意滞后」的设计窗口，但翻转→击杀之间的那一刀确实可能高于卡面显示值）。
- 与攻击无关的独立直伤：遗物/技能 `DealDamage`（顺劈斧 [战斗时]相邻怪 1 伤、弹弓 [击杀时]随机怪 4 伤、火球/炸弹类道具固定伤）——伤害来源与「攻击力」完全无关，卡面攻击不反映。

**内容数据**
- 机关卡：10 张 trap 生产 JSON 全部 `hp=6, atk=0, armor=0`——任何「有效攻击 ≥6」或「狂暴 ×2（攻击≥3）」或「破甲锤（TransferArmor）后」均可**一刀击破**，与「一刀砍掉机关卡」吻合。
- 怪物：如 `monster_big_orc` 14 血 3 攻（全向近战）、`monster.big_stone` 4 血 1 攻 2 甲（斜角）等——攻击 4 显示 4 的玩家配狂暴后一刀 8，可一刀 14 血怪。
- 遗物加攻链：顺劈斧/木剑等 `wood_sword.base`（攻击+1 Persistent）→ 提交有效攻，显示会同步（显示正确但玩家可能没注意遗物来源）。

### 2d. 三方比对判定

**「一刀砍掉血量比玩家攻击高的怪物卡和机关卡」＝落地理解偏差（设计行为），辅以一处需进一步分析。**

「单次伤害 > 卡面显示攻击」的全部现实路径及其设计依据：

| 路径 | 机制 | 卡面显示 | 判定 |
|------|------|---------|------|
| 狂暴卡 ×2（Once Temporary） | `DamageMultiplier` 乘区 | 不上卡面（ADR-0005/CONTEXT：Temporary 不上卡面） | 设计行为；显示 3 打 6 |
| 规则乘区/平板（烈焰叠伤、偶数仇恨、石庇护等） | `DamageMultiplier`/`DamageFlatDelta` | 不进卡面三围（ADR-0028 明示） | 设计行为 |
| Conditional 常驻攻（狂战×2 / 血之暴力 +2） | 有效攻条件翻转 | 翻转拍内滞后，随击杀补扫 | **需进一步分析**：翻转→首次击杀之间的卡面值与结算值不一致；属 CONTEXT 设计的「拍内滞后」窗口，但玩家侧无任何提示 |
| 遗物/技能直伤（顺劈斧溅射、弹弓、道具固定伤） | 独立 `DealDamage` | 与攻击无关 | 设计行为（伤害来源 ≠ 攻击力） |
| 破甲锤（TransferArmor 10） | 扒甲后全伤打血 | 甲值同步提交 | 设计行为 |
| 公式本身 | ADR-0028 逐条核对 | —— | **未发现硬错误** |

**明确的负面结论**：`DealDamageAction` 公式、三段交战（先手还击/主段/反击）的伤害数量、金甲/无视护甲/伤害减免分支均与 ADR-0028 一致；「一刀砍掉高血怪」不是公式 bug，而是**卡面「攻击」显示口径（有效攻击/静态三围）与结算口径（有效攻击 + 规则乘区 + 独立直伤）存在设计性的差距**。

**建议策划确认的设计沟通点**：卡面攻击 3、一刀飘字 6（狂暴 ×2）时，玩家无任何「临时增伤已就绪/已生效」的可读线索（狂暴卡本身有「下次翻倍」文案，但伤害飘字与卡面数字并存时仍易被读成结算 bug）。是否需要在伤害数字或卡面上呈现临时增伤来源——属展示/文案票，非规则票。

---

## 附注：三方不一致的其它观察（非本次反馈直接相关）

1. **设计案 怪物卡.md 数值表与生产 JSON 已分叉**：如 近战1（4/1/2）对应生产 `monster.big_stone`（displayName「小蜜蜂」，4/1/2，斜角近战）；ADR-0011 后果提及的「大石头 0/1/5、火祭司 0/12/8 显式保留无」的旧数值已被替换（大石头现 1 攻 斜角近战）。策划案表格是旧草案，**内容以 StreamingAssets JSON 为准**（ADR-0008 单一内容源）。
2. **设计案 战斗机制.md 无「敌方行动阶段/齐射」概念**：该文档的「反击」只指交战内还击；齐射、报名-结算-收尾、盘面冻结为 ADR-0012 在实现中补全的权威语义。策划反馈「怪物主动攻击」在旧设计案中无对应原文，**以 ADR-0011~0013 为准**——若策划对「怪物主动出手」本身有异议，应回到 ADR 层讨论，而非按战斗机制.md 的旧文判定实现错误。
3. **提速与倒计时加速的体感**：设计案怪物技能.md:47 原文就写「其他怪物卡的互动次数+1（倒计时-1）」——加速导致「显示还有值却开火」是**设计案与实现一致**的合法效果，建议在 提速 的文案/表现上让玩家可感知（现仅有效果触发脉冲）。
4. **机关卡面板**：10 张 trap 全部 6 血 0 甲 0 攻——「一刀击破机关」的门槛极低（有效攻≥6 或任何 ×2 增伤），配合条目 2 的显示口径问题，机关卡是玩家最先体感到「伤害异常」的对象；数值是否健康属策划平衡议题。

---

## 引用索引（核心文件:行）

- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Systems/PhaseSystem.cs:1581-1799`（敌方行动整拍/报名/逐条/收尾；`2025-2034` 攻击伤害）
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/Actions/CoreActions.cs:57-202`（ADR-0028 公式）
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/Actions/PhaseActions.cs:337-365`（倒计时写卡面事件）
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/Actions/EffectActions.cs:1173-1184, 1530-1559, 1591-1608`（Permanent 攻提交/补扫）
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/Actions/ContentActions.cs:28-71, 227-319`（ModifyBaseStat 有效攻 / 遗物贡献）
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Effects/EffectAtomLibrary.cs:3295-3328`（提速）
- `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/CardDraft.cs:92-99`（倒计时初始化）
- `Assets/Scripts/NineGrid.Presentation/Flow/Presentation/EnemyActionPhaseScheduler.cs`、`AttackIntentScriptFactory.cs`（表现分拍）
- `Assets/Scripts/NineGrid.Presentation/Flow/Presentation/CardFaceStatHandler.cs:84-97`（ActionCount Commit）
- 内容：`StreamingAssets/ContentVisual/cards/{monster_*,trap_*,help_brutality_card,relic_berserker_axe}.json`、`tables/effect_templates.json:48,240,688,1056,1208`
- 测试锚点：`NineGrid.Presentation/Tests/Flow/EnemyActionVolleyIntentTests.cs:118`（反击+单向打击均 Present Counter）、`NineGrid.Core.Tests/`（`EnemyActionPhaseTests`、`ActionCountdownSemanticsTests`、`DamageFormulaRegressionTests`）
