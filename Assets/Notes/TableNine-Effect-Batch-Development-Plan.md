# TableNine 效果系统小步快跑开发规划

日期：2026-06-18  
目的：把 P5/P6/R3/R4 后续工作从“很多东西都开了头”整理成可执行、可验证、可连续委托给 AI 的批次路线。

## 0. 批次0事实重置记录（2026-06-18）

本次只做事实重置，不改玩法代码。统计来源为 `TableNineContentCatalog.cs`、`EffectAtomLibrary.cs`、`Assets/Tools/Luban/Datas/*.json`、核心设计文档和 `NineGrid.Core.Tests`。

### 0.1 当前数量

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 48 |
| hardcoded explicit pending effects | 38 |
| pending monster skill placeholder effects | 38 |
| runtime pending effects | 76 |
| runtime total effects | 124 |
| help cards | 27 |
| relics | 19 |
| skill definitions total | 69 |
| explicit skill definitions before `PendingSkill` expansion | 31 |
| monster cards | 60 |
| monster decks | 6 |
| reward pools | 4 |
| reward entries | 14 |
| rooms | 5 |
| node deck rules | 9 |

Effect atom surface:

| Atom lane | Count |
|:--|--:|
| Triggers | 15 |
| Conditions | 7 |
| Targets | 9 |
| Actions | 17 |

Luban sample rows:

| File | Rows |
|:--|--:|
| `cards.json` | 3 |
| `economy.json` | 1 |
| `effects.json` | 5 |
| `monster_decks.json` | 1 |
| `node_deck_rules.json` | 1 |
| `relics.json` | 1 |
| `reward_entries.json` | 2 |
| `reward_pools.json` | 1 |
| `rooms.json` | 3 |
| `skills.json` | 2 |

Design doc effect lines:

| Doc | Effect lines |
|:--|--:|
| `帮助卡数据.md` | 22 |
| `遗物数据.md` | 58 |
| `玩家技能.md` | 7 |
| `怪物技能.md` | 81 |

Core EditMode test methods:

| Test file | Tests |
|:--|--:|
| `P0ArchitectureGuardTests.cs` | 3 |
| `P0InfrastructureTests.cs` | 4 |
| `P1ModelTests.cs` | 2 |
| `P2StatPipelineTests.cs` | 6 |
| `P3ActionPipelineTests.cs` | 2 |
| `P4NodeFlowTests.cs` | 11 |
| `P5EffectSystemTests.cs` | 12 |
| `P6ContentLandingTests.cs` | 6 |
| `P6LubanContentTests.cs` | 1 |
| `P6R3LubanIntegrationTests.cs` | 4 |

### 0.2 Pending IDs by container

HelpCard pending, 21:

- `help.armor_breaking_hammer.pending`
- `help.bear_trap.pending`
- `help.blood_conversion.pending`
- `help.blue_chest_card.pending`
- `help.brutality_card.pending`
- `help.common_chest_card.pending`
- `help.doubling_tower.pending`
- `help.fireball.pending`
- `help.food_card.pending`
- `help.golden_chest_card.pending`
- `help.healing_spring.pending`
- `help.impact_tutorial.pending`
- `help.kidnapping.pending`
- `help.rolling_stone.pending`
- `help.rotation_wheel.pending`
- `help.shield_bash_tutorial.pending`
- `help.stat_boost_card.pending`
- `help.swap_card.pending`
- `help.teleport_card.pending`
- `help.ward_magic_card.pending`
- `help.watchtower.pending`

Relic pending, 6:

- `relic.gold_armor.pending`
- `relic.heavy_armor.pending`
- `relic.lucky_coin.pending`
- `relic.potion_bag.pending`
- `relic.throwing_knife_bag.pending`
- `relic.vitality_amulet.max_hp`

PlayerSkill pending, 3:

- `skill.easy_road.pending`
- `skill.even_hatred.pending`
- `skill.thorn_skin.pending`

MonsterSkill pending, 46:

