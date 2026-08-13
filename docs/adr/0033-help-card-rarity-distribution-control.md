---
status: accepted
---

# 道具卡稀有度分级与投放控制（常规/特殊）

> **现行语义见「修订（2026-08-13）」**：常规三档（白=高/蓝=中/金=低，随机装填 60/30/10 加权）+ 特殊（红，仅定向渠道）。下方原始决策为历史记录。

## 决策

道具卡「稀有度」作为**投放契约**而非纯展示：`ContentRarity.White` = **常规**，Blue/Gold/Red = **特殊**（ADR-0033 起，`HelpCardDecks.RegularRarity`）。

- **常规（White）**：可进玩家侧卡组随机来源池（`ProfessionCatalog.SeedItemGenerationRules` → `ItemSourcePoolDefIds`），每战斗节点随机装填只从常规中抽取（`RewardSystem.AddRunPlayerSideCards`）。
- **特殊（Blue/Gold/Red）**：不进随机来源池；只经**定向渠道**投放——商店固定货架（宝箱卡 / 随机属性卡）、战斗房注入（金币房 → 金币卡、宝箱房 → 宝箱卡、困难房混合注入）、宝箱奖励房 / 道具奖励房固定项、精英击杀奖池（`kill.elite`）、层主击杀固定注入（1 金宝箱 + 2 金币卡）、遗物效果（幸运硬币等）。
- **稀有度与策划表对齐**：食品卡 / 绑票 / 撞击教程按策划「常规」由 Blue 改 White；其余 9 张常规（恢复药水等）维持 White；7 张特殊（金币卡 / 三宝箱卡 / 加攻 / 血量 / 加甲）维持 Blue/Gold/Red。
- **编辑器只读展示**：卡面配置器全卡种显示「稀有度」行（白/蓝/金/红），只读；无稀有度概念的卡种（玩家卡 / 技能 / 房间 / 选项卡）统一显示「无」。

## 为什么

策划要求「关卡开始时从已确定的道具卡来源中**随机抽取常规道具卡**」；此前实现把全部 19 张现行道具卡等权放入随机来源池，特殊卡（金币卡 / 宝箱卡 / 属性卡）也能随机进玩家侧卡组，与文档不符。稀有度轴此前只服务奖池权重（65/30/5），没有「常规/特殊」语义。

## 考虑过的替代

- **新增独立「常规/特殊」枚举字段**：否决——与既有 `rarity`（White/Blue/Gold/Red）双轴并存会造成两处真相；策划表与遗物「品质」均以稀有度为唯一轴，White 即常规是自然映射。
- **来源池全量保留、只在随机装填时过滤**：否决——来源池同时供奖励房随机道具（`RollRandomItemDefId`）消费，过滤放在源头一处即可同时约束两条随机路径；且 `ItemSourcePoolDefIds` 作为领域词「来源池」应语义自洽（即常规来源）。

## 后果

- 玩家侧卡组随机装填范围收窄为 12 张常规；重复可能仍在（等权有放回）。
- 奖池（`help.choice` 65/30/5）中食品卡 / 绑票 / 撞击教程从蓝档移入白档，出现率随权重变化；属策划表对齐的预期平衡影响。
- 契约测试 `FormalContentReachabilityContractTests`：来源池恰为 12 张常规；7 特殊卡必须各有 ≥1 个定向正式来源（原「全部 19 张进池」断言作废）。
- 新增内容（编辑器新建道具卡）若未标稀有度（None）将**静默不进随机来源池**，只能经定向渠道投放；如需进随机池须标 White。

## 修订（2026-08-13）：常规三档 + 特殊，权重 60/30/10，持有写回守卫，副图标

按策划最新《道具卡》《卡组生成流程》，稀有度轴从「白=常规，蓝/金/红=特殊」改为**四档投放契约**：

