---
name: table-nine-battlelog-analysis
description: >-
  Analyze TableNine BattleTrace JSON against Assets/Docs design, Luban/catalog
  numeric tables, and Core implementation; triage Core vs presentation vs
  numbers drift; retest with EditMode harness. Use when the user asks to
  分析战斗日志/BattleLog/BattleTrace、一反击就死、对照设计文档/数值表查战斗问题、
  或根据日志复测/改接线.
---

# TableNine 战斗日志分析

精炼流程：读日志 → **数值三角对照** → 对照 Docs 规则 + Core → 分层定性 →（有疑点）EditMode 复测 → 按用户意图汇报或改代码。

## 何时用

- 用户丢了 BattleLog / Trace JSON，或说「看最新战斗日志」
- 「一反击就死 / 伤害不对 / 该不该旋转」等战斗合理性问题
- 要区分 **Core 错了** vs **表现接线错了** vs **数值/表漂移**

## 日志从哪来

1. 用户显式给了路径 / 粘贴 → 用这份  
2. 否则取 **最新**：`Assets/Notes/BattleLog/battlelog-*.json`（按修改时间）  
3. Play 结束自动导出；DevTest Keypad4 可立即导出（`InBattleManager` 层）

Schema 要点（每条 Op）：

| 字段 | 用途 |
|------|------|
| `reason` | `PlayerAttack` / `CounterAttack` / … |
| `attacker`/`target` | 打前 snap：atk/hp/armor/**defId**（有效属性） |
| `events` | 门内 EventLog：`DamageDealt` amount/delta/remainingHp |
| `presentation` | `targetKilled` / `avatarDefeated` |
| `phaseBefore`→`phaseAfter` | 是否进 `Defeat` |

## 分析流水线（按序，勿跳）

```
1 读日志末 1～3 条 CombatHit Op，还原「谁→谁、打前有效数值、伤害、是否死」
2 数值维度（必做）：Docs 意图 ↔ Luban/Catalog 基础 ↔ Trace 有效值 + DamageDealt
   → 详见 references/numeric-tables.md
3 对照 Docs 规则（先手/反击/旋转）写出「机制期望」
4 CodeGraph / 读 ApplyCombatHit、DealDamage、Attack，写出「Core 实际」
5 定性：数值？机制 Docs↔Core？日志是否符合 Core？
6 有 Core/公式疑点 → EditMode 复测（references/test-harness.md）
7 Core 绿且数值自洽 → 再查表现接线（FieldBattle / reason / 命中帧）
8 按用户意图收束：分析只汇报；要求改才动对应层（默认不改 Core）
```

### 数值维度（摘要）

战斗离不开设定数值。每条相关 Op 至少回答：

1. **表基础**：该 `defId` 在 Luban/`cards.json`（或职业表）的 ATK/HP/Armor  
2. **有效值**：Trace snap 是否被修正抬高/压低（差要能用遗物/技能/光环解释）  
3. **公式**：`伤害公式.md` → 期望扣甲/扣血 vs `DamageDealt`  
4. **漂移**：Docs≠表、表≠有效基础、有效 ATK≠伤害 → 打标签（见 numeric-tables）

| defId 类 | Docs | 真实数 |
|----------|------|--------|
| `monster.*` | `Assets/Docs/九宫牌局/07-数据/怪物卡数据.md` | `Assets/Tools/Luban/Datas/cards.json` → StreamingAssets `tablenine_tbcard.json` |
| `help.*` | `…/帮助卡数据.md` | 卡面常 0；直伤看 `effects.json` |
| `avatar.default` | `…/05-职业与层级/职业.md` | **`ProfessionCatalog.cs`**（不在 TbCard） |
| 公式 | `…/01-机制规则/伤害公式.md`、`属性.md` | Core `DealDamageAction` |

完整路径与 rg 示例：[`references/numeric-tables.md`](references/numeric-tables.md)。

### Docs 规则优先读

| 问题类型 | 文档 |
|----------|------|
| 先手/反击/击杀不反击 | `Assets/Docs/九宫牌局/01-机制规则/战斗机制.md`、`…/先攻.md` |
| 伤害/护甲/有效属性 | `…/伤害公式.md`、`属性.md`；`RUL_战斗.md` RUL_005 |
| 旋转/补牌 | `战斗机制.md`；`RUL_战斗.md` RUL_006；契约测 R1 |
| 单位/卡意图数值 | `Assets/Docs/九宫牌局/07-数据/*` |

### Core 优先看

- 门禁：`PhaseSystem.ApplyCombatHit`（单段伤害，无内建反击）
- 整段：`PhaseSystem.Attack`（可能含先手/反击语义）
- 伤害：`DealDamageAction` → Avatar 致死 `DefeatIfAvatarDead`
- 表现分段：`FieldBattleManager` → `PendingTraceReason` → `ApplyCombatHitFromCore`
- 灌数：`ContentSystem` / `TableNineLubanCatalogFactory`；化身例外 `ProfessionCatalog`

**关键认知**：表现「玩家攻 + 未杀则反击」= **两次** `ApplyCombatHit`（`PlayerAttack`→`CounterAttack`）。勿用单次 `Attack` 心智硬套分段 Trace。

## 定性口诀

| 现象 | 先怀疑 |
|------|--------|
| Docs/表基础与 Trace 有效值或 DamageDealt 对不上 | **数值漂移**（docs≠luban / 修正未解释 / cheat） |
| 数值自洽，机制与 Docs 不符且 EditMode 复现 | **Core** |
| EditMode+表都对，Play 手感不对 | **表现接线** |
| Docs 歧义或表未同步 | **设计/落地缺口**，勿当结算 bug 乱改 |

硬约束：非明确要求不改 Core；不手改 `.unity`；不「顺手改公式」让某一局 Trace 好看。

## 用户意图分流

| 用户说 | 做 |
|--------|----|
| 分析 / 合不合理 / 汇报 | 只出简报 |
| 修 / 改 / 落地 | 按定性改层；改数走 Catalog→Luban sync（effect-landing），改后可再导出日志 |
| 复测 | 只跑/补测试 |

## 汇报模板（分析模式）

```markdown
## 战斗日志结论
- 日志：`Assets/Notes/BattleLog/...`（seed / sessionId）
- 关键 Op：#n reason=… → …（一句话）

## 数值对照
- defId：Docs意图 = … / Luban基础 = … / Trace有效 = …
- 期望伤害（公式）= …；DamageDealt amount/delta/remainingHp = …
- 漂移标签：（无 / docs≠luban / formula_mismatch / …）

## 机制期望（Docs）
- …

## Core 实际
- …

## 判定
- [ ] 数值问题 / [ ] Core / [ ] 表现接线 / [ ] 设计歧义 / [ ] 合理无问题

## 证据
- …

## 建议下一步
- …
```

## 参考

- 数值查表：[`references/numeric-tables.md`](references/numeric-tables.md)
- EditMode 复测：[`references/test-harness.md`](references/test-harness.md)
- Luban 同步流程：`.cursor/skills/table-nine-effect-landing/`
- Trace 产出：`Assets/Scripts/Flow/Diagnostics/BattleTraceRecorder.cs`
