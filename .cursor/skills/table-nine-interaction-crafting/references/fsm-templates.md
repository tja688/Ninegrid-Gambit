# 交互 FSM 模板（来自表现层 canvas V0.3）

## ItemTargetingSession — 道具指向协调器（非手势 FSM）

```text
ItemCardInteractionFsm.Drag ──BeginDrag──► Session.Start(itemUid, profile)
                              UpdatePointer ──► Board TargetingAssist + 本地高亮
Drag 松手 ──┬── TryConfirm ──► payload → ItemFsm.Confirm → UseItemCommand
            └── Cancel ──► ItemFsm.Return

Session 订阅：ViewRegistry · ItemUseProfileResolver · InputLockGate(Cancel)
```

**不是第三套手势状态机**；不处理 press/drag 阈值，只回答「当前道具需要什么指向、指针下谁是合法目标」。

## BoardInteractionFsm — 场地卡

```text
【Normal — Session 未激活】
Idle ──pointer enter──► Hover ──click──► Selected ──hold──► Hold
  ▲                      │                         └──confirm──► Cmd(Attack/Pickup/ClickEmpty)
  │                      └──pointer exit──► Idle
  └──────────────── reset / Watching 吞操作 ─────────┘

【TargetingAssist — Session 激活，Normal 抑制】
Eligible ──pointer on──► TargetHover ──► TargetSelected（仅本地反馈，不发 Command）
```

本地反馈绑定：

| 子态 | Performance |
|------|-------------|
| Normal Hover | `CardHoverPerformance` |
| Normal Selected | `CardSelectedPerformance` |
| Normal Hold | `CardHoldPerformance` |
| Assist Eligible | `CardTargetEligiblePerformance` |
| Assist TargetHover/Selected | `CardSelectedPerformance`（或专用预览变体） |

**V0.3 移除**独立 `KnifeTargeting`；飞刀 = `ItemUseProfile.SingleTarget + Monster`。

## ItemCardInteractionFsm — 道具卡

```text
Idle ──enter──► Hover ──press/drag──► Drag ──┬── valid ──► Confirm ──► Cmd(UseItem+payload)
  ▲               │                          ├── invalid ──► Return ──► Idle
  │               └── ItemCardInteractPerformance
  └──────── cancel / Watching / Session.Cancel ─────────┘
```

**Confirm 是唯一 Command 出口。** Profile 分支：

| Mode | Confirm 条件 | Command |
|------|--------------|---------|
| ApplyZone | 在释放区内 | `UseItem(uid)` |
| SingleTarget | 指针在合法目标上 | `UseItem(uid, [target])` |
| MultiPick | 释放区 + 点满 N 张 | `UseItem(uid, [uids])` |
| OptionOverlay | 释放区 + 已选 option | `UseItem(uid, null, option)` |

★ `ItemCardInteractionFsm` 骨架已落地；待接 Session 与三参数 Command。

## SelectionOverlayMode — 覆盖层 N 选 1

由 `PendingChoiceKind` / `RewardOffered` / `RoomChoicesOffered` 拉起：

- 三选一奖励、房间选择、酒馆、删牌、路线
- **不含**道具棋盘指向（那是 `ItemTargetingSession`）

```text
(内核 PendingChoice) ──► SelectionOverlayMode ──► 选项 Hover/Selected
                              └── Confirm ──► SelectReward / SelectRoom / EnterRoom / SkipHelpChoice
```

## Watching 子态（所有 FSM + Session 共有）

```text
正常态 ──IsInputLocked=true──► Watching
  - 强制退出：Hover / Selected / Hold / Drag / TargetingAssist / Confirm 悬停
  - Session.Cancel()
  - 观演期拒绝一切指针交互（含 Hover）
Watching ──IsInputLocked=false──► Idle
```

## 与 Flow Shell（泳道 A）的衔接

- `NodePlaying` 激活 `BoardInteractionFsm` + `ItemCardInteractionFsm`
- `RewardScreen` / `RoomChoiceScreen` 激活 `SelectionOverlayMode`，挂起棋盘/道具可操作态

## 开发笔记

详见 `Assets/Notes/道具指向交互与场地卡FSM落地指南-2026-06-22.md`
