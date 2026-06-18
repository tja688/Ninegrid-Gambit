# TableNine 效果系统批次8最终落地汇报

日期：2026-06-18  
范围：批次8 Luban 迁移、CI 闸门、阶段性收束。旧批次计划案已归档到 `Assets/Notes/归档/TableNine-Effect-Batch-Development-Plan-2026-06-18.md`。

## 1. 本批完成内容

- 新增 `NineGrid.Content.Editor` 编辑器程序集与 `TableNineLubanDataExporter`，可从当前 hardcoded 默认目录一键导出 Luban 源表。
- 将 `Assets/Tools/Luban/Datas` 从最小样例扩展为当前 hardcoded 默认目录的完整运行快照。
- 运行 Luban 生成，刷新 `Assets/StreamingAssets/TableNine/LubanData`。
- 将 P6 Luban 测试从“最小样例可读”升级为“当前生产目录可读、可校验、可创建 draft”。
- 新增 hardcoded/Luban parity 测试，锁定 effects、cards、skills、relics、monster decks、reward pools、rooms、node rules、economy 的关键字段一致。
- 更新 R3 bootstrap 测试语义：Luban 不再是样例源，而是批次8后的 production parity 数据源；Auto 在 StreamingAssets 存在时可加载完整目录。

## 2. 当前内容事实

Luban 源表与生成后运行数据已对齐 hardcoded 默认目录：

| 项目 | 数量 |
|:--|--:|
| effects total | 133 |
| implemented effects | 100 |
| pending effects | 32 |
| cards | 87 |
| skills | 68 |
| relics | 19 |
| reward pools | 8 |
| reward entries | 30 |
| monster decks | 6 |
| node deck rules | 9 |
| rooms | 5 |

本次没有伪装清零 pending。32 个 pending 被原样迁入 Luban，继续作为后续内容实现的真实缺口。

## 2.1 批次9补充（2026-06-18）：怪物技能可拼装落地

对照现有触发器/目标/动作原子，将 31 个怪物技能 pending 中**无需新原子**的 7 个技能先落地为可执行 DSL，pending 从 39 降至 32（怪物技能 31→24）。

### 本批已落地（7 技能 / 12 效果）

| 技能 | 拼装方式 |
|:--|:--|
| `skill.turn_world` | `OnEnter` + `Rotate` |
| `skill.hoodlum` | `OnMoveToSlot(1)` + `DealDamage` |
| `skill.fall_apart` | `OnRemove` + `Sequence(ShuffleInto×2)` |
| `skill.air_strike` | 四角格各一条 `OnMoveToSlot` + `DealDamage` |
| `skill.space_mastery` | `OnBattle` + `Rotate` |
| `skill.gear_delivery` | `OnSelfMove(every:2)` + `FilteredCards` + `WeightedRandom` |
| `skill.stone_growth` | 左列三格 `OnMoveToSlot` + `FilteredCards` + `GainArmor` |

### 仍 pending 的 24 个怪物技能（需要新原子/能力簇）

| 缺口能力 | 代表技能 |
|:--|:--|
| 护甲转移 | `skill.absorb_stone`、`skill.swallow_stone` |
| 战斗期动态数值（损失护甲/造成伤害计数） | `skill.hard`、`skill.bloodthirst`、`skill.throw_stone` |
| 相邻临时增益（离开后消失） | `skill.orc_tactics`、`skill.rascality` |
| 强制战斗 / 战斗劫持 | `skill.relentless_chase`、`skill.fight_me` |
| 跨区交换（场面↔卡组） | `skill.delivery` |
| 按 defId / 精英过滤批量移除 | `skill.sacrifice`、`skill.flame_breath` |
| 按卡牌数量计数修饰 | `skill.fire_power` |
| 全局护甲损失监听 | `skill.stone_lover` |
| 他人受伤规则减伤 | `skill.stone_shelter` |
| 相邻数量条件 + 多卡移除组合 | `skill.strong_combo` |
| 指定格+种类条件 | `skill.rolling_crush` |
| 随机牌组/等级洗入 | `skill.fracture_fall_apart`、`skill.mixed_bones`、`skill.otherworld_help` |
| 移动来源区分 | `skill.hot_observation` |
| 累计相邻玩家计数 | `skill.stocking` |
| 福地计数 + 一次性高伤 | `skill.find_weakness` |
| 自身攻击获得递归抑制（需 `EventFilter.targetIs`） | `skill.smart` |

