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

下一批建议：批次4 已完成首个小闭环；继续批次4 后续可处理 `skill.taunt.pending`、`relic.gold_armor.pending`、`help.doubling_tower.pending`，或进入批次5 奖励/选择动作。

## 4. 批次4落地记录（2026-06-18）

目标：实现生命周期、防伤、规则改写 v1 的最小可验闭环，先补一次性防伤和先攻规则，不展开到嘲讽/金币抵伤/帮助卡二次触发。

本批新增能力：

- 新增 `RuleId.DamageMultiplier`：伤害动作在扣护甲/血量前先询问规则修正；`ModifierScope.Once` 的防伤规则会在一次实际伤害结算后清理。
- 新增 `RuleId.FirstStrike`：战斗入口按先攻规则决定玩家/怪物伤害顺序；怪物先攻且先造成致命伤害时，玩家不再反击。
- 新增 `ruleModifier.target`：RuleModifier 可约束到 `Self` 或 `Player`，避免怪物技能变成全局规则。
- 新增 `AddRuleModifier` action atom：Triggered 效果可以给目标注册一次性/临时规则修正。
- 战斗反击开始接入 `EnemyAttackDelta`，让已有龙鳞甲类规则进入真实伤害入口。

本批转换：

- `help.ward_magic_card.use`：HelpCard / Triggered / `[使用时]`，给玩家注册一次性 `DamageMultiplier Override 0`。
- `skill.stray_cub.first_strike`：MonsterSkill / RuleModifier / `Self`，处于格6时获得 `FirstStrike`。
- `skill.first_strike.rule`：MonsterSkill / RuleModifier / `Self`，持有先攻。
- `skill.blessing.rule`：MonsterSkill / RuleModifier / `Self`，下一次受到伤害变为0。

刻意延后：

- `skill.taunt.pending` 仍保持 pending。它需要交互合法性规则入口和“只能攻击本卡”的拒绝原因测试。
- `relic.gold_armor.pending` 仍保持 pending。当前批次只完成乘法/覆盖型防伤，金币抵伤需要 PlayerModel 金币扣减与伤害公式联动。
- `help.doubling_tower.pending` 仍保持 pending。它需要帮助卡触发二次执行与一次性移除语义，适合单独小批。

批次4后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 63 |
| hardcoded explicit pending effects | 25 |
| pending monster skill placeholder effects | 36 |
| runtime pending effects | 61 |
| runtime total effects | 124 |

验证记录：

- Unity MCP refresh/compile：通过，Console 无编译错误；Unity Test Framework 写入 TestResults.xml 产生 2 条保存结果提示。
- 批次4窄测试 6 个：通过。
- `P5EffectSystemTests` + `P6ContentLandingTests`：34/34 通过。
- `NineGrid.Core.Tests` EditMode 全量：68/68 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。

下一批建议：继续批次4 后续小批，优先做 `skill.taunt.pending` 的交互合法性规则；如果想换能力簇，则进入批次5 处理宝箱/奖励/选择动作。

## 5. 批次5落地记录（2026-06-18）

目标：实现奖励、选择、内容动作的最小可验闭环，让效果 DSL 能产出确定性的奖励候选事件，并转换宝箱/轻车熟路/幸运硬币。

本批新增能力：

- 新增 `OfferRewardChoice` action atom：Triggered 效果可以通过 `poolId` 请求奖励候选，不引入 UI 依赖。
- `OfferRewardChoiceAction` 执行时通过 `RewardSystem.RollPool` 抽取候选，并在 `RewardOffered` 事件 `Message` 中记录 `poolId|defId:kind:count` 列表。
- 新增 `CardCounter` condition：可检查 `EventCard` 或指定目标卡的 counter，例如 `elite` / `boss` 标记。
- `EffectAtomSchemas` 增加 `OfferRewardChoice.poolId`、`CardCounter.key`、`CardCounter.min` 校验。
- 新增奖励池 `help.white.choice`、`relic.blue_chest`、`relic.golden_chest`，保留 `relic.common_chest` 作为普通宝箱池。

本批转换：

- `help.common_chest_card.use`：HelpCard / Triggered / `[使用时]`，提供 `relic.common_chest` 三选一。
- `help.blue_chest_card.use`：HelpCard / Triggered / `[使用时]`，提供 `relic.blue_chest` 三选一。
- `help.golden_chest_card.use`：HelpCard / Triggered / `[使用时]`，提供 `relic.golden_chest` 三选一。
- `skill.easy_road.node_end`：PlayerSkill / Triggered / `OnNodeEnd`，提供 `help.white.choice` 白色帮助卡三选一。
- `relic.lucky_coin.elite_kill`：Relic / Triggered / `OnKill + EventCard.elite`，洗入 1 张 `help.gold_card`。
- `relic.lucky_coin.boss_kill`：Relic / Triggered / `OnKill + EventCard.boss`，洗入 1 张 `help.gold_card`。

