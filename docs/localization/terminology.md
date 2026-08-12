# 中英术语全表（Terminology zh ↔ en）

> 生成：2026-08-12。数据交付物：`Assets/Resources/Localization/en/glossary.json`（50 条）、`Assets/Resources/Localization/en/cards.json`（295 条）。
> 本表是后续补翻的**口径权威**：新增/修改翻译时先查本表，再改表数据；`[[词条]]` 英文名以 glossary.json 的 `displayName` 为准（Ordinal 逐字匹配，含大小写）。

## 1. 固定锚点（与 UI 翻译共用，禁止另译）

| 中文 | English | 中文 | English |
|---|---|---|---|
| 攻击 | Attack | 卡组 | Deck |
| 护甲 | Armor | 交战 | Combat |
| 血量/生命 | HP | 互动 | Interaction |
| 血量上限 | Max HP | 行动计数 | Action Count |
| 金币 | Gold | 移动计数 | Move Count |
| 遗物 | Relic | 离开机关 | Exit Trap |
| 道具卡 | Item Card | 回收 | Recycle |
| 机关 | Trap | 抽牌堆 | Draw Pile |
| 怪物 | Monster | 楼层/层 | Floor |
| 层主 | Boss | | |

## 2. 词条表（glossary.json，`[[…]]` 权威，共 50 条）

### 2.1 图标词条（CardFaceDescriptionIconCatalog 现有 12 条）

| code | 中文词条名 | English |
|---|---|---|
| action | 行动图标 | Action Icon |
| adjacent | 正交相邻 | Adjacent |
| attack | 攻击 | Attack |
| armor | 防御 | Armor |
| HP | 血量 | HP |
| money | 金币 | Gold |
| MHP | 血量上限 | Max HP |
| death | 死亡 | Death |
| basic_armor | 基础防御 | Base Armor |
| 潜伏近战 | 潜伏近战 | Ambush Melee |
| 普通攻击 | 普通攻击 | Standard Attack |
| move | 移动计数 | Move Count |

### 2.2 攻击模式 / 机制标签词条

| 中文 | English | 备注 |
|---|---|---|
| 普通近战 | Standard Melee | 正交相邻近战 |
| 全向近战 | Omni Melee | 八向近战 |
| 斜角近战 | Diagonal Melee | 对角近战 |
| 链接近战 | Link Melee | 链接系近战 |
| 远程武器 | Ranged Weapon | 同名技能同译 |
| 神圣决斗 | Holy Duel | 同名技能同译 |
| 潜伏 | Lurk | 背面入场 |
| 链接 | Link | 链接机制标签 |
| 无视护甲 | Piercing | 伤害属性 |

### 2.3 技能名词条（与 cards.json 中对应 Skill 的 displayName 逐字一致）

| 中文 | English | skillId | 中文 | English | skillId |
|---|---|---|---|---|---|
| 吸收 | Absorb | skill.absorb | 空间掌握 | Spatial Mastery | skill.space_mastery |
| 献火 | Fire Offering | skill.offer_fire | 转动 | Spin | skill.turn_world |
| 剧烈燃烧 | Intense Burning | skill.intense_burning | 献身 | Martyrdom | skill.sacrifice |
| 损耗 | Attrition | skill.attrition | 跳杀 | Pounce | skill.leap_kill |
| 快递 | Delivery | skill.delivery | 提速 | Quicken | skill.speed_up |
| 历战 | Battle-Hardened | skill.battle_hardened | 链接战术 | Link Tactics | skill.link_tactics |
| 嘲讽 | Taunt | skill.taunt | 链接准备 | Link Prep | skill.link_prep |
| 叠甲 | Plating | skill.stack_armor | 链接护甲 | Link Armor | skill.link_armor |
| 天涯若比邻 | Distant Neighbors | skill.world_as_neighbors | 盗取 | Steal | skill.steal |
| 刺客领袖 | Assassin Leader | skill.assassin_leader | 呼唤 | Beckon | skill.call_melee6 |
| 死亡之主 | Lord of Death | skill.lord_of_death | 死亡召唤 | Death Summon | skill.death_summon |
| 烈焰沸腾 | Boiling Flames | skill.flame_boiling | 吟唱 | Chant | skill.chant |
| 吞云吐雾 | Fire Breather | skill.cloud_breath | 休养 | Recuperate | skill.recuperate |
| 生生不息 | Everburning | skill.endless_flame | 逃避 | Evade | skill.evade |
| 起来 | Rise Up | skill.rise_up | | | |

## 3. 专名对照（描述内引用时必须同译）

### 3.1 实体卡牌（被其它描述引用的）

| 中文 | English | contentId |
|---|---|---|
| 烈焰（机关） | Flame | trap.flame |
| 烈焰（怪物/层主） | Blaze | monster.shelter_stone |
| 复活石 | Revive Stone | trap.revive_stone |
| 旋转轮 | Rotation Wheel | help.rotation_wheel |
| 恢复药水 | Healing Potion | help.healing_potion |
| 飞刀 | Throwing Knife | help.throwing_knife |
| 交换 | Swap | help.swap_card |
| 金色宝箱/高级宝箱 | Golden Chest | help.golden_chest_card |
| 普通宝箱 | Common Chest | help.common_chest_card |
| 进阶宝箱 | Advanced Chest | help.blue_chest_card |
| 轻弓骷髅 | Shortbow Skeleton | monster.smuggler |

