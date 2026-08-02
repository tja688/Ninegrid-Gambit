---
status: accepted
---

# 非战斗 Avatar 正交跳格

## 决策

玩家卡在**非战斗相位**（`RoomChoice` / `RoomEvent`）可通过点击目标格走正交最短路径：逐邻格 `MoveAvatar` + 复用场地 hop 动画。输入一律经 IntentIntake（`boardWalk`），忙时 latest-wins 缓冲；半空改目标等当前跳落地后重规划。`InteractionLoop` **禁止** `MoveAvatar` 与 BoardWalk。逻辑占格权威仍是 Core `BoardModel.AvatarSlot`。

**途经格优先完全空置**（绕开其它场地图标 / 可购选项 / 真卡）；**终点可为空格或场地图标格**（ADR-0020 的驻留提交需要踩上图标）。若空途经不存在（盘面堆满，多半设计失误），允许途经踩过软占与真卡到达终点。图标格作为终点时不改变图标归属，Avatar 与图标共占一格由几何注册表达。

## 为什么

非战斗房间的选房、导航与就地选项需要 Avatar 在九宫上连续跳格；战斗内 Explore 是补牌旋转互动，不是位移。硬相位门禁避免与写死格 5 的交战表现互相踩踏。

## 后果

- 正式开战前 Avatar 须回到格 5（战斗表现仍有 `AvatarReservedSlot=5` 假设）；进入下一房间时无条件复位到格 5，硬切不做走回表现。
- 终点放宽到图标格后，`AvatarWalkPathfinder` 对**终点**与**途经**须分别判定；途经优先空置，无空路再允许软占回退，不能再统一只用 `board.IsEmpty`。

## 历史

初版（#83 之前）曾以 `\0` QuickTest 作为跳格沙盒：`StartWalkSandboxNode` 空盘入 `RoomChoice`、开 `IAvatarWalkSystem`、拒对战意图，途经与终点均仅限空格。ADR-0020 落地后 `\0` 改为完整流程测试通道，沙盒随之取消——流程通道本身即包含跳格，沙盒的验证价值已被覆盖。

## 相关

- [ADR-0004](0004-input-intake-two-axis-gating.md) — IntentIntake
- [ADR-0020](0020-board-as-interaction-surface.md) — 场地图标、驻留提交、终点放宽的来由
- [code-map/presentation.md](../code-map/presentation.md) — BoardWalk / AvatarWalkSystem
