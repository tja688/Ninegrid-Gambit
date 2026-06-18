# TableNine 效果系统小步快跑开发规划

日期：2026-06-18  
目的：把 P5/P6/R3/R4 后续工作从“很多东西都开了头”整理成可执行、可验证、可连续委托给 AI 的批次路线。

## 0. 批次0事实重置记录（2026-06-18）

本次只做事实重置，不改玩法代码。统计来源为 `TableNineContentCatalog.cs`、`EffectAtomLibrary.cs`、`Assets/Tools/Luban/Datas/*.json`、核心设计文档和 `NineGrid.Core.Tests`。

### 0.1 当前数量

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 51 |
| hardcoded explicit pending effects | 35 |
| pending monster skill placeholder effects | 38 |
| runtime pending effects | 73 |
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
| `P6ContentLandingTests.cs` | 9 |
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

Relic pending, 3:

- `relic.gold_armor.pending`
- `relic.heavy_armor.pending`
- `relic.lucky_coin.pending`

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

- 批次1后核心 burn-down 数字已移动：implemented 51、runtime pending 73、runtime total 124。
- Luban 仍是最小样例，不是生产内容源；Hardcoded catalog 仍是当前运行真源。
- pending ID 清单已扣除批次1转换项，并把 runtime pending 明确拆成 `35` 个显式 pending 和 `38` 个 `PendingSkill` 展开项。
- Atom 表面比旧快照更明确：15 trigger、7 condition、9 target、17 action。后续批次不要再把“缺表达力”和“已存在 atom 但未转内容”混在一起。

### 0.4 下一批建议

优先落地批次2：实现 ValueExpr v1，用通用动态数值能力解锁一组真实效果；批次1已完成 pending count / container count 闸门和 3 个现有 atom quick win。

## 1. 批次1落地记录（2026-06-18）

目标：建立 hardcoded catalog 的 pending 上限闸门，并只转换现有 atom 已能准确表达的效果。

本批转换：

- `relic.vitality_amulet.max_hp`：Relic / Modifier / `Player`，`MaxHp +6`，复用现有 `Modifier` DSL。
- `relic.throwing_knife_bag.node_start`：Relic / Triggered / `OnNodeStart`，向 `PlayerCardPool` 生成 2 张 `help.throwing_knife`。
- `relic.potion_bag.node_start`：Relic / Triggered / `OnNodeStart`，向 `PlayerCardPool` 生成 2 张 `help.healing_potion`。

本批闸门：

- `P6ContentLandingTests.DefaultCatalogKeepsBatchOnePendingGateAndQuickWinsImplemented` 将 runtime pending 上限锁到 `<= 73`，并按容器锁定上限：HelpCard `<= 21`、Relic `<= 3`、PlayerSkill `<= 3`、MonsterSkill `<= 46`。
- 转换项均断言为 `Implemented`，且不再出现在 pending report。

批次1后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 51 |
| hardcoded explicit pending effects | 35 |
| pending monster skill placeholder effects | 38 |
| runtime pending effects | 73 |
| runtime total effects | 124 |

验证记录：

- Unity MCP refresh/compile：通过，Console 无编译错误。
- 新增 P6 窄测试 3 个：通过。
- `P5EffectSystemTests` + `P6ContentLandingTests`：21/21 通过。
- `NineGrid.Core.Tests` EditMode 全量：54/54 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。

下一批建议：进入批次2 `ValueExpr v1`，优先解锁 `help.fireball.pending`、`help.impact_tutorial.pending`、`help.shield_bash_tutorial.pending`、`skill.thorn_skin.pending`、`skill.hard.pending` 这类动态数值效果。

## 2. 批次2落地记录（2026-06-18）

目标：实现 `ValueExpr v1`，让动作 atom 可以从玩家、当前目标、事件和简单表达式读取动态数值，同时保持旧 `amount` 常量 DSL 兼容。

本批新增能力：

- `DealDamage`、`Heal`、`GainArmor` 支持 `value` 字段；旧 `amount` 字段继续可用。
- `value` 支持 `source + stat` 读值：`Player`、`Target`、`Owner/Self`、`EventTarget`、`EventCard`、`Actor`。
- `value` 支持 `Event` 字段：`Amount`、`Delta`、`RemainingHp`、`RemainingArmor`。
- `value` 支持简单组合：`Add`、`Subtract`、`Multiply`、`Min`、`Max`、`Negate`。
- `EffectAtomSchemas` 增加表达式 schema 校验，能拦截未知 source/stat/op/field；动作数值现在要求 `amount` 或 `value` 二选一。

