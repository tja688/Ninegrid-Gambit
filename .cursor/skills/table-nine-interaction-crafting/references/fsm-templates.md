# 交互 FSM 模板（来自表现层 canvas V0.2）

## BoardInteractionFsm — 场地卡

```text
Idle ──pointer enter──► Hover ──click──► Selected ──┬──hold──► Hold
  ▲                      │                         ├──飞刀──► KnifeTargeting
  │                      │                         └──click/confirm──► Cmd
  │                      └──pointer exit──► Idle    Hold/Knife ──confirm──► Cmd
  └──────────────── reset / Watching 吞操作 ─────────┘

本地反馈绑定：
  Hover      → CardHoverPerformance
  Selected   → CardSelectedPerformance
  Hold       → CardHoldPerformance
  Cmd 节点   → 不发反馈，只 SendCommand
```

**Command 节点候选**（按交互类型选一）：

- `AttackCommand` / `PickupCommand` / `ClickEmptySlotCommand`
- `UseItemCommand`（带 `selectedCardUids` / `option`，需 Core 已暴露参数）

## ItemCardInteractionFsm — 道具卡

```text
Idle ──enter──► Hover ──press/drag──► Drag ──release valid──► Confirm ──► Cmd(UseItem)
  ▲               │                    │
  │               └── ItemCardInteractPerformance (hover 变体)
  │                    └── drag 变体持续驱动
  └──────── cancel / Watching ─────────┘

Confirm 是唯一 Command 出口。Drag 期间演员跟随指针/锚点预览，不修改内核 zone/slot。
```

## SelectionOverlayMode — 覆盖层 N 选 1

由 `PendingChoiceKind` / `RewardOffered` / `RoomChoicesOffered` 拉起，**一个参数化模式**覆盖：

- 三选一奖励、房间选择、酒馆、删牌、路线、帮助卡选择
- 棋盘内飞刀目标（`KnifeTargeting`）与之统一为「当前 PendingChoice 驱动选项高亮 + Confirm」

```text
(外部 Phase 拉起) ──► SelectionOverlayMode ──► 覆盖层选项 Hover/Selected
                              │
                              └── Confirm ──► SelectReward / SelectRoom / EnterRoom / SkipHelpChoice
```

**不要**为每种选择单独新建 FSM 类；用 `PendingChoiceKind` 分支选项源与 Confirm Command。

## Watching 子态（所有 FSM 共有）

```text
正常态 ──IsInputLocked=true──► Watching
  - 强制退出：Selected / Hold / Drag / KnifeTargeting / Confirm 悬停
  - 保持：Hover（只读）
Watching ──IsInputLocked=false──► 恢复进入锁前的 Idle（或清空选中，按产品设计二选一，默认 Idle）
```

## 与 Flow Shell（泳道 A）的衔接

- `NodePlaying` 激活 `BoardInteractionFsm` + `ItemCardInteractionFsm`
- `RewardScreen` / `RoomChoiceScreen` 激活 `SelectionOverlayMode`，**挂起**棋盘/道具的可操作态（可保留只读 Hover 或整 FSM Sleep，按屏设计）
