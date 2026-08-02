---
status: accepted
---

# 非战斗 Avatar 正交跳格

## 决策

玩家卡在**非战斗相位**（`RoomChoice` / `RoomEvent`）可通过点击空格走正交最短路径：逐邻格 `MoveAvatar` + 复用场地 hop 动画。输入一律经 IntentIntake（`boardWalk`），忙时 latest-wins 缓冲；半空改目标等当前跳落地后重规划。`InteractionLoop` **禁止** `MoveAvatar` 与 BoardWalk。逻辑占格权威仍是 Core `BoardModel.AvatarSlot`。`\0` QuickTest 为跳格沙盒：`StartWalkSandboxNode` 空盘入 `RoomChoice`、开 `IAvatarWalkSystem`、拒对战意图。

## 为什么

战后房间/商店将改为场地图标与选项卡就地交互，需要 Avatar 在九宫上连续跳格；战斗内 Explore 是补牌旋转互动，不是位移。硬相位门禁避免与写死格 5 的交战表现互相踩踏。

## 后果

- 正式开战前 Avatar 须回到格 5（战斗表现仍有 `AvatarReservedSlot=5` 假设）；本票沙盒不进战斗。
- 选房图标落格 / 走上去选房 / 选项卡生效属后续票。
- 途经与终点 v1 仅空格；图标占格终点后续放宽。

## 相关

- [ADR-0004](0004-input-intake-two-axis-gating.md) — IntentIntake
- [code-map/presentation.md](../code-map/presentation.md) — BoardWalk / AvatarWalkSystem / `\0` 沙盒
