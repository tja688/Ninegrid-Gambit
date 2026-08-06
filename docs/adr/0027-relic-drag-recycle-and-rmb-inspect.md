---
status: accepted
---

# 遗物栏：拖入回收区丢弃 + 右键详述

装备栏遗物与道具卡格**共用**回收区 UI（`HandcardRecycleZone` / `CardRecycleNotice`），交互改为：

- **左键按住拖动**遗物图标：拖动时幽灵图标抬高排序并半透明，栏位槽图标暂时隐藏且**不改布局**；松手落入回收区 ⇒ Core `DiscardRelic`（+DiscardRelicGold，默认 20）并同步栏位；未落入则图标归位恢复显示。满栏 Bounce 腾空时仍须能拖（含半黑屏盖住栏位）。
- **右键** ⇒ 打开既有卡牌详述（`CardInspectOverlayPresenter`），按 `relic.*` defId 挂遗物卡面；**不再**右键直接丢弃。
- **宝箱 UseItem 满栏前置拒收**（2026 修正）：`PhaseSystem.UseItem` / `ApplyUseItem` 进入须用效果前，若识别为「开宝箱选遗物」（`OnSelfUsed` → `OfferRewardChoice(relic.*)`）且 `PlayerModel.IsRelicInventoryFull == true`，**直接拒收**「遗物格子已满」；**不**开 `PendingChoice.Reward`，**不**消耗宝箱卡。表现层 `UseItemIntentScriptFactory` 把卡片归还原位（回手）；玩家须先 `DiscardRelic` 腾出再开宝箱。避免表现层 `BounceFan` `while(true)` 反弹死循环。仅对宝箱生效——其它 OnSelfUsed 输入卡片（属性提升、爆弹等）不受满遗物栏约束。

Core 经济命令仍分岔：道具回收走 `RecycleItemSlot`（+10），遗物丢弃走 `DiscardRelic`（+20）；共享的是表现层回收区与拖放手感，不是同一条 Core 命令。

## 为什么

右键即丢与全局「右键=详述」冲突，误触会直接毁掉遗物。拖入回收区与道具卡格同一套离手心智，满栏腾空也不再依赖特殊右键穿透。

## 考虑过的替代

- **保留右键丢弃、另做确认菜单**：否决——多一层确认仍与详述抢右键。
- **遗物改走 RecycleItemSlot 同一 Core 命令**：否决——金币与反激活效果路径不同，不能并命令。

## 相关

- [ADR-0025](0025-item-slots-run-persistent-hold.md) — 道具卡格回收区语义
- [ADR-0004](0004-input-intake-two-axis-gating.md) — 意图收口；`DiscardRelic` 仍为模态、ChoiceOverlay 下满栏可放行
- #98 — 遗物栏上限与经济；本 ADR 只改**交互入口**，不改容量与金币数