本批转换：

- `help.fireball.use`：HelpCard / Triggered / `[使用时]`，随机怪物目标，伤害值读取 `Player.Attack`。
- `help.impact_tutorial.use`：HelpCard / Triggered / `[使用时]`，随机怪物目标，伤害值读取 `Player.Hp`。
- `help.shield_bash_tutorial.use`：HelpCard / Triggered / `[使用时]`，随机怪物目标，伤害值读取 `Player.Armor`。
- `help.food_card.use`：HelpCard / Triggered / `[使用时]`，治疗值为 `Player.MaxHp - Player.Hp`。

刻意延后：

- `skill.thorn_skin.pending` 仍保持 pending。它需要在 `OnBattle` 上再次造成伤害，当前还缺 source/cause 标签与递归保护；放入批次6更稳。
- `skill.hard.pending` 仍保持 pending。它需要记录“本次战斗损失护甲值”，不是纯当前 stat 读值。

批次2后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 55 |
| hardcoded explicit pending effects | 31 |
| pending monster skill placeholder effects | 38 |
| runtime pending effects | 69 |
| runtime total effects | 124 |

验证记录：

- Unity MCP refresh/compile：通过，Console 无编译错误。
- `P5EffectSystemTests` + `P6ContentLandingTests`：23/23 通过。
- `NineGrid.Core.Tests` EditMode 全量：56/56 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。

下一批建议：进入批次3 `Target Filters and Movement Control v1`，补选择/过滤/随机目标能力，再转换 `help.rotation_wheel.pending`、`help.swap_card.pending`、`help.teleport_card.pending`、`skill.thief_claims.pending`、`skill.unstable.pending`、`skill.random_walk.pending` 中的一小组。

## 3. 批次3落地记录（2026-06-18）

目标：实现目标过滤与移动控制 v1，让目标 atom 能表达“九宫格随机/过滤/相邻/排除自身”这类通用选择，并补齐逆时针旋转。

本批新增能力：

- `FilteredCards` target 支持 `kind`、`zone`、`adjacentTo`、`include`、`exclude`、`random`、`count`。
- `FilteredCards.random` 走 seeded `IRngUtility`；无候选时返回空目标，不产生副作用。
- `Rotate` action 增加 `direction`，支持 `Clockwise` / `CounterClockwise`；逆时针仍通过 `GameAction` 产出 `CardMoved` / `BoardRotated` 事件。
- `EffectAtomSchemas` 增加 `FilteredCards.count` 与 `Rotate.direction` 校验。

本批转换：

- `help.rotation_wheel.use`：HelpCard / Triggered / `[使用时]`，逆时针旋转一次。
- `skill.thief_claims.move`：MonsterSkill / Triggered / `OnSelfMove every 3`，移除正交相邻帮助卡。
- `skill.unstable.move`：MonsterSkill / Triggered / `OnSelfMove every 3`，与九宫格随机另一张怪物卡交换。
- `skill.random_walk.move`：MonsterSkill / Triggered / `OnSelfMove every 3`，与九宫格随机帮助卡交换。

刻意延后：

- `help.swap_card.pending` 仍保持 pending。原文是“选择两张非玩家卡互换位置”，当前 `UseItemAction` 还没有承载玩家选择目标。
- `help.teleport_card.pending` 仍保持 pending。原文是“选择一张非玩家卡洗回战斗卡组”，同样需要先补可验证的选择目标负载。

批次3后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 59 |
| hardcoded explicit pending effects | 27 |
| pending monster skill placeholder effects | 38 |
| runtime pending effects | 65 |
| runtime total effects | 124 |

验证记录：

- Unity MCP refresh/compile：通过，Console 无编译错误。
- `P5EffectSystemTests` + `P6ContentLandingTests`：30/30 通过。
- `NineGrid.Core.Tests` EditMode 全量：63/63 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。

