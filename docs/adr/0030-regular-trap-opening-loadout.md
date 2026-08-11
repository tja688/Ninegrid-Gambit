---
status: accepted
---

# 常规机关开局装填契约

## 决策

正式战斗卡组生成阶段（`RewardSystem.BuildNodeDeckOptions`，Core 侧唯一注入点）开局随机注入 **三张常规机关** 进敌侧发牌池，经正常发牌/补牌上场。

- **常规机关池**（`RegularTrapPool.CollectRegularTraps`）：Kind=`Trap` 且稀有度 **White** 且非 Reserve。按策划「稀有度」列标记（常规=White / 特殊=Red / 离开机关=None），数据驱动——新增常规机关只需标记 White。
- **排除项**：离开机关（`trap.leave`，唯一清关手段）与技能/遗物专用特殊机关（烈焰/治疗泉 Red、复活石 None）绝不出现在初始随机三张内。
- **抽取语义**：无放回（同一场不重复）；不足三张按池量注入，池空注入零张；抽取顺序由 `IRngUtility` 种子决定，同种子同顺序可复现。
- **不借用 QuickTest 通道**：QuickTest `trapContentIds` 仅 `\1–\9` 定向注入，叠加在 Core 三张之上；`\0` 与正式镜像一致（同为 Core 注入三张，无额外注入）。
- **与离开机关插入正交**：常规机关仍不算真怪物（无击杀赏金）；离开机关在普通战斗房由 `AppendOpeningLeaveTrapCard` 开局编入（层主房仍击破开局层主后洗入，见 ADR-0026）。

## 为什么

策划案（`Assets/Docs/九宫格登神/04-敌人侧卡牌信息/机关卡.md`）规定「常规机关卡：每关卡开始时，战斗卡组生成时自动加入随机三张；特殊机关卡：只能通过遗物或技能效果加入」。统一在 Core 装填路径注入可同时覆盖正式流程与 `\0` 镜像，避免表现层/QuickTest 分支污染正式内容。

## 考虑过的替代

- **QuickTest `trapContentIds` 路径复用**：否决——正式内容不得依赖 DevTest 通道；隔离见 #125。
- **按卡组 `deck.trap` 全量过滤**：否决——会把离开机关与特殊机关也纳入随机池；稀有度标记是策划现有装填模型（机关卡.md 稀有度列）。
- **放回抽取（允许重复三张）**：否决——「随机三张」按装填模型取不同机关更符合战斗构成意图；池不足按池量。
- **硬编码六张 defId 列表为唯一来源**：否决——数据驱动 + 契约测试锁定当前六张组合（滚石/攻甲恢复图腾/倒刺/捕熊陷阱），加卡改稀有度即入池。

## 相关

- [ADR-0017](0017-trap-card-kind-and-dual-bucket.md) — Trap 双桶；无赏金；静默 CounterAttackBanned
- [ADR-0026](0026-leave-trap-sole-clear-condition.md) — 离开机关为唯一清关手段；普通房开局编入 / 层主房洗入
- [ADR-0022](0022-node-loadout-model.md) — 节点装填模型（`BuildNodeDeckOptions`）
