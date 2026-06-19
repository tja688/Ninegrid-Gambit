# TableNine 效果系统批次8最终收束报告

日期：2026-06-19
范围：在前序 24 个 MonsterSkill 已落地基础上，继续彻底清空剩余 `6 HelpCard + 1 Relic + 1 PlayerSkill` pending，并同步 hardcoded catalog、Luban 源数据、StreamingAssets 运行数据与测试 gate。

## 1. 最终结论

本轮已将当前设计体量下 catalog 内所有 pending 效果清空。`TableNineContentCatalog.CreateDefault()` 与 Luban 运行数据已回到 parity，内容校验 gate 已收紧为 pending 必须为 0。

当前内容事实：

| 项目 | 数量 |
|:--|--:|
| effects total | 141 |
| implemented effects | 141 |
| pending effects | 0 |
| MonsterSkill pending | 0 |
| HelpCard pending | 0 |
| Relic pending | 0 |
| PlayerSkill pending | 0 |
| cards | 87 |
| skills | 68 |
| relics | 19 |
| monster decks | 6 |
| node deck rules | 9 |
| reward pools | 8 |
| rooms | 5 |

## 2. 本轮清空的 8 个 pending

| 容器 | 原占位容器 | 已落地 effect id | 分类 | 运行语义 |
|:--|:--|:--|:--|:--|
| HelpCard | `help.brutality_card` | `help.brutality_card.use` | Triggered | 使用后给玩家下一次对怪物造成伤害添加 x2 一次性倍率；非玩家来源或非怪物目标不会触发/消耗。 |
| HelpCard | `help.rolling_stone` | `help.rolling_stone.board_slot3`、`help.rolling_stone.use` | Triggered | 场上移动到 3 号格时，移除 6 号格普通怪物并移除自身；直接使用时移除自身。 |
| HelpCard | `help.armor_breaking_hammer` | `help.armor_breaking_hammer.use` | Triggered | 使用时选择 1 个场上怪物，基础护甲 -10。 |
| HelpCard | `help.healing_spring` | `help.healing_spring.board_adjacent`、`help.healing_spring.item_battle`、`help.healing_spring.use` | Triggered | 场上自身移动到玩家相邻格时治疗 2；在道具位时玩家对怪物战斗后治疗 1；直接使用时移除自身。 |
| HelpCard | `help.kidnapping` | `help.kidnapping.use` | Triggered | 使用时选择 1 个场上非精英非 Boss 怪物，将其全部护甲转给玩家并移除该怪物。 |
| HelpCard | `help.watchtower` | `help.watchtower.board_corner`、`help.watchtower.item_battle`、`help.watchtower.use` | Triggered | 场上自身移动到四角时随机怪物受 3 伤，累计 4 次后移除自身；在道具位时玩家对怪物战斗后随机怪物受 2 伤；直接使用时移除自身。 |
| Relic | `relic.heavy_armor` | `relic.heavy_armor.base`、`relic.heavy_armor.node_start` | Modifier + Triggered | 常驻基础护甲 +1；关卡开始按当前有效护甲的 50% 向下取整获得护甲。 |
| PlayerSkill | `skill.even_hatred` | `skill.even_hatred.rule` | RuleModifier | 玩家对偶数等级怪物造成伤害 x2。 |

本轮新增/替换为 14 条 implemented effect id，全部纳入 `P6ContentLandingTests.DefaultCatalogKeepsBatchSevenPendingGateAndConvertedEffectsImplemented` 的显式 implemented 断言。

## 3. 能力面变化

- `OnMoveToSlot` 支持 `slots` 多格触发，并可用 `counterKey` 在移动命中时累加自身计数。
- `SelectedCards`、`SlotCard` 支持 `minLevel`、`maxLevel`、`excludeElite`、`excludeBoss`，用于绑票/滚石这类普通怪物过滤。
- `AddRuleModifier` 支持 `conditionTarget: "None"`、`conditionActor`、`conditionTargetKind`，可表达“玩家对怪物下一次伤害翻倍”。
- `DealDamageAction` 的 stat context 现在携带 `ActorUid`，让 RuleModifier / EventFilter 可以区分伤害来源。
- `EventFilter` 的 stat 条件支持 `actorIs` 与 `sourcePrefix`，用于玩家技能与来源过滤。
- 值表达式新增 `Floor`，运行时值读取支持 `effective:true`，用于重盔甲按有效护甲计算。
- Schema 同步校验上述字段，避免 DSL 写入后绕过验证。

## 4. 数据同步

- 已更新 `Assets/Scripts/NineGrid.Content/Catalog/TableNineContentCatalog.cs` 的 effect 定义与容器绑定。
- 已通过 Unity 菜单 `TableNine/Content/Export Hardcoded Catalog To Luban Datas` 覆盖导出 `Assets/Tools/Luban/Datas`。
- 已运行 `Assets/Tools/Luban/gen_table_nine.ps1`，覆盖刷新 `Assets/StreamingAssets/TableNine/LubanData`。
- 已将 Luban 测试中的生产规模断言更新为 141 个 effect，并将 Luban pending gate 收紧到 0。

## 5. 验证记录

- Unity MCP refresh/compile：通过。
- Unity Console error：0。
- targeted EditMode：新增/收口相关 6 个测试，6/6 通过。
- Unity EditMode 核心全集：`NineGrid.Core.Tests`，112/112 通过。
- Luban parity：`P6LubanContentTests`、`P6R3LubanIntegrationTests` 均随全集通过。
- 数据搜索：`Assets/Tools/Luban/Datas` 与 `Assets/StreamingAssets/TableNine/LubanData` 中无 pending effect id。

## 6. 收束状态

当前 effect DSL 体系在现有设计体量下已无遗留 pending。后续如果新增卡牌/遗物/技能，应以 implemented DSL、测试覆盖和 Luban parity 为入库门槛，不再接受 pending 占位进入默认 catalog。