刻意延后：

- `help.blood_conversion.pending` 仍保持 pending。它需要扣除 MaxHp、四路随机奖励、随机遗物发放的组合语义；当前批次只签收“奖励候选事件”和击杀奖励内容动作。

批次5后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 69 |
| hardcoded explicit pending effects | 20 |
| pending monster skill placeholder effects | 36 |
| runtime pending effects | 56 |
| runtime total effects | 125 |

验证记录：

- Unity MCP refresh/compile：通过，Console 无编译错误；Unity Test Framework 写入 TestResults.xml 产生 1 条保存结果提示。
- `P5EffectSystemTests` + `P6ContentLandingTests`：36/36 通过。
- `NineGrid.Core.Tests` EditMode 全量：70/70 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。

下一批建议：继续批次5 后续小批，补 `help.blood_conversion.pending` 所需的随机奖励/授予内容动作；或切到批次6，处理 `skill.thorn_skin.pending`、来源标签与递归保护。

### 5.1 批次5后续补全记录（2026-06-18）

目标：检查批次5首轮遗留的 `help.blood_conversion.pending` 是否已经具备收口条件；能收的直接补齐，不能收的按能力缺口重新排期。

本批新增能力：

- 新增 `ModifyBaseStat` action atom：Triggered 效果可以修改目标卡基础属性，支持 `MaxHp` 负数扣减。
- 新增 `GrantRelic`、`GrantPlayerSkillContent` action atom：把已有内容授予 GameAction 暴露给效果 DSL。
- 新增 `GrantRewardFromPool` action atom / `GrantRewardFromPoolAction`：从奖励池按权重抽取并立即发放可支持的内容；当前用于“随机遗物”。
- `ModifyBaseStatAction` 修复旧边界：降低 `MaxHp` 时会把当前 `Hp` 夹到新上限，避免出现当前血量高于上限。
- `EffectAtomSchemas` 增加 `ModifyBaseStat.stat/delta`、`GrantRewardFromPool.poolId`、`GrantRelic.relicDefId`、`GrantPlayerSkillContent.skillDefId` 校验。
- 新增奖励池 `relic.blood_conversion`，作为血液转换随机遗物分支。

本批转换：

- `help.blood_conversion.use`：HelpCard / Triggered / `[使用时]`，先扣除玩家 `MaxHp 5`，再在攻击+1、护甲+1、金币+50、随机遗物四个分支中等权随机一个。

遗留问题能力判断：

- `help.blood_conversion.pending`：已具备修复能力，本批已补全并移除 pending。
- `help.stat_boost_card.pending`：已有 `ModifyBaseStat` 地基，但还缺玩家选择选项负载；建议放入批次5C `ChoicePayload v1` 收口。
- `help.swap_card.pending` / `help.teleport_card.pending`：目标过滤已具备，但还缺玩家选择目标负载；建议同批次5C 或批次3后续 `SelectedTargets v1` 收口。
- `help.doubling_tower.pending`：不是奖励/选择动作问题，需要帮助卡二次触发、一次性移除和递归保护；建议批次4后续或批次6收口。

批次5后续补全后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 70 |
| hardcoded explicit pending effects | 19 |
| pending monster skill placeholder effects | 36 |
| runtime pending effects | 55 |
| runtime total effects | 125 |

验证记录：

- Unity MCP refresh/compile：通过，Console 无编译错误；Unity Test Framework 写入 TestResults.xml 产生 1 条保存结果提示和 1 条 PerformanceTesting 清理 warning。
- `NineGrid.Core.Tests` EditMode 全量：72/72 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。

下一批建议：如果继续批次5，应先做 `ChoicePayload v1 / SelectedTargets v1`，一口气收 `help.stat_boost_card.pending`、`help.swap_card.pending`、`help.teleport_card.pending`；如果切换能力簇，则进入批次6 处理来源标签、计数器和递归保护。

## 6. 批次6首个小闭环记录（2026-06-18）

目标：补齐 OnBattle 的最小来源过滤和递归保护能力，先转换 `skill.thorn_skin.pending`，不把学习成长、烈焰链式、暴力狂/吸骨等更大来源归因问题混进同一批。

