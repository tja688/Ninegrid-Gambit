---
status: accepted
---

# 非正式内容不得进入正式局投放

## 决策

归档卡组（`deck.help_archive` / `deck.relic_archive`）、AI 拓展卡组（`deck.ai_expansion`）与过渡卡组（`deck.transition`）只保留 JSON 与效果实现，**不得出现在正式游戏投放面**。统一门禁 `FormalContentWiring`：

- **随机池 / 奖池 / 开局序列抽选**：排除非正式卡组，并排除 `isReserve` 怪物（遭遇不抽召唤物）。
- **具名 Spawn / 洗入 / 授予遗物 / 道具卡格写回**：排除非正式卡组。`isReserve` 召唤物仍可被具名 Spawn（死亡召唤等）。
- Catalog 未登记的 defId（测试夹具）放行。

既有分站点过滤（奖池只拦 `help_archive`、随机扫 Catalog 只拦 `isReserve`、授予只拦遗物归档）全部改走这一处判定。

## 为什么

玩家局内截图反复出现倍增塔（归档道具）与 AI 拓展卡。根因不是某一条奖池规则写错，而是**护栏只钉在静态 defId 引用与个别装填入口**，`ShuffleRandomContent`（大力腰带 / 造物之镰 / 回收手链等 live 遗物）按 Kind 扫全 Catalog，归档卡 `isReserve=false`、AI 拓展成员同样 `isReserve=false`，因此能被洗进抽牌堆再补到场上。每修一条静态路径，随机扫盘路径仍在漏。

## 考虑过的替代

- **继续在各入口补排除列表**：否决——已多次复发，入口会继续增加。
- **给归档/AI 卡一律打 `isReserve=true`**：否决——Reserve 语义是「遭遇不抽、效果可直生」的召唤物；AI 拓展与归档不是召唤物，混用会让死亡召唤一类正式链路误伤。
- **从 Catalog 删除非正式卡**：否决——效果实现与编辑器参考需要保留 JSON。

## 后果

- 正式局随机帮助卡 / 随机怪物 / 奖池展开 / 常规机关池 / 职业来源池 / 存档写回 / 房间注入 / 具名授予均不得放出倍增塔、瞭望塔、AI 拓展成员等。
- 作弊加卡/加遗物搜索同步排除非正式卡组。
- 卫生校验增加展开后奖池扫描；EditMode `FormalContentWiringLeakTests` 锁死倍增塔与 `ShuffleRandomContent` 候选池。
- QuickTest / 教学关具名注入的正式或教学卡不受影响；教程卡组 `deck.tutorial` 不在非正式名单。

## 相关

- [ADR-0033](0033-help-card-rarity-distribution-control.md) — 道具稀有度投放与归档写回守卫
- [ADR-0029](0029-content-guardrail-stable-theme-deck-mapping.md) — 主题卡组正式可达
- `#115` / `#139` — 遗物/道具归档卡组约定