### 3.2 中文源沿用的历史指称（对应卡多为归档/储备，译文按字面直译）

| 中文 | English | 说明 |
|---|---|---|
| 重生骷髅 | Reborn Skeleton | monster.summon.special_omni（transition，跳过；trap.revive_stone 描述引用） |
| 近战6 / 近战3 | Melee 6 / Melee 3 | 技能描述引用的历史名（现行卡名已改）；按字面保留 |
| 乞丐 | Beggar | skill.beggar_bond 引用；monster.beggar 现名"人面莱姆/Face Slime" |
| 龙信徒 | Dragon Follower | 呼唤信徒/献祭引用；monster.dragon_follower 现名"幽灵炎/Ghost Flame" |
| 骷髅头 / 无头骷髅 | Skull Head / Headless Skeleton | 散架/重新组合引用；对应卡现名已改为莱姆系 |
| 多骨虫 | Multi-Bone Worm | 折损散架引用（transition 卡） |
| 巨大骷髅 | Giant Skeleton | 强力组合引用（transition 卡） |
| 石人 | Stone Man | 落石引用（transition 卡） |
| 福地 | Blessed Ground | skill.guide 创建、skill.find_weakness 引用 |

### 3.3 时序标签（仅 Skill 描述出现；非图标代号，按英文标签直译）

| 中文 | English |
|---|---|
| [场上] | [On Board] |
| [战斗时] | [In Combat] |
| [战斗后] | [After Combat] |
| [被移除时] | [On Removal] |
| [翻面后] | [After Flip] |

### 3.4 种族 / 系列词

| 中文 | English | 中文 | English |
|---|---|---|---|
| 莱姆 | Slime | 决斗者 | Duelist |
| 哥布 | Goblin | 烈焰一族 | the Flame clan |
| 骷髅 | Skeleton | 木质套装 | Wooden set |
| 熔炉（系列） | Forge | 潮汐（系列） | Tide |
| 回收（遗物系列） | Recycle/Recycled | 教学·X（机关名） | Tutorial: X |

### 3.5 UI/结构词（卡牌数据内出现）

| 中文 | English | 中文 | English |
|---|---|---|---|
| 行动时（怪物开火） | on act: | 进入战斗时 | Combat start: |
| 每互动N次 | Per N Interactions: | 每移动N次 | Per N moves: |
| 击杀时 | On kill: | 被移除时（卡面文） | On removal: |
| 战斗节点 | combat node | 关卡开始 | Node start: |
| 常规怪物 | regular monster | 战斗卡组 | combat deck |
| 格N（1-9） | cell N | 中心格 | center (cell) |
| 角格 | corner cell | 左列 | left column |
| 正面/背面 | face-up / face-down | 翻面 | flip |

## 4. 归档跳过清单（46 条，未进 cards.json）

**跳过原因**：spec §0 规定归档卡 v1 不翻；`deck.relic_archive` / `deck.help_archive` 成员不参与奖池与授予，`deck.transition` 成员全部 `isReserve=true` 不参与正式遭遇。
**后补方法**：这些卡若复活，把 contentId 加进 `cards.json.entries`（字段同 live 卡：displayName/description/faceIntro，红线同 spec §6），术语先查本表；无需改任何代码。

| 桶 | 数量 | contentId 清单 |
|---|---|---|
| help 归档 | 7 | help.armor_breaking_hammer（破击锤）、help.blood_conversion（血液转换）、help.doubling_tower（倍增塔）、help.shield_bash_tutorial（盾击教程）、help.stat_boost_card（属性提升）、help.ward_magic_card（庇佑）、help.watchtower（瞭望塔） |
| relic 归档 | 9 | relic.arsenal（军械库）、relic.battle_hardened（历战）、relic.blood_shockwave（血液冲击波）、relic.easy_road（轻车熟路）、relic.even_hatred（偶数仇恨）、relic.hard_skin（硬皮）、relic.junk_slot_machine（废物老虎机）、relic.thorn_skin（刺皮）、relic.tower_child（塔之子） |
| transition 怪物（全部 isReserve） | 29 | monster.big_orc、monster.executioner、monster.fire_cult_leader、monster.friendly_ancient、monster.giant_skeleton、monster.growing_stone、monster.killer、monster.megalith、monster.melee_6、monster.mist、monster.multi_bone_worm、monster.observer、monster.old_orc、monster.orc_boss、monster.orc_quartermaster、monster.orc_warrior、monster.rolling_stone_man、monster.rotating_cub、monster.sharp_stone、monster.skeleton_mage、monster.space_master、monster.stepwalker、monster.stone_golem、monster.stone_man、monster.stone_shrimp、monster.stone_swallower、monster.stone_thrower、monster.summon.special_omni、monster.void_cub |
| monster_decks 表行 | 1 | deck.transition（display_name「过渡卡组」；deck.transition 的 Deck 卡本体为 live，已翻） |