下一批建议：进入批次4 `Lifecycle, Prevention, and Rule Modifiers v1`，优先处理 `help.ward_magic_card.pending`、`relic.gold_armor.pending`、`skill.taunt.pending`、`skill.first_strike.pending`、`skill.blessing.pending`。

## 4. 当前事实基线

### 已经可靠的地基

- `NineGrid.Core` 已有 Action 流水线、TriggerSystem、StatSystem、EffectSystem、ContentSystem、Reward/Economy/Board/Deck/Phase 等核心系统。
- P0-P4 已有较完整地基测试，P5/P6 也已经出现真实 catalog DSL 测试，不再只是机制替身。
- `EffectValidator` 当前已经接入 `EffectAtomRegistry` / `EffectAtomSchemas`，能校验未知 atom、缺字段、嵌套 action graph、参数范围。
- Luban 工程、表定义、生成代码、StreamingAssets 数据、`TableNineLubanCatalogFactory` 与 bootstrap 已接入，能跑最小样例。

### 当前还没签收的部分

- `TableNineContentCatalog.cs` 仍是主要内容真源，Luban 只是最小样例，不是生产内容源。
- hardcoded catalog 预计有 59 个 implemented effect、65 个 pending effect，R4 远未清零。
- 设计文档里效果总量更大：帮助卡 22 条、遗物 58 条、玩家技能 7 条、怪物技能 81 条。hardcoded catalog 覆盖了帮助卡和玩家技能的大部分，但遗物和怪物技能仍是子集/原型。
- R5 的全内容回放网、pending 闸门、Luban 全量迁移闸门还没有完成。

## 5. 核心判断

不要按“帮助卡 -> 遗物 -> 玩家技能 -> 怪物技能”硬推进。那会把同一种能力重复拆开，越做越乱。

正确推进方式是按“能力簇”推进：

1. 先有盘点和 pending 闸门。
2. 再补通用表达力，比如动态数值、目标过滤、生命周期/规则改写、奖励选择、计数器、棋盘标记。
3. 每补一个通用能力，只转 2-5 个真实内容。
4. 每批都用 schema + catalog DSL + 行为测试证明。
5. 最后再全量迁表、pending 清零、CI 卡死。

## 6. 效果分类准则

每条效果先归入三态之一：

| 分类 | 何时使用 | 例子 |
|:--|:--|:--|
| Modifier | 改数值，且数值可随条件实时求值 | 攻击+1、处于格6攻击+2 |
| RuleModifier | 改规则或判定点 | 恢复翻倍、金币抵消伤害、只能攻击本卡 |
| Triggered | 某时机发生动作 | 每移动N次洗入卡、击杀时造成伤害 |

不要把“规则改写”伪装成属性，不要把“动态数值”做成一堆专属 action，不要靠“记得减回去”处理临时效果。

## 7. 批次规划

### 批次0：事实重置

使用场景：开始新一轮开发、状态混乱、要重新规划。

交付：

- 更新 implemented/pending/Luban/docs/test 数量。
- 列出 pending effect by container。
- 标出上次规划后已经变化的事实。
- 不做玩法代码，除非明确要求。

### 批次1：基线闸门 + 现有 atom 快速清理（已落地）

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

### 批次2：动态数值表达 ValueExpr v1（已落地）

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

### 批次3：目标过滤与移动控制 v1（已落地）

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

## 8. 推荐下一步

下一次如果要直接进入实现，建议从：

1. **落地批次4**：做生命周期、防伤、规则改写 v1，处理先攻、庇佑、嘲讽等效果。
2. **落地批次5**：接奖励/选择/内容动作，处理宝箱卡、血液转换、轻车熟路、幸运硬币。
3. **落地批次6**：补 cause/source 与递归保护后，再处理 `skill.thorn_skin.pending`、火焰链式等来源敏感效果。

## 9. 给后续 AI 的技能入口

已创建 Codex skill：

`C:\Users\jinji\.codex\skills\table-nine-effect-batches`

后续可以直接说：

```text
用 $table-nine-effect-batches 落地批次1
用 $table-nine-effect-batches 落地批次2
用 $table-nine-effect-batches 复核当前 pending burn-down
```

该 skill 会要求 AI 先读规则、当前规划、current map、batch roadmap、effect taxonomy，再按小批次开发和验收。
