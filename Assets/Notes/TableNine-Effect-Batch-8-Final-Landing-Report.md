# TableNine 效果系统批次8最终落地汇报

日期：2026-06-19
范围：接管前序子代理未提交成果，核验并收束 `Batch 8` 报告中仍 pending 的 24 个怪物技能。

## 1. 收尾结论

本轮确认前序子代理已大体完成能力面扩展，但存在测试失败、Luban 未同步、pending gate 未收紧等尾巴。主控收尾后，报告中列出的 24 个 MonsterSkill pending 已全部替换为 executable DSL，并同步到 Luban 源表与运行数据。

当前内容事实：

| 项目 | 数量 |
|:--|--:|
| effects total | 135 |
| implemented effects | 127 |
| pending effects | 8 |
| MonsterSkill pending | 0 |
| HelpCard pending | 6 |
| Relic pending | 1 |
| PlayerSkill pending | 1 |
| cards | 87 |
| skills | 68 |
| relics | 19 |
| monster decks | 6 |
| node deck rules | 9 |
| reward pools | 8 |
| rooms | 5 |

## 2. 本轮落地的 24 个怪物技能

本轮按 `Modifier` / `RuleModifier` / `Triggered` 三态完成分类和落地。大部分为 `Triggered`，`skill.fire_power` 为条件 `Modifier`，`skill.stone_shelter` 为 `RuleModifier`。

| 能力簇 | 已落地技能 |
|:--|:--|
| 护甲转移 | `skill.absorb_stone`、`skill.swallow_stone` |
| 战斗期动态数值 | `skill.hard`、`skill.bloodthirst`、`skill.throw_stone` |
| 相邻临时增益 | `skill.orc_tactics`、`skill.rascality` |
| 强制战斗 / 战斗劫持 | `skill.relentless_chase`、`skill.fight_me` |
| 跨区交换 | `skill.delivery` |
| defId / 精英过滤批量移除 | `skill.sacrifice`、`skill.flame_breath` |
| 按卡牌数量计数修饰 | `skill.fire_power` |
| 全局护甲损失监听 | `skill.stone_lover` |
| 他人受伤规则减伤 | `skill.stone_shelter` |
| 相邻数量组合 | `skill.strong_combo` |
| 指定格+种类条件 | `skill.rolling_crush` |
| 随机牌组/等级洗入 | `skill.fracture_fall_apart`、`skill.mixed_bones`、`skill.otherworld_help` |
| 移动来源区分 | `skill.hot_observation` |
| 累计相邻玩家计数 | `skill.stocking` |
| 福地计数 + 一次性高伤 | `skill.find_weakness` |
| 自身攻击获得递归抑制 | `skill.smart` |

对应新增/替换为 26 条 implemented effect id：`skill.hard.slot1/4/7`、`skill.swallow_stone.move`、`skill.bloodthirst.damage`、`skill.smart.gain`、`skill.stone_lover.armor_lost`、`skill.throw_stone.move`、`skill.stone_shelter.rule`、`skill.absorb_stone.move`、`skill.rascality.armor_lost`、`skill.fire_power.aura`、`skill.rolling_crush.slot3`、`skill.strong_combo.move`、`skill.stocking.move`、`skill.orc_tactics.move`、`skill.find_weakness.move`、`skill.delivery.move`、`skill.hot_observation.observe`、`skill.relentless_chase.move`、`skill.fight_me.battle`、`skill.sacrifice.move`、`skill.fracture_fall_apart.remove`、`skill.flame_breath.move`、`skill.otherworld_help.move`、`skill.mixed_bones.move`。

## 3. 能力面变化

- 扩展了事件过滤、累计触发、移动来源过滤、目标过滤、随机/数量目标、槽位目标、卡牌数量值表达式、事件字段值表达式等 DSL 能力。
- 新增或补齐了护甲转移、强制战斗、跨区交换、随机内容洗入、按目标生成/洗入、条件动作、条件 Modifier 的运行时支撑。
- 补齐 `DamageFlatDelta` 类规则修饰的战斗查询路径，用于 `skill.stone_shelter` 这类他人受伤减伤。
- 收紧内容 gate：`P6ContentLandingTests` 现在要求 total pending <= 8、MonsterSkill pending <= 0，防止怪物技能 pending 回流。
- 已重新导出 `Assets/Tools/Luban/Datas` 并运行 Luban 生成，刷新 `Assets/StreamingAssets/TableNine/LubanData`。

## 4. 剩余 pending

怪物技能已无 pending。剩余 8 个 pending 不属于本轮 24 个怪物技能范围：

### HelpCard pending, 6

- `help.armor_breaking_hammer.pending`：选中怪物护甲降低 10，需要护甲修改语义和选择目标验证。
- `help.brutality_card.pending`：玩家当前总攻击翻倍，战斗一次后复原，需要临时攻击倍率和战斗后清理。
- `help.healing_spring.pending`：多区域恢复效果，目标/区域语义仍需拆清。
- `help.kidnapping.pending`：移除非精英非层主怪物并按其护甲获得护甲，需要移除前护甲快照组合。
- `help.rolling_stone.pending`：移动到格3时移除格6普通怪物和本卡，需要帮助卡场上移动闭环。
- `help.watchtower.pending`：场上/道具牌格随机伤害，需要按 zone 分支和随机伤害目标策略。

### Relic pending, 1

- `relic.heavy_armor.pending`：关卡开始按基础护甲获得护甲，需要读取玩家基础护甲并在节点开始结算。

### PlayerSkill pending, 1

- `skill.even_hatred.pending`：对偶数等级怪物造成双倍伤害，需要等级奇偶条件和伤害倍率规则。

## 5. 验证记录

- Unity MCP refresh/compile：通过。
- Unity Console：未发现编译 error；Console 中仅有测试结果保存日志。
- `Assets/Tools/Luban/gen_table_nine.ps1`：通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。
- Unity EditMode 窄测试：`P5EffectSystemTests`、`P6ContentLandingTests`、`P6LubanContentTests`，74/74 通过。
- Unity EditMode 全量：`NineGrid.Core.Tests`，107/107 通过。

## 6. 已知风险

- 强制战斗、随机内容洗入、跨区交换目前已在 Core 可验证，但仍是原型期语义；Presentation 的操作提示、目标选择、可视化反馈不在本轮范围。
- `help.*`、`relic.heavy_armor`、`skill.even_hatred` 仍需要后续按小闭环继续落地，不建议再混成大批次。
- 当前 Luban 与 hardcoded catalog 已 parity，但 `TableNineContentCatalog` 仍是内容源头；删除 hardcoded catalog 不是本轮目标。