## 5. 翻译风格说明（补翻同口径）

1. **语域**：对标 Slay the Spire 英文版。卡面 description 用电报体祈使句/名词短语，内部用 `,` `;` `:` 分隔，**不加句末句点**（与中文源一致）；faceIntro 是风味句，加正常句末标点（`.` `!` `?` `~` 均可）。
2. **精简**：description 目标 ≤ ~34 拉丁字符（`{…}`/`[…]`/`[[…]]` 各按 1 格计）。可砍冠词（"Deal 3 damage to [adjacent] monsters"），不可砍语义（次数、随机性、范围、条件都要保留）。
3. **数字**：一律阿拉伯数字；倍数用 "double"/"2x" 时优先 "double"。
4. **红线**（spec §6）：`{装配id.键}` 令牌逐字保留可移位；`[[X]]` 必须逐字命中 glossary displayName；`[code]`（armor/attack/MHP/HP/adjacent/money/death/action/basic_armor）原样保留；id/路径永不改；JSON UTF-8 无注释无尾逗号。
5. **词条=技能名同步**：§2.3 中的词条改名时，glossary.json 与 cards.json 对应 skill displayName **必须同步改**（自检脚本会抓）。
6. **专名**：直译可行就直译（骷髅头→Skull Head）；含梗取意象（打金刀→Farming Knife、悬赏令 faceIntro 保留玩笑）。contentId 英文主题词是历史残留（ADR-0014），不作为翻译依据。
7. **field 规则**：cards.json 条目字段缺省=中文源无该字段；不得新增源里没有的字段。
8. **中间产物**：中文抽取清单与分批稿在 `Assets/Notes/Localization/_l10n_*` / `_en_batch_*.json`；改译文后跑 `_l10n_merge_and_check.py` 重新合并 cards.json 并自检。

## 6. 值得注意的口径决定

1. **glossary 扩到 50 条**：`[[…]]` 在中文源共引用 40 个词，其中 38 个不在 12 条图标词条表里（多为技能名）。为满足「en 描述中所有 `[[X]]` 可在 glossary 命中」，把全部被引用词收进 glossary，技能名词条与 Skill 卡英文名逐字对齐（脚本校验）。
2. **烈焰同名分流**：机关 trap.flame 与怪物 monster.shelter_stone 中文同名「烈焰」，英文拆为 **Flame**（机关，技能文本引用的都是它）与 **Blaze**（烈焰一族之王），避免规则文本歧义。
3. **卡组名合并键**：deckId 键单条同时服务 deck JSON `displayName` 与 monster_decks `display_name` 两个消费点，英文以 **deck JSON displayName 为准**（玩家右键详述实际所见），monster_decks 的主题名（莱姆牌组等）不另译。
4. **时序标签英译**：`[场上]` 等 5 个中文方括号标签只出现在 Skill 描述（编辑器/Workbench 可见），不是图标代号（词条表无此 code，运行时未命中即原样显示），按 §3.3 英文标签直译。
5. **历史指称按字面直译**：中文技能描述引用的「乞丐/龙信徒/骷髅头」等是旧卡名（对应卡现名已改），译文按字面直译并在 §3.2 登记，不擅自改指向。
6. **「潜伏近战」词条 intro**：词条表原文即为占位文「潜伏近战测试」，照实译为 "Ambush Melee test."，未擅自替换为技能全文。

## 7. 自检结果（2026-08-12，`_l10n_merge_and_check.py`）

| # | 检查项 | 结果 |
|---|---|---|
| ① | 三个 JSON 可解析（glossary.json / cards.json / zh 抽取清单） | **PASS** |
| ② | cards.json 覆盖全部 295 个 live contentId/deckId（缺漏 0、多余 0、字段缺口 0） | **PASS** |
| ③ | en 文本中全部 `[[X]]` 逐字命中 glossary en displayName（bad=0） | **PASS** |
| ④ | 全部 `{…}` 令牌逐条 multiset 比对 zh↔en（mismatch=0，共 155 处） | **PASS** |
| ⑤ | 超 34 格（token 记 1 格）description 计数 | **140 / 280**（详见下） |
| 附 | `[code]` 图标代号 multiset 比对 zh↔en | PASS（mismatch=0） |
| 附 | glossary 技能名词条 ↔ Skill displayName 同步 | PASS（mismatch=0） |

超长分布（超 34 / 有 description 总数）：ChoiceOption 1/8、Deck 4/4、HelpCard 6/24、Monster 2/45、Relic 31/56、Room 5/13、Skill 76/86、Trap 15/44。
说明：Skill 与 Deck 描述不进 26 格卡面槽（编辑器/详述侧），中文源本身超长；受 26 格契约约束的 Trap/Relic/HelpCard 共 52 条超标，多为多令牌复合句（每个 `{…}` 已按 1 格计），语义无损前提下已尽量压缩，未强行清零（spec §5：英文无硬限制，运行时依赖 autosize/字号系数兜底）。完整清单见 `Assets/Notes/Localization/_l10n_check_report.txt`。