- **常规三档**：White=高 / Blue=中 / Gold=低，均可进随机来源池；随机装填（开局玩家侧卡组、
  道具奖励房/宝箱奖励房随机货架、卡店「道具卡固定」候选）按**高60 / 中30 / 低10**加权
  （`HelpCardDecks.GetRegularFillWeight` + `RewardSystem.RollRegularItemDefIdWeighted`），
  同档内均匀；池内缺档时该档不参与。
- **特殊（Red）**：不进任何随机来源池/随机装填；只经定向渠道——宝箱房/金币房/属性房开局注入、
  商店与道具奖励房固定属性货架、精英击杀（`kill.elite`）、层主击杀固定洗入、遗物效果。
  特殊卡集合不变：金币卡 / 普通宝箱 / 蓝宝箱 / 金宝箱 / 加攻 / 血量 / 加甲（全部改标 Red）。
- **卡面档位对照（策划表）**：高=恢复药水/飞刀/耐用盾牌；中=火球术/暴力卡/爆弹/交换卡/撞击教程
  （撞击教程策划未标出现率，暂按「中」实装）；低=旋转轮/传送卡/食品卡/绑票。价格同步策划表
  （蓝宝箱 150→100、金宝箱 400→100、撞击教程 80→30）。
- **持有写回守卫补全**：`HelpCardDecks.FilterLiveHelpCards`——跑图存档与跨层库存恢复**道具卡格 /
  固定卡**前过滤：道具卡格只放行 live 道具卡（特殊档允许驻留）；固定卡按常规档过滤
  （`FilterRegularSourcePool`）。堵住旧存档把归档卡（倍增塔/瞭望塔等，仍在 Catalog 中可被
  `CreateDraft` 重造）带回正式局的最后通道。
- **卡面副图标**：道具卡标准模版「副Icon」节点下四档子对象（高常规/中常规/低常规/特殊）由
  `CardFacePresentationBinder.ApplySubIconRarityVariant` 按稀有度单选激活；稀有度 None 整组隐藏。
  快照克隆路径（翻面/检视/数值提交/倒计时提交/作弊预览）补齐 `Rarity` 透传。
- 奖池表 `help.choice` / `shop.helpCards` 权重 65/30/5 → 60/30/10（当前正式流程未使用，仅对齐语义）。

## 修订（2026-08-12）：宝箱卡渠道收口

策划确认宝箱卡（`help.common_chest_card` / `help.blue_chest_card` / `help.golden_chest_card`）**只由宝箱类房间与击杀掉落派发，不上商店货架、不进任何随机注入池**：

- **保留渠道**：宝箱房（`Treasure.json` 开局固定注入 1 普通宝箱）、宝箱奖励房（`BuildTreasureRewardShelves` 固定 1 普通宝箱）、精英击杀（`kill.elite` 池 → 进阶宝箱，卡面 `tag.kill_elite`）、层主击杀（固定 1 金宝箱 + 2 金币卡，卡面 `tag.kill_boss`）、遗物效果（黄金宝匣等）。
- **移除渠道**：商店固定货架第一格宝箱（`BuildShopShelves` 不再上架）；精英房开局混合注入池中的普通宝箱（`Elite.json` WeightedPool 移除，原约 1/3 概率随机混入开局玩家侧卡组）。
- **来源池写回守卫**：`HelpCardDecks.FilterRegularSourcePool`——跑图存档（`RunSaveGame.RestoreAfterCreate`）与跨层库存（`BattleSessionExecutor.RestoreRunInventory`）恢复 `ItemSourcePoolDefIds` 前过滤，只放行 live 常规（White）道具卡；ADR-0033 前旧存档（全 19 张进池）不再把宝箱卡带回随机来源池。

## 相关

- `CONTEXT.md` — 玩家侧卡组
- [ADR-0022](0022-node-loadout-model.md) — 玩家侧卡组重生成模型
- `docs/code-map/README.md` — NineGrid.Core 契约
- `FormalContentReachabilityContractTests` — #139 归档 + ADR-0033 投放契约终审
