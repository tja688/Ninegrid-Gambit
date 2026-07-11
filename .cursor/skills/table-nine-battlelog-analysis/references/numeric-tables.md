# 数值查表（Docs ↔ Luban）

> 卡牌**错位 / 瞬移 / 闪消失**等纯表现问题走 PerfLog（模式 C），不要用本表硬套数值结论。见 [`perf-events.md`](perf-events.md)。

战斗日志里的 `atk/hp/armor` 是**结算前有效属性**，不等于表基础值。分析时必须三角对照：

```
Docs 意图数值  ↔  Luban/Catalog 落地基础值  ↔  Trace 有效值 + DamageDealt
```

## 权威优先级（读数时）

| 优先级 | 来源 | 何时用 |
|--------|------|--------|
| 1 运行时落地 | `Assets/StreamingAssets/TableNine/LubanData/tablenine_tbcard.json` 等 | Play 实际加载（`ContentCatalogBootstrap` Auto） |
| 2 可编辑源表 | `Assets/Tools/Luban/Datas/cards.json`（及 effects/skills/relics） | 查基础三维、effect 固定伤害 |
| 3 内容真源 C# | `NineGrid.Content/Catalog/TableNineContentCatalog.cs` | 与 Luban 不一致时看谁更新了 |
| 4 策划文档 | `Assets/Docs/九宫牌局/07-数据/*`、`伤害公式.md` | **意图**；与表不一致记「设计/落地漂移」 |
| — 例外 | `ProfessionCatalog.cs` | `avatar.default` / 职业初始三维 **不在** TbCard |

真源→表同步：菜单 `TableNine/Content/Export Hardcoded Catalog To Luban Datas` → `Assets/Tools/Luban/gen_table_nine.ps1`（详见 `table-nine-effect-landing`）。

## defId → 查哪

| defId | Docs（意图） | 真实表 |
|-------|--------------|--------|
| `monster.*` | `Assets/Docs/九宫牌局/07-数据/怪物卡数据.md` | `Datas/cards.json` / `tablenine_tbcard.json` 字段 `attack`,`max_hp`,`armor` |
| `help.*` | `…/07-数据/帮助卡数据.md` | 卡面三维常为 0；伤害在 `effect_ids` → `Datas/effects.json` |
| `skill.*` | `…/04-技能/*`、`元设计/06-数据/*技能*` | `Datas/skills.json` → `effect_ids` |
| `relic.*` | `…/07-数据/遗物数据.md`、`03-遗物/遗物数值.md` | `Datas/relics.json` → effects（modifier） |
| `avatar.default` / `profession.*` | `…/05-职业与层级/职业.md` | **`ProfessionCatalog.cs`**（非 Luban 卡表） |

公式意图：`Assets/Docs/九宫牌局/01-机制规则/伤害公式.md`、`属性.md`；元规则 `RUL_战斗.md` RUL_005。

## 手算核对（每条 CombatHit）

1. 表基础 ATK/HP/Armor（或职业初始）  
2. Trace snap 有效值 — 差 = 修正（遗物/技能/光环/临时）  
3. 期望原始伤害 ≈ `max(0, 有效ATK - 伤害减免)`（减免默认 0，除非 Docs/效果写明）  
4. 护甲吸收后：`DamageDealt.amount`、`delta`、`remainingHp`/`remainingArmor` 是否吻合  
5. 帮助卡/技能直伤：以 **effect JSON 的 amount** 为准，不以卡面 attack 为准  

## 常见漂移标签

| 标签 | 含义 |
|------|------|
| `docs≠luban` | 策划表与 cards.json / StreamingAssets 不一致 |
| `luban≠trace_base` | 有效值远超基础且无效果解释 → 灌表/加载/cheat |
| `formula_mismatch` | 有效 ATK 与 DamageDealt 对不上公式 |
| `test_stub` | 如 `monster.test` / 仅测试草稿，勿当生产数值 |

## 快速 rg

```bash
rg '"def_id": "monster.xxx"' Assets/Tools/Luban/Datas/cards.json
rg 'monster.xxx' Assets/StreamingAssets/TableNine/LubanData/tablenine_tbcard.json
rg 'monster.xxx' Assets/Docs/九宫牌局/07-数据/怪物卡数据.md
```