本批新增能力：

- `OnBattle` trigger 支持 `sourceAction`，可限定触发来源 action，例如只响应 `DealDamage`。
- `OnBattle` trigger 支持 `targetKind`，可限定事件目标卡类型，例如只响应目标为 `Monster` 的伤害。
- `OnBattle` trigger 支持 `maxActionDepth`，本批用 `0` 限制只响应顶层战斗伤害，阻止由效果自身 follow-up 造成的伤害再次递归触发。
- 新增 `EventTarget` target atom，让 DSL 能直接把当前事件目标作为动作目标。
- `EffectAtomSchemas` 增加 `OnBattle.targetKind` 和 `OnBattle.maxActionDepth` 校验。

本批转换：

- `skill.thorn_skin.battle`：PlayerSkill / Triggered / `OnBattle + DealDamage + targetKind Monster + maxActionDepth 0`，对当前受伤怪物造成等同其攻击的伤害。

刻意延后：

- `skill.learning_growth.pending` 仍保持 pending。它需要属性增加事件带来源/目标归因，而当前 `AddStatModifier` 与 `ModifyBaseStat` 事件还不足以表达“其他怪物获得攻击”。
- `skill.flame_boiling.pending` 仍保持 pending。它需要帮助卡伤害来源增益，适合在来源标签扩展到具体 defId/cause 后处理。
- `skill.violence_maniac.pending`、`skill.violence_nutrition.pending`、`skill.absorb_bone.pending` 仍保持 pending。它们需要移除来源、被移除卡基础属性快照和来源敏感连锁。

批次6首个小闭环后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 71 |
| hardcoded explicit pending effects | 18 |
| pending monster skill placeholder effects | 36 |
| runtime pending effects | 54 |
| runtime total effects | 125 |

验证记录：

- Unity MCP refresh/compile：通过，Console 无错误。
- `NineGrid.Core.Tests` EditMode 全量：74/74 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。
- `Assets/Notes/CI/run-core-tests.ps1`：Core tests passed；本机已有 Unity Editor 打开项目，batchmode 末尾报告重复实例提示，最终以 Unity MCP 全量测试为准。

下一批建议：继续批次6B，先补更明确的 `sourceDefId/cause` 事件标签与属性变化来源，再处理 `skill.learning_growth.pending` 或烈焰链式“不递归”效果；如果想快速收内容，则回到 `ChoicePayload v1 / SelectedTargets v1` 处理 `help.stat_boost_card.pending`、`help.swap_card.pending`、`help.teleport_card.pending`。

### 6.1 批次6B落地记录（2026-06-18）

目标：补齐结构化来源标签、事件过滤和非递归链式触发，把批次6从 `OnBattle` 特例推进成通用来源敏感能力。

本批新增能力：

- `CoreGameEvent` 新增 `SourceDefId` / `Cause` 字段；伤害、治疗、护甲、金币、移除、用卡、基础属性修改、洗入/生成卡牌、效果触发事件会写入来源信息。
- 新增 `OnEvent` trigger：用于监听普通 action 产出的事件，例如 `BaseStatModified` / `EffectModifierApplied`。
- 新增 `OnDeal` trigger：用于监听洗入/发牌事件 `CardDealt`。
- 新增 `EventFilter` condition：支持 `eventType/eventTypes`、`stat`、`minDelta`、`targetKind`、`targetNot`、`sourceDefId`、`excludeSourceDefId`、`cause`、`excludeCause`。
- `EffectAtomSchemas` 增加 `OnEvent.eventType`、`EventFilter.eventTypes/targetKind/stat` 校验，避免来源过滤 DSL 静默写错。
- effect action atom 会把 `EffectOwner.SourceDefId` 透传到后续 GameAction；洗入/生成卡牌事件以被创建卡牌 defId 作为 `SourceDefId`，以触发它的技能/遗物/帮助卡作为 `Cause`。

本批转换：

- `skill.learning_growth.gain`：MonsterSkill / Triggered / `OnEvent + EventFilter`，当其他怪物获得攻击时，本卡 `Attack +1`；排除 `skill.learning_growth` 自身来源，防止学习成长互相递归触发。
- `skill.intense_burning.flame_deal`：MonsterSkill / Triggered / `OnDeal + EventFilter`，每有一张 `help.flame` 加入战斗卡组，额外加入一张 `help.flame`；排除 `Cause == skill.intense_burning`，证明额外加入不会递归。
- `skill.violence_maniac.move`：MonsterSkill / Triggered / `OnSelfMove every 2`，移除正交相邻怪物卡和帮助卡，并用 `SourceDefId == skill.violence_maniac` 标记移除来源。
- `skill.violence_nutrition.monster_remove`：MonsterSkill / Triggered / `OnRemove + EventFilter`，每依靠暴力狂移除一张怪物卡，本卡 `Attack +3`。
- `skill.violence_nutrition.help_remove`：MonsterSkill / Triggered / `OnRemove + EventFilter`，每依靠暴力狂移除一张帮助卡，本卡 `Armor +5`。

