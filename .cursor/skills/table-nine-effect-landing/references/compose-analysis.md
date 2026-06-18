# Compose Analysis — Analysis Only

Use when the user asks to **分析 / 能否拼 / 怎么拼** without implementing.

**Do not** modify the repo, export Luban, or run pending gate changes.

## Steps

1. Intake — same as landing workflow step 1
2. Classify — fill taxonomy template
3. Gap analysis — compose vs missing capability
4. Output report below

## Output Template

```markdown
## 效果分析：{effect_id}

### 设计语义
- 原文：（引用设计文档或 catalog design_text）
- 容器：{HelpCard|Relic|PlayerSkill|MonsterSkill}
- 生命周期：{...}

### 三态分类
- Primary state：{Modifier|RuleModifier|Triggered}
- 理由：{一句话}

### 拼装结论
- **可拼装 / 不可拼装（需新能力）**

### 推荐 DSL（草稿）
​```json
{ ... draft effect JSON ... }
​```

### 复用原子
| 角色 | Atom | 说明 |
|:--|:--|:--|
| Trigger | ... | ... |
| Condition | ... | ... |
| Target | ... | ... |
| Action | ... | ... |

### 参考实现
- `{similar.effect.id}` — {为什么类似}

### 若不可拼装
- 缺口能力簇：{from pending-gaps.md cluster}
- 建议落点：{Modifier|RuleModifier|Trigger|Target|Action|GameAction}
- 建议改动文件：{from file-touch-map.md}
- 预估工作量：{小|中|大} — {理由}

### 建议测试
- {TestClass}.{MethodName} — {断言什么}
```

---

## Worked Example: `help.armor_breaking_hammer.pending`

### 设计语义

- 原文：将目标怪物卡护甲降低10点
- 容器：HelpCard，`[使用时]`，一次性消耗
- 生命周期：on use → remove card

### 三态分类

- Primary state：**Triggered**
- 理由：一次性使用时事件，非常驻属性或规则改写

### 拼装结论

- **不可完全拼装**（截至 batch-8）— 缺少「降低目标护甲」动作或可对 Armor 做负向 `ModifyBaseStat` 的明确语义；需要选中目标。

### 推荐 DSL（草稿，能力补齐后）

```json
{
  "id": "help.armor_breaking_hammer.use",
  "typeTag": "帮助卡",
  "containerType": "HelpCard",
  "kind": "Triggered",
  "trigger": { "atom": "OnUseHelpCard" },
  "target": { "atom": "SelectedCards", "kind": "Monster", "zone": "Board", "count": 1 },
  "action": {
    "atom": "ModifyBaseStat",
    "stat": "Armor",
    "delta": -10,
    "reason": "help.armor_breaking_hammer"
  }
}
```

> 前提：`ModifyBaseStat` 支持 `Armor` 负 delta 且运行时钳制不低于 0；`SelectedCards` 在测试中通过编程注入选择。

### 复用原子

| 角色 | Atom | 说明 |
|:--|:--|:--|
| Trigger | `OnUseHelpCard` | 道具格使用 |
| Target | `SelectedCards` | 玩家选择怪物 |
| Action | `ModifyBaseStat` | stat=Armor, delta=-10 |

### 参考实现

- `help.shield_bash_tutorial.use` — 使用帮助卡 + 伤害/护甲类动作
- `help.swap_card.use` — `SelectedCards` 目标模式

### 若不可拼装（当前）

- 缺口能力簇：**Target filters and selection** + 确认 Armor 负向修改
- 建议落点：验证 `ModifyBaseStat` on `Armor`；若无， extend action or add `ReduceArmor` wrapping `LoseArmorAction`
- 建议改动文件：`TableNineContentCatalog.cs`；可能 `EffectAtomLibrary.cs` if new action
- 预估工作量：**小** — 若 ModifyBaseStat 已支持负 armor；**中** — 若需新 action + selection test harness

### 建议测试

- `P6ContentLandingTests` — 新增方法：使用破击锤，选中 5 护甲怪物，断言护甲 0 或 -5 钳制结果
- 复用 `P5CatalogTestSupport.ActivateCatalogEffect` 做 schema 校验

---

## Worked Example: `skill.hoodlum` (composable)

### 拼装结论

- **可拼装** — 已落地为 `skill.hoodlum.slot1`

### 推荐 DSL

```json
{
  "trigger": { "atom": "OnMoveToSlot", "slot": 1, "target": "Self" },
  "target": { "atom": "Player" },
  "action": { "atom": "DealDamage", "amount": 2, "actor": "Self" }
}
```

### 参考实现

- `skill.hoodlum.slot1`, `skill.air_strike.slot*`

No repo changes required for analysis mode.
