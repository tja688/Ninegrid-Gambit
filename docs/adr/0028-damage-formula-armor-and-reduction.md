---
status: accepted
---

# 标准伤害公式：三层护甲、伤害减免、无视护甲

## 决策

以策划案为产品权威，钉死局内**标准伤害公式**与护甲相关语汇。实现以本 ADR 为准；内容挂载（如铁盾）另票，本决策不要求本 PR 落地遗物。

策划权威源：

- `Assets/Docs/九宫格登神/01-核心概念/属性.md`
- `Assets/Docs/九宫格登神/02-机制/战斗机制.md`
- `Assets/Docs/九宫格登神/07-流程/战斗关卡流程.md`

### 三层护甲

| 层 | 含义 | 存取 |
|----|------|------|
| **基础护甲** | 静态成长层（出生值、`ModifyBaseStat(Armor)` 等） | `StatId.Armor` base |
| **有效护甲** | 基础 + Modifier（遗物/图腾等） | `IStatSystem.GetEffectiveInt(..., Armor)` |
| **当前护甲** | 本关可消耗缓冲；受伤优先扣它，归零后扣血 | `StatId.CurrentArmor` base |

关卡 / 节点开始：Avatar **当前护甲重置为有效护甲**（`ResetCurrentArmor` 须在开战 `AvatarAppeared` 之前；值变化时发 `ArmorChanged`）。**无**「回合结束清甲」。局内 `GainArmor` / `TransferArmor` 只动当前护甲。

**`ModifyBaseStat(Armor)`（永久成长）**：在改基础甲的同时，按**实际基础变化量**同步增减 `CurrentArmor`（钳 ≥0），并追加 `ArmorChanged`（携带新当前甲绝对值）。这样「护甲+N」类内容立刻成为本关可消耗缓冲，且卡面只经当前甲指令更新，不会被基础甲 `ResultValue` 污染。

显示约定（与现行表现一致）：

- **卡面甲** = 当前护甲（指令来源：`ArmorChanged` / 生成类 `RemainingArmor` / `BaseStatModified(CurrentArmor)`；**不**消费 `BaseStatModified(Armor)` 的 `ResultValue`）
- **玩家信息 HUD 甲** = 有效护甲（基础甲成长由 `ModifyBaseStat(Armor)` 旁路写 HUD）
- 玩家可见文案用「护甲」，**不用**「防御」指战斗属性（「防御」仅作投放功能角色粗轴，见 [CONTEXT](../../CONTEXT.md) / [ADR-0009](0009-parameterized-effect-templates.md)）

### 标准伤害公式（结算顺序）

对一次 `DealDamage`（交战打击、敌方齐射单向打击、技能直伤等共用）：

1. 以调用方给出的 `Amount` 为基数（通常为有效攻击或技能伤）。
2. `afterRules = max(0, round(DamageMultiplier(Amount)) + DamageFlatDelta)`  
   - `DamageMultiplier` / `DamageFlatDelta`：**既有规则乘区与平板**（庇佑一次性 ×0、石庇护对他怪 −1、烈焰叠伤等），不是策划属性「伤害减免」。
3. `原始伤害 = max(0, afterRules - 伤害减免)`  
   - **伤害减免**：受击方 `RuleId.DamageReduction` 多源累加；无来源时为 0。不进卡面三围、不进 HUD（与界面布局一致）。
4. 若本次伤害标记 **无视护甲**：血量损失 = 原始伤害；不扣当前护甲；不走金甲。
5. 否则：
   - 拟甲伤 = `min(当前护甲, 原始伤害)`
   - **金甲**（`GoldArmorAbsorb`）：仅可抵消「拟甲伤」段（每 5 金抵 1）；对已溢出到血量的伤害不生效
   - 实际甲损 = 拟甲伤 − 金抵；血量损失 = `max(0, 原始伤害 − 当前护甲)`（按吸收前当前护甲计溢出，与金抵甲伤正交）
6. 写回当前护甲与 HP；实际扣血最低为 0。

### 与相邻规则的边界

- **伤害减免** ≠ `DamageFlatDelta`：后者可正可负、可挂攻击方/场上语境；前者是受击方累加的减伤属性语义，专供「攻击 − 减免」策划公式。
- **无视护甲**是**单次伤害实例**标记（DSL / `DealDamageAction` 参数），不是受击方常驻状态。
- 金币盔甲遗物正文「只抵当前护甲伤害」优先于个别「实现注」里「原始伤害先全额金抵」的含糊写法；冲突时以本 ADR + 遗物效果正文为准。

### 实现状态

- 三层护甲、关开始重置、甲吸收、金甲抵甲伤：**已实现**。
- `RuleId.DamageReduction` 与 `DealDamage` / DSL `ignoreArmor`：**已实现**（落点见后果）。
- 铁盾等依赖伤害减免的遗物内容：**另票**，本决策不包含。

## 为什么

护甲三层与关间重置早已在 Core 落地，但权威层（ADR / CONTEXT）未写清「伤害减免 / 无视护甲」与既有 `Damage*` 规则的分工，UI 又曾用「防御」指有效护甲，导致策划案、代码、投放粗轴三套词混用。先统一口径，再改公式与内容，避免遗物落地时各写一套语义。

## 考虑过的替代

- **把伤害减免做成 `StatId` 并上 HUD**：否决——策划界面布局只列攻/甲/血等，减免无来源时为 0；用 `RuleId` 累加即可。
- **用负的 `DamageFlatDelta` 冒充伤害减免、不另开 RuleId**：否决——平板规则已被石庇护、烈焰等占用，语义与「受击方属性减免」混写会让铁盾与场伤修正无法分诊。
- **金甲改为「原始伤害先全额金抵再进甲」**：否决——与遗物「对血量伤害不生效」正文冲突。

## 后果

- [ADR-0012](0012-enemy-action-phase-volley.md) 单向打击改为引用本 ADR，不再把「伤害减免」与 `DamageMultiplier`/`DamageFlatDelta` 糊成一串。
- CONTEXT 增补护甲三层 / 伤害减免 / 无视护甲；功能角色「防御」必须显式消歧。
- 公式层落点：
  - `RuleId.DamageReduction`（`CoreEnums.cs`）
  - `DealDamageAction.IgnoreArmor` + 减免减法（`CoreActions.cs`）
  - DSL `DealDamage.ignoreArmor`（`EffectAtomLibrary.cs`）；`AddRuleModifier` 已可挂新 `RuleId`
  - EditMode：`DamageFormulaRegressionTests`
- **不**在本决策票内改遗物 JSON（铁盾等另票）。
- 卡面/基础甲边界加固：`ModifyBaseStat(Armor)` 同步 `CurrentArmor` + `ArmorChanged`；`CardFaceStatHandler` 不消费基础甲 `ResultValue` 写卡面（见上文显示约定）。

## 相关

- [ADR-0012](0012-enemy-action-phase-volley.md) — 敌方齐射单向打击消费本公式
- [ADR-0005](0005-card-face-beat-commit.md) — 卡面甲经 Impact 指令提交
- [ADR-0009](0009-parameterized-effect-templates.md) — `role` 粗轴「防御」≠ 战斗护甲
- `CONTEXT.md` — 领域词条
- `Assets/Docs/九宫格登神/01-核心概念/属性.md`、`02-机制/战斗机制.md`、`07-流程/战斗关卡流程.md`