刻意延后：

- `skill.flame_boiling.pending` 仍保持 pending。它是“烈焰帮助卡伤害+1”的来源敏感伤害加值规则，需要后续补 `DamageFlatDelta` / source-aware damage rule，而不是伪装成当前卡牌攻击。
- `skill.absorb_bone.pending` 仍保持 pending。它需要移除事件携带被移除卡的基础攻击/护甲快照，建议作为批次6C或批次7前置小批处理。

批次6B后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 76 |
| hardcoded explicit pending effects | 17 |
| pending monster skill placeholder effects | 33 |
| runtime pending effects | 50 |
| runtime total effects | 126 |

验证记录：

- Unity MCP refresh/compile：通过，Console 无编译错误；仅有 Unity Test Framework 保存 `TestResults.xml` 与 PerformanceTesting setup/cleanup 提示。
- `P5EffectSystemTests` + `P6ContentLandingTests`：46/46 通过。
- `NineGrid.Core.Tests` EditMode 全量：80/80 通过。
- `Assets/Notes/CI/check-core-guards.ps1`：通过。

下一批建议：批次6能力地基已经覆盖 `OnBattle` 来源过滤、通用事件来源过滤、非递归发牌链和来源敏感移除收益；若继续同簇，最小后续是 `removed-card stat snapshot v1`，处理 `skill.absorb_bone.pending`。如果切到新能力簇，则进入批次7 棋盘标记与场地规则。

### 6.2 批次7首个小闭环记录（2026-06-18）

目标：补齐棋盘标记/场地规则的最小闭环，先让福地成为可见、可触发、可随节点重置的棋盘状态；不把移动锁、全图视为相邻、陷阱帮助卡一起混进同一批。

本批新增能力：

- 新增 `BoardMarkId.Blessed` 与 `CoreEventType.BoardMarked`，`BoardModel` 现在提供 `IsMarked` / `SetMark` / `CountMarkedSlots`，并继续兼容既有 `IsBlessed` / `SetBlessed`。
- 新增 `SetBoardMarkAction`，棋盘标记变更通过 `GameAction` 进入流水线并写入 EventLog。
- 新增 `SetBoardMark` action atom：支持随机选择棋盘格、排除指定格、只选择未标记格，本批用于“非格5且未标记为福地”。
- 新增 `OnMoveToBoardMark` trigger 与 `BoardMarkEventCard` target：可以监听卡牌移动到指定棋盘标记，并把实际进入标记格的卡作为动作目标。
- `EffectAtomSchemas` 增加标记 atom 的 `mark`、`targetKind`、`slot/count/excludeSlots` 校验，错误 mark 或越界格子会在 catalog validation 阶段失败。

本批转换：

- `skill.guide.create_blessed`：MonsterSkill / Triggered / `OnSelfMove every 3`，随机将一个非格5、未标记的棋盘格标记为 `Blessed`。
- `skill.guide.blessed_enter`：MonsterSkill / Triggered / `OnMoveToBoardMark Blessed + targetKind Monster`，任意怪物移动到福地时获得 `Armor +2` 与 `Attack +1`。
- `skill.guide.pending` 已移除，`monster.ringleader` 现在会随内容应用激活这两个 DSL 效果。

刻意延后：

- `help.bear_trap.pending` 仍保持 pending。它可以复用 `OnEnter`/相邻目标能力，但“帮助卡自身作为陷阱、补牌触发后移除自身”的语义适合下一个小闭环单独验。
- `skill.range_expand.pending` 仍保持 pending。它是“所有怪物视为与本卡正交相邻”的邻接规则重写，需要 `RuleModifier` 或 BoardSystem 邻接查询入口，不应伪装成标记。
- Boss 移动锁仍延后。它需要移动入口查询规则并拒绝/跳过旋转、交换、补位等多种移动来源。

批次7首个小闭环后数量：

| 项目 | 数量 |
|:--|--:|
| hardcoded implemented effects | 78 |
| hardcoded explicit pending effects | 17 |
| pending monster skill placeholder effects | 32 |
| runtime pending effects | 49 |
| runtime total effects | 127 |