## 3. 还差哪些

### HelpCard pending, 6

- `help.armor_breaking_hammer.pending`：选中怪物护甲降低 10，需要 Armor/BaseArmor 修改语义和选择目标验证。
- `help.brutality_card.pending`：玩家当前总攻击翻倍，战斗一次后复原，需要临时攻击倍率和战斗后清理。
- `help.healing_spring.pending`：多区域恢复效果，目标/区域语义仍未拆清。
- `help.kidnapping.pending`：移除非精英非层主怪物并按其护甲获得护甲，需要非精英/非 Boss 过滤与移除前护甲快照组合。
- `help.rolling_stone.pending`：移动到格3时移除格6普通怪物和本卡，需要位置触发、普通怪物过滤、自移除闭环。
- `help.watchtower.pending`：场上/道具牌格随机伤害，需要按 zone 分支和随机伤害目标策略。

### Relic pending, 1

- `relic.heavy_armor.pending`：关卡开始按基础护甲获得护甲，需要读取玩家基础护甲并在节点开始结算。

### PlayerSkill pending, 1

- `skill.even_hatred.pending`：对偶数等级怪物造成双倍伤害，需要等级奇偶条件和伤害倍率规则。

### MonsterSkill pending, 24

- `skill.absorb_stone.pending`
- `skill.bloodthirst.pending`
- `skill.delivery.pending`
- `skill.fight_me.pending`
- `skill.find_weakness.pending`
- `skill.fire_power.pending`
- `skill.flame_breath.pending`
- `skill.fracture_fall_apart.pending`
- `skill.hard.pending`
- `skill.hot_observation.pending`
- `skill.mixed_bones.pending`
- `skill.orc_tactics.pending`
- `skill.otherworld_help.pending`
- `skill.rascality.pending`
- `skill.relentless_chase.pending`
- `skill.rolling_crush.pending`
- `skill.sacrifice.pending`
- `skill.smart.pending`
- `skill.stocking.pending`
- `skill.stone_lover.pending`
- `skill.stone_shelter.pending`
- `skill.strong_combo.pending`
- `skill.swallow_stone.pending`
- `skill.throw_stone.pending`

这些主要分布在护甲转移、移动锁/位移规则、击杀/牺牲链、怪物族群联动、Boss 空间规则、石头/骷髅/火焰长尾技能。它们已经进入 Luban parity 数据，但仍没有 executable DSL。

### 更大的设计缺口

- 当前 hardcoded/Luban 目录是可运行原型子集，不等于设计文档全量。
- 设计文档中的遗物和怪物技能仍明显多于运行目录，后续需要另起“设计文档全量入库/差异盘点”任务，而不是在批次8里混入。
- `TableNineContentCatalog` 仍保留为 hardcoded 生成源；批次8完成的是 Luban parity 和 CI 闸门，不是删除 hardcoded catalog。
- Presentation/UI 仍未接管奖励选择、选目标、数据编辑工作流；Core 目前只保证事件和 DSL 行为可验证。

## 4. 验证记录

- `Assets/Tools/Luban/gen_table_nine.ps1`：通过。
- Unity MCP refresh/compile：通过，Console 无编译 error。
- P6 Luban 窄测试：2/2 通过。
- `NineGrid.Core.Tests` EditMode 全量：98/98 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。

Console 仅保留项目既有 Easy Save obsolete warning，与本批代码无关。

## 5. 后续建议

如果继续实现内容，建议不再开“大批次路线图”，而是按 1-3 个效果的小闭环处理：

1. 选目标帮助卡小闭环：`help.armor_breaking_hammer.pending`、`help.kidnapping.pending`。
2. 临时倍率/清理规则小闭环：`help.brutality_card.pending`、`skill.even_hatred.pending`。
3. 护甲读取/转移小闭环：`relic.heavy_armor.pending`、`skill.swallow_stone.pending`、`skill.absorb_stone.pending`。
4. 设计文档差异专项：把 `Assets/Docs` 的遗物、玩家技能、怪物技能与 Luban 当前目录做全量对照，决定哪些进入原型、哪些保持设计稿。
