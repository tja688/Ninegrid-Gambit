---
status: accepted
---

# 属性房三选二会话（#136）

## 决策

属性房不再是「开局自动按权重抽 2 张」的房间，改为**玩家三选二会话**：

1. **进房即生成 3 个候选**：按该房 `RoomDefinition.OpeningInjects` 里 Player 侧 `WeightedPool` 声明
   加权抽取 3 张（策划权重为准，当前内容为血量卡 40% / 加甲卡 40% / 加攻卡 20%）；
   同种候选是否可重复以声明 `AllowDuplicates` 为准（当前为可重复）。
2. **玩家选 2 张**：选择按**候选实例**（optionIndex）而非 defId——选中实例立即从剩余候选移除，
   同实例不可重复点选；同种 defId 可重复选（两个候选都是血量卡时两张都可选）。
3. **选满两张即结束会话**：选择结果提交到 `RunModel.AttributePickDefIds`（≤2 张），
   清 Pending 并推进节点（复用 `mInRoomRewardContext` 的房间会话路径，同商店/卡店离开语义）。
4. **选择结果进「本关开局注入」容器**：下一战斗节点 `RewardSystem.BuildNodeDeckOptions`
   把这两张选择结果注入玩家侧战斗卡组（与 ADR-0022 的玩家侧开局注入同一容器），
   **不写道具卡格、不发明新持有区**。消费后清空，不会跨关重复注入。
5. **无交互直接随机注入两张的正式路径退役**：`BuildNodeDeckOptions` 对属性房不再自动抽 2 张；
   无选择结果（未开会话 / 会话被放弃）时按无注入处理。
6. **离开/放弃**：`SkipHelpChoice` 放弃未选完的候选（不发跳过金），结束会话并推进节点。
7. **清理**：新会话 offer 时先清 `RunModel.AttributePickDefIds`（防旧 Generation 残留）；
   `RunModel.Reset`（新 Run）清空。

## 状态落点

- 会话期选择暂存 `PendingChoiceModel`（`Kind=AttributePick`，pool=`attribute.pick`，
  `AttributeSelectedDefIds`）；只有选满两张才提交 `RunModel`。
- `RunModel.AttributePickDefIds` 是「本属性房战斗开局注入源」，只存活于
  「会话提交 → BuildNodeDeckOptions 消费」之间。
- 会话本身复用 `PendingChoiceKind` + `SelectReward` / `SkipHelpChoice` 命令与
  `RewardItemChoice` 相位，不新增相位、不复活旧浮层三选一。

## 为什么

设计案（《玩法.md》）规定属性房为「关卡开始时生成 3 个权重相关道具卡，选择 2 张（可重复），
加入玩家侧卡组」。直接随机注入剥夺了玩家在这三个候选间的取舍，与房间定位（可调度的资源选择）冲突。

## 后果

- 表现层（#137）按 `PendingChoiceKind.AttributePick` + `attribute.pick` 池渲染候选并提交选择。
- 既有 `RoomOpeningInjectContractTests` 属性房「自动抽两张」断言改为「无选择结果不注入」；
  三选二行为由 `AttributePickSessionContractTests` 覆盖。
- 属性房内容声明（`count=2` / `AllowDuplicates=true` / 40/40/20 池）保持不动，
  候选抽取与选择结果注入都以它为权威。

## 相关

- [ADR-0022](0022-node-loadout-model.md) — 开局注入容器与玩家侧卡组生成规则（本决策在其之上补充属性房交互段）
- [ADR-0025](0025-item-slots-run-persistent-hold.md) — 道具卡格语义；三选二结果不写道具卡格