验证记录：

- `Assets/Notes/CI/check-core-guards.ps1`：通过。
- Unity MCP refresh/compile：通过，Console 无 error；仅有 TestResults 保存与 PerformanceTesting cleanup 提示。
- `NineGrid.Core.Tests` EditMode 全量：83/83 通过。

下一批建议：继续批次7B，做 `help.bear_trap.pending` 的补牌陷阱触发与自移除；或者做 `skill.range_expand.pending` 的邻接规则改写入口。

## 7. 当前事实基线

### 已经可靠的地基

- `NineGrid.Core` 已有 Action 流水线、TriggerSystem、StatSystem、EffectSystem、ContentSystem、Reward/Economy/Board/Deck/Phase 等核心系统。
- P0-P4 已有较完整地基测试，P5/P6 也已经出现真实 catalog DSL 测试，不再只是机制替身。
- `EffectValidator` 当前已经接入 `EffectAtomRegistry` / `EffectAtomSchemas`，能校验未知 atom、缺字段、嵌套 action graph、参数范围。
- Luban 工程、表定义、生成代码、StreamingAssets 数据、`TableNineLubanCatalogFactory` 与 bootstrap 已接入，能跑最小样例。

### 当前还没签收的部分

- `TableNineContentCatalog.cs` 仍是主要内容真源，Luban 只是最小样例，不是生产内容源。
- hardcoded catalog 预计有 76 个 implemented effect、50 个 pending effect，R4 远未清零。
- 设计文档里效果总量更大：帮助卡 22 条、遗物 58 条、玩家技能 7 条、怪物技能 81 条。hardcoded catalog 覆盖了帮助卡和玩家技能的大部分，但遗物和怪物技能仍是子集/原型。
- R5 的全内容回放网、pending 闸门、Luban 全量迁移闸门还没有完成。

## 8. 核心判断

不要按“帮助卡 -> 遗物 -> 玩家技能 -> 怪物技能”硬推进。那会把同一种能力重复拆开，越做越乱。

正确推进方式是按“能力簇”推进：

1. 先有盘点和 pending 闸门。
2. 再补通用表达力，比如动态数值、目标过滤、生命周期/规则改写、奖励选择、计数器、棋盘标记。
3. 每补一个通用能力，只转 2-5 个真实内容。
4. 每批都用 schema + catalog DSL + 行为测试证明。
5. 最后再全量迁表、pending 清零、CI 卡死。

## 9. 效果分类准则

每条效果先归入三态之一：

| 分类 | 何时使用 | 例子 |
|:--|:--|:--|
| Modifier | 改数值，且数值可随条件实时求值 | 攻击+1、处于格6攻击+2 |
| RuleModifier | 改规则或判定点 | 恢复翻倍、金币抵消伤害、只能攻击本卡 |
| Triggered | 某时机发生动作 | 每移动N次洗入卡、击杀时造成伤害 |

不要把“规则改写”伪装成属性，不要把“动态数值”做成一堆专属 action，不要靠“记得减回去”处理临时效果。

## 10. 批次规划

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

### 批次4：生命周期、防伤、规则改写 v1（首个小闭环已落地）

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

### 批次5：奖励、选择、内容动作（血液转换补全已落地）

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
- 对“随机立即发放”类效果，使用 `GrantRewardFromPool`，仍通过 GameAction 和 EventLog 验证。

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

## 11. 推荐下一步

下一次如果要直接进入实现，建议从：

1. **继续批次4 后续小批**：做 `skill.taunt.pending` 的交互合法性规则，或做 `relic.gold_armor.pending` 的金币抵伤。
2. **继续批次5 后续小批**：实现 `ChoicePayload v1 / SelectedTargets v1`，处理 `help.stat_boost_card.pending`、`help.swap_card.pending`、`help.teleport_card.pending`。
3. **继续批次6后续小批**：基于 `sourceDefId/cause` 处理被移除卡属性快照，优先收 `skill.absorb_bone.pending`。

## 12. 给后续 AI 的技能入口

已创建 Codex skill：

`C:\Users\jinji\.codex\skills\table-nine-effect-batches`

后续可以直接说：

```text
用 $table-nine-effect-batches 落地批次1
用 $table-nine-effect-batches 落地批次2
用 $table-nine-effect-batches 复核当前 pending burn-down
```

该 skill 会要求 AI 先读规则、当前规划、current map、batch roadmap、effect taxonomy，再按小批次开发和验收。
