---
status: accepted
---

# 遗物栏：拖入回收区丢弃 + 右键详述

装备栏遗物与道具卡格**共用**回收区 UI（`HandcardRecycleZone` / `CardRecycleNotice`），交互改为：

- **左键按住拖动**遗物图标：拖动时幽灵图标抬高排序并半透明，栏位槽图标暂时隐藏且**不改布局**；松手落入回收区 ⇒ Core `DiscardRelic`（+DiscardRelicGold，默认 20）并同步栏位；未落入则图标归位恢复显示。满栏 Bounce 腾空时仍须能拖（含半黑屏盖住栏位）。
- **右键** ⇒ 打开既有卡牌详述（`CardInspectOverlayPresenter`），按 `relic.*` defId 挂遗物卡面；**不再**右键直接丢弃。
- **宝箱 UseItem 满栏前置拒收**（2026 修正）：`PhaseSystem.UseItem` / `ApplyUseItem` 进入须用效果前，若识别为「开宝箱选遗物」（`OnSelfUsed` → `OfferRewardChoice(relic.*)`）且 `PlayerModel.IsRelicInventoryFull == true`，**直接拒收**「遗物格子已满」；**不**开 `PendingChoice.Reward`，**不**消耗宝箱卡。表现层 `UseItemIntentScriptFactory` 把卡片归还原位（回手）；玩家须先 `DiscardRelic` 腾出再开宝箱。避免表现层 `BounceFan` `while(true)` 反弹死循环。仅对宝箱生效——其它 OnSelfUsed 输入卡片（属性提升、爆弹等）不受满遗物栏约束。

**满栏拒收三防线（2026-08-15 修正）**：宝箱识别规则收敛为共享 `ChestUseRelicPoolRule`（Core），三层同源执行，杜绝识别漂移与拒收后「卡视图消失、内核仍持有、主线静默 Abort」的幽灵：

1. **Core 权威门禁**：`PhaseSystem.ExecuteUseItem` 拒收（同上，不变）。
2. **表现层 idle 合法性镜像**：`BoardIntentLegality.TryExplainUseItem` 同规则提前拒绝——拖放校验（`ValidateHandDragApplyAsync`）返回 false，卡经 `FinishDragWithReturnAsync` 直接回手（视图不消失），并播 `card.use.chest_reject` 拒绝音效 + 简要解释提示。
3. **时间线拒收善后**：`UseItemIntentScriptFactory` 的解算步（`BranchableResolveStep`）对权威拒收标记分支而非 Abort 整条主线；拒收分支经 `RejectedUseItemRecovery` 提示 + 拒绝音 + 把仍在道具卡格的卡视图重生回手。非战斗路径（`NonCombatUseItemIntentScriptFactory`）同款善后。

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
