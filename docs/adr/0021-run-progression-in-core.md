---
status: accepted
superseded_in_part_by: ADR-0025, ADR-0026
---

# 跑图进度与节点编排归 Core

## 决策

「玩家此刻在第几层第几节点、下一步能去哪种房间、什么时候通关」全部是 Core 规则，表现层壳**只读**。

- **规模**：`RunModel.NodesPerFloor` 由 9 改为 **8**，`FinalFloor` 保持 3，共 24 个**地图节点**。
- **节点编排**（每层同构，Core 权威）：

  | 节点 | 房间来源 | 清关 / 结束后放出 |
  |------|----------|-------------------|
  | 1 | 随机战斗房 | 2 个战斗房图标 |
  | 2 | 前房选定 | 2 个战斗房图标 |
  | 3 | 前房选定 | 2 个**消费房**图标 |
  | 4 | 前房选定（**非战斗**） | 1 个**离开**图标 |
  | 5 | 随机战斗房 | 2 个战斗房图标 |
  | 6 | 前房选定 | 2 个**特殊房**图标 |
  | 7 | 前房选定（**非战斗**） | 1 个**层主房**图标（战前缓冲） |
  | 8 | 层主房 | 1 个**下楼**图标；第 3 层则通关 |

- **非战斗节点（4 / 7）不进 `InteractionLoop`**：没有发牌、没有怪、没有清关判定。节点 4 靠离开图标推进；节点 7 放出层主房图标供走格进入，进入后 `AdvanceNode` 至节点 8 开战（给玩家心理准备，不再直接开战）。
- **困难房出现条件按层重新数**：每层节点 5、6 清关时放出的战斗房选项里才可能含困难房。
- **无清关三选一**：`CompleteNodeIfCleared` 里的 `OfferRewardChoiceAction("help.choice", 3)` 删除；清关直接放房间图标。
- **清关条件与收场（已修正）**：战斗房清关条件见 [ADR-0026](0026-leave-trap-sole-clear-condition.md)（离开机关击破，而非真怪物清零）。清关收场清掉场上残留且**不兑金**；道具卡格按 [ADR-0025](0025-item-slots-run-persistent-hold.md) 跨节点保留。~~旧条款「清关即全量结算场上+道具卡格每张 +10」废止。~~
- **通关**：第 3 层节点 8 清关后踩下楼图标（或等价推进）触发 `GamePhase.Victory`，沿用 `RunModel.AdvanceNode()`。

## 为什么

节点编排读起来像「表现流程」，实际每一条都是规则：节点 3 之后必给消费房、节点 8 必为层主、第 3 层末即通关、困难房有出现门槛。放在表现层壳意味着这些规则不可测、不可回放，也无法被 Core 的合法命令矩阵约束——现状就是壳层 `GameFlowShellSystem.NodeIndex` 与 Core `RunModel.NodeIndex` 两份进度并存，靠 `preserveRunInventory` 快照勉强对齐。

`RunModel` 本来就有 `Floor` / `NodeIndex` / `Seed` 与 `AdvanceNode()`，把编排收进来是**补齐**而非新建。

（历史注：曾选「清关即全量结算」以免扫场兑金；已被策划改为离开机关出口 + 主动回收经济，见 ADR-0025 / 0026。）

## 后果

- 表现层壳的 `NodeIndex` 退化为 Core 投影，`GameFlowOrchestrator` 的 `IncrementNodeIndex()` 不再是进度真相。
- `GameFlowOrchestrator.RunNodeCycleAsync` 的固定四段（Battle → Reward → RoomChoice → RoomEvent）不再适用于节点 4 / 7，循环须按 Core 给出的节点类型分支。
- `node_deck_rules.json` 由 9 行改 8 行（且节点 4 / 7 无怪物行）。
- `GoUp` 上一层图标存在但不接线，跑图为单向向下。

## 相关

- [ADR-0020](0020-board-as-interaction-surface.md) — 图标怎么放、怎么踩
- [ADR-0022](0022-node-loadout-model.md) — 每个节点具体装填什么牌
- [ADR-0017](0017-trap-card-kind-and-dual-bucket.md) — Trap 双桶
- [ADR-0025](0025-item-slots-run-persistent-hold.md) — 道具卡格持续持有
- [ADR-0026](0026-leave-trap-sole-clear-condition.md) — 离开机关清关