- `skill.absorb_bone.pending`
- `skill.absorb_stone.pending`
- `skill.air_strike.pending`
- `skill.blessing.pending`
- `skill.bloodthirst.pending`
- `skill.delivery.pending`
- `skill.fall_apart.pending`
- `skill.fight_me.pending`
- `skill.find_weakness.pending`
- `skill.fire_power.pending`
- `skill.first_strike.pending`
- `skill.flame_boiling.pending`
- `skill.flame_breath.pending`
- `skill.fracture_fall_apart.pending`
- `skill.gear_delivery.pending`
- `skill.guide.pending`
- `skill.hard.pending`
- `skill.hoodlum.pending`
- `skill.hot_observation.pending`
- `skill.intense_burning.pending`
- `skill.learning_growth.pending`
- `skill.mixed_bones.pending`
- `skill.orc_tactics.pending`
- `skill.otherworld_help.pending`
- `skill.random_walk.pending`
- `skill.range_expand.pending`
- `skill.rascality.pending`
- `skill.relentless_chase.pending`
- `skill.rolling_crush.pending`
- `skill.sacrifice.pending`
- `skill.smart.pending`
- `skill.space_mastery.pending`
- `skill.stocking.pending`
- `skill.stone_growth.pending`
- `skill.stone_lover.pending`
- `skill.stone_shelter.pending`
- `skill.stray_cub.first_strike`
- `skill.strong_combo.pending`
- `skill.swallow_stone.pending`
- `skill.taunt.pending`
- `skill.thief_claims.pending`
- `skill.throw_stone.pending`
- `skill.turn_world.pending`
- `skill.unstable.pending`
- `skill.violence_maniac.pending`
- `skill.violence_nutrition.pending`

### 0.3 相比上一版笔记的变化

- 核心 burn-down 数字未移动：implemented 48、runtime pending 76、runtime total 124。
- Luban 仍是最小样例，不是生产内容源；Hardcoded catalog 仍是当前运行真源。
- 本次补充了完整 pending ID 清单，并把 runtime pending 明确拆成 `38` 个显式 pending 和 `38` 个 `PendingSkill` 展开项。
- Atom 表面比旧快照更明确：15 trigger、7 condition、9 target、17 action。下一批不要再把“缺表达力”和“已存在 atom 但未转内容”混在一起。

### 0.4 下一批建议

优先落地批次1：建立 pending count / container count 闸门，并尝试只用现有 atom 转 1-3 个准确表达的 quick win。若 quick win 语义无法完全表达，就只完成闸门和报告，不强行清零。

## 1. 当前事实基线

### 已经可靠的地基

- `NineGrid.Core` 已有 Action 流水线、TriggerSystem、StatSystem、EffectSystem、ContentSystem、Reward/Economy/Board/Deck/Phase 等核心系统。
- P0-P4 已有较完整地基测试，P5/P6 也已经出现真实 catalog DSL 测试，不再只是机制替身。
- `EffectValidator` 当前已经接入 `EffectAtomRegistry` / `EffectAtomSchemas`，能校验未知 atom、缺字段、嵌套 action graph、参数范围。
- Luban 工程、表定义、生成代码、StreamingAssets 数据、`TableNineLubanCatalogFactory` 与 bootstrap 已接入，能跑最小样例。

### 当前还没签收的部分

- `TableNineContentCatalog.cs` 仍是主要内容真源，Luban 只是最小样例，不是生产内容源。
- hardcoded catalog 预计有 48 个 implemented effect、76 个 pending effect，R4 远未清零。
- 设计文档里效果总量更大：帮助卡 22 条、遗物 58 条、玩家技能 7 条、怪物技能 81 条。hardcoded catalog 覆盖了帮助卡和玩家技能的大部分，但遗物和怪物技能仍是子集/原型。
- R5 的全内容回放网、pending 闸门、Luban 全量迁移闸门还没有完成。

## 2. 核心判断

不要按“帮助卡 -> 遗物 -> 玩家技能 -> 怪物技能”硬推进。那会把同一种能力重复拆开，越做越乱。

正确推进方式是按“能力簇”推进：

1. 先有盘点和 pending 闸门。
2. 再补通用表达力，比如动态数值、目标过滤、生命周期/规则改写、奖励选择、计数器、棋盘标记。
3. 每补一个通用能力，只转 2-5 个真实内容。
4. 每批都用 schema + catalog DSL + 行为测试证明。
5. 最后再全量迁表、pending 清零、CI 卡死。

## 3. 效果分类准则

每条效果先归入三态之一：

| 分类 | 何时使用 | 例子 |
|:--|:--|:--|
| Modifier | 改数值，且数值可随条件实时求值 | 攻击+1、处于格6攻击+2 |
| RuleModifier | 改规则或判定点 | 恢复翻倍、金币抵消伤害、只能攻击本卡 |
| Triggered | 某时机发生动作 | 每移动N次洗入卡、击杀时造成伤害 |

不要把“规则改写”伪装成属性，不要把“动态数值”做成一堆专属 action，不要靠“记得减回去”处理临时效果。

## 4. 批次规划

### 批次0：事实重置

使用场景：开始新一轮开发、状态混乱、要重新规划。

交付：

- 更新 implemented/pending/Luban/docs/test 数量。
- 列出 pending effect by container。
- 标出上次规划后已经变化的事实。
- 不做玩法代码，除非明确要求。

### 批次1：基线闸门 + 现有 atom 快速清理

目标：让混乱变成可测量，并只转当前 atom 已经能准确表达的少量 pending。

内容：

