# 帮助卡 HandcardApplyZone 临时路由说明

> 日期：2026-06-24  
> 状态：**临时方案**，待 `ItemTargetingSession` + 完整 Profile 路由落地后替换。

## 背景

- Demo 模式下手牌布局正确（`ItemCardInteractionFsm` 挂在 `HandCardAnchors` 下，直接解析 `handcard1`…`handcard5`）。
- 正式拾取/落手曾落在屏幕中间：`TableNineViewRegistry` 与 `HandCardAnchors` 为场景**兄弟节点**，子树搜索找不到锚点，`handRoot` 回退到 `NineGrid Battle` 原点。
- 拖拽使用原先未发 `UseItemCommand`（MVP 注释「场地主导」），拖入释放区也无法出牌。

## 本次修复

### 落手位置

- `HandCardLayoutBindingUtility` 子树未命中时，**场景级**查找 `HandCardAnchors`，统一解析 `handcard1`…`handcard5` 与 `HandCardActors`。
- `TableNineViewRegistry.EnsureHandLayoutBindings()` / 拾取适配器 `HandRoot` 现可落到正确槽位坐标系。

### 使用路由（临时）

| Profile | 手势 | Command |
|---------|------|---------|
| **ApplyZone** | 拖入 `HandcardApplyZone` 松手 | `UseItem(uid)` |
| **SingleTarget / MultiPick** | 先拖入 `HandcardApplyZone` 松手 → 再**点选**场上合法目标（多选需点满 N 张） | `UseItem(uid, [targets…])` |
| **OptionOverlay** | 拖入释放区 → `SelectionOverlayFsm.TryUseItemWithOptionOverlay`（如 stat_boost） | `UseItem(uid, null, option)` |

- 实现：`ItemUseProfileResolver`（硬编码 defId）、`ItemCardInteractionFsm.ReleaseDrag`、`BoardItemInteractionCoordinator` 点选会话、`BoardInteractionFsm.ItemUseAssist`。
- 取消点选：点选阶段 **右键 / Esc** → `CancelApplyZoneTargeting`，卡仍在手。

### 未覆盖（仍待正式 Session）

- 拖放过程中场上 `TargetingAssist` 高亮批（`CardTargetEligiblePerformance`）
- 按 Profile 的精细非法目标提示 / 灰显
- 完整 V0.3 `ItemTargetingSession` 状态机与 canvas 多路由图

## 蓝图 / Canvas 备注

请在表现层 canvas（`九宫牌局表现层.canvas`）泳道 B 道具交互区追加黄色注释：

```text
★已落地（临时 2026-06-24）
HandcardApplyZone 释放区 Confirm + 点选目标；
ItemUseProfileResolver 硬编码首版；
待 ItemTargetingSession 替换。
```

并更新 `Assets/Notes/归档/表现层蓝图落地对照表.md` 对应行。

## 场景对象

- `HandCardAnchors/handcard1` … `handcard5`：布局参考锚点（**不是**演员父节点）
- `HandCardAnchors/HandCardActors`：手牌演员父节点
- `HandCardAnchors/HandcardApplyZone`：`BoxCollider` 释放区（`ItemCardInteractionFsm.applyZoneCollider`）

## 验证清单

1. 拾取帮助卡 → 飞入 `handcard` 槽位，非屏幕中心
2. 治疗/炸弹等 ApplyZone 卡：拖入释放区松手 → `UseItem` batch → 卡消耗
3. 飞刀等：拖入释放区 → 点怪物 → `UseItem(uid, [target])`
4. 交换卡：释放区松手 → 点两张场上卡
5. stat_boost：释放区松手 → 选项覆盖层
6. 释放区外松手 → 回手；点选阶段 Esc/右键 → 取消
