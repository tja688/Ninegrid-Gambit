---
status: accepted
---

# 中心格是拓扑，Avatar 占格随置换走

战斗内玩家可离开格 5 并跟外圈旋转。我们保持 Avatar 不进入场上卡占格表（认领、图标共占、`ZoneId.Avatar` 仍成立），同时把格 5 从「Avatar 永远在这」拆成拓扑**中心格**：不旋转、不补牌，与玩家是否站在此处无关。旋转 / 换位 / 移格凡置换到 Avatar 当前格，必须同步移动 Avatar 占格，否则双轨会重叠或把玩家钉死。否决了把 Avatar 写入占格表来复用现有两卡 `Swap` 原子。

## 考虑过的替代

- **把 Avatar 写进占格表当一张普通卡**：否决——会撞上 Avatar 永不认领、与场地图标共占、开战归位、补牌跳过语义。
- **中心空了就按现有补牌发进格 5**：否决——中心不转是格的性质；空中心发牌会把「中心炮台」变成常规补牌结果。

## 相关

- [ADR-0019](0019-avatar-board-walk.md) — 非战斗跳格；开战仍须回到中心格
- [ADR-0023](0023-slot-hit-frame-and-claim.md) — Avatar 永不认领
- [ADR-0038](0038-card-rhythm-dual-channel.md) — 移动计数仍只属于场上卡，不套在 Avatar 上
- [ADR-0057](0057-avatar-swap-off-home.md) — 换位离巢与归位倒计时
