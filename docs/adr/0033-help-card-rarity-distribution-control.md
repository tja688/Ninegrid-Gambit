---
status: accepted
---

# 道具卡稀有度分级与投放控制（常规/特殊）

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