- 增加或整理 pending count / container count 测试或辅助报告。
- 明确 Hardcoded 仍是主内容源，Luban 仍是样例源。
- 转 1-3 个不需要新公共能力的 pending。
- 若没有安全 quick win，就只完成闸门和报告，不硬转。

验收：

- pending 数不会无意增加。
- 转换项有 executable DSL 和测试。
- P5/P6 相关测试通过。

### 批次2：动态数值表达 ValueExpr v1

目标：解决“造成等同于 X 的伤害/恢复/加成”这一大类共通难题。

候选：

- `help.fireball.pending`
- `help.impact_tutorial.pending`
- `help.shield_bash_tutorial.pending`
- `skill.thorn_skin.pending`
- `skill.hard.pending`

验收：

- 常量 `amount` 兼容旧 DSL。
- 新 value expression 能读取 player/target/event/owner 的关键值。
- schema 能拦住错误表达。
- 真实 catalog DSL 能执行。

### 批次3：目标过滤与移动控制 v1

目标：解决“选择/随机/相邻/非玩家/指定类型目标”和基础位移控制。

候选：

- `help.rotation_wheel.pending`
- `help.swap_card.pending`
- `help.teleport_card.pending`
- `skill.thief_claims.pending`
- `skill.unstable.pending`
- `skill.random_walk.pending`

验收：

- 随机目标走 seeded RNG。
- 没有候选目标时不崩、不产生幽灵副作用。
- 移动仍走 GameAction，并产出 EventLog。

### 批次4：生命周期、防伤、规则改写 v1

目标：解决“下一次”“只生效一次”“直到某时”“不能做某事”等规则型效果。

候选：

- `help.ward_magic_card.pending`
- `relic.gold_armor.pending`
- `skill.taunt.pending`
- `skill.stray_cub.first_strike`
- `skill.first_strike.pending`
- `skill.blessing.pending`
- `help.doubling_tower.pending`

验收：

- 激活、触发、失活、清理全部有测试。
- 不叠加/一次性/跨节点保留等生命周期边界明确。

### 批次5：奖励、选择、内容动作

目标：把宝箱、导师、奖励、获得遗物/技能/帮助卡这类内容动作接进 DSL。

候选：

- `help.common_chest_card.pending`
- `help.blue_chest_card.pending`
- `help.golden_chest_card.pending`
- `help.blood_conversion.pending`
- `skill.easy_road.pending`
- `relic.lucky_coin.pending`

验收：

- Core 只产出确定性的奖励候选/事件，不引入 UI 依赖。
- RewardSystem/ContentSystem 负责内容引用和发放。

### 批次6：计数器、来源标签、递归保护

目标：处理每 N 次、由某来源导致、不得递归触发的链式效果。

候选：

- 击杀/移除 N 次类遗物。
- 使用帮助卡 N 次类遗物。
- `skill.learning_growth.pending`
- 烈焰额外加入但不递归类效果。
- 暴力狂/吸骨等来源敏感效果。

验收：

- Counter key 归属清楚。
- 递归保护有专门测试。
- EventLog 带足够 cause/source 信息。

### 批次7：棋盘标记与场地规则

目标：处理陷阱格、福地、视为相邻、移动锁定等棋盘持续状态。

候选：

- `help.bear_trap.pending`
- 陷阱格遗物。
- 福地相关怪物技能。
- `skill.range_expand.pending`
- Boss 移动锁定类技能。

验收：

- 标记创建、触发、可见状态、节点结束清理都有测试。

### 批次8：Luban 迁移与 CI 闸门

目标：让 P6 变成真实数据驱动。

内容：

- 按已验证能力簇把 hardcoded 内容迁到 `Assets/Tools/Luban/Datas`。
- 每迁一小批就运行 Luban 生成和 content tests。
- 增加 hardcoded/Luban parity 测试。
- pending 闸门逐步从“允许存在”变成“不允许增加”，最终变成 `pending == 0`。

## 5. 推荐下一步

下一次如果要直接进入实现，建议从：

1. **落地批次1**：建立 pending 闸门和现状报告，让后续 burn-down 可见。
2. **落地批次2**：做 ValueExpr v1，因为它能解锁最多真实效果。
3. **落地批次3**：做目标过滤/移动控制，帮助卡和怪物位移类会明显减少 pending。

## 6. 给后续 AI 的技能入口

已创建 Codex skill：

`C:\Users\jinji\.codex\skills\table-nine-effect-batches`

后续可以直接说：

```text
用 $table-nine-effect-batches 落地批次1
用 $table-nine-effect-batches 落地批次2
用 $table-nine-effect-batches 复核当前 pending burn-down
```

该 skill 会要求 AI 先读规则、当前规划、current map、batch roadmap、effect taxonomy，再按小批次开发和验收。
