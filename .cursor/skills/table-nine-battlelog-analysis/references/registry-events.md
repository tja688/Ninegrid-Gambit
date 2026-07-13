# RegistryLog / RegistryTrace 事件与约定

落盘：`Assets/Notes/Logs/OtherLog/RegistryLog/registrylog[-{runTag}]-{sessionId}-seed{seed}.json`  
与 CoreLog / PerfLog / BattleLog 共享 `sessionId` / `seed` / `runTag`（见 skill「日志发现」）。  
共通键：`beatId`（`DiagBeatClock`）、`tMs`（相对会话起点毫秒）。

## 设计原则

- **CardManager 注册表单线**：Release / Audit / 生命周期快照，不混在 PerfLog 海量 Motion 里。
- **自然落盘**：Play 退出 / 胜负 / 重开轮转即导出，**无需小键盘**。
- **自动检查点**：发牌后就位 + idle 段定时快照，等同原 Keypad7 意图。

## 自动检查点 trigger

| trigger | 何时 | 附带 |
|---------|------|------|
| `Opening.Settled` | 开局发牌 BeatClose 后 | `Checkpoint` + 全量 `BoardSnap` |
| `InteractionLoop.Idle` | 进入 InteractionLoop 后 | 同上 |
| `IdleWatch.1s` / `2s` / `3s` / `5s` / `7s` / `10s` / `15s` | idle 延迟自动审计 | `RegistryAudit` + `Checkpoint` + `BoardSnap` + **`FieldVisualAudit`** |
| `IdlePoll.*` | idle 段每 0.5s 轮询 | **`FieldVisualAudit`**；`visualMissing>0` 时额外 **`FieldVisualGap`** |
| `BeatClose.*` | 每个表现 Beat 结束 | **`FieldVisualAudit`**；有 gap 时 **`FieldVisualGap`** |
| `AuditMismatch.{原trigger}` | ghosts/orphans 非空 | 额外检查点 |
| `UserMark.*` | DevTest Keypad7（可选） | 同上 |

## kind

| kind | 何时 | 关键 payload |
|------|------|----------------|
| `RegistryDelta` | `_cardsByUid` 增删 | `op`, `countBefore`, `countAfter`, `reason`, `caller`, `defId` |
| `RegistryAudit` | 完整性审计 | `trigger`, `registryCount`, `fieldCount`, `ghosts`, `orphans` |
| `RegistryMiss` | TryGet 失败 | `detail` |
| `Despawn` | Release 回收视图 | `reason`, `caller` |
| `Vacate` | 场地占格注销 | `slot`, `caller` |
| `Checkpoint` | 生命周期/异常锚点 | `trigger`, `registryCount`, `fieldCount`, `ghosts`, `orphans`, `phase` |
| `BoardSnap` | 检查点伴随全量画面 | `phase`, `full`=1, `cards` 紧凑串（同 PerfLog 格式） |
| `FieldVisualAudit` | 场地可见卡审计 | `baselineVisible`, `visibleCount`, `visualMissing`, `emptyVisualSlots`, `hiddenOccupied`, `slotDetail`, `userInteractionCount` |
| `FieldVisualGap` | 可见卡少于 Opening 基线（**直接回答少几张**） | 同上 + `lastReleaseUid/reason/caller`, `lastVacate`, `beatKind` |
| `SuspectRelease` | 可疑 Release（如 Hand vanish 时仍占 Ground 槽） | `reason`, `caller`, `groundSlot` |

## Release reason 速查（沿 reason+caller 修调用方）

| reason | 典型 caller | 层 |
|--------|-------------|-----|
| `Combat.FinalizeLethal` | `FinalizeLethalVictimAsync` | 表现战斗 |
| `Combat.CompleteRemoveVictim` | `PresentRemovedFieldCardAsync` | 表现战斗 |
| `Hand.VanishAfterApply` | `VanishCardAfterApplyAsync` | 手牌 |
| `BounceFan.ReleaseEntry` | `ReleaseAllEntries` | 弹跳 UI |
| `Sync.SweepOrphan` | （Sync 清扫） | 表现 Sync |
| `Presentation.ResetCardSurface` | `ResetCardPresentationSurface` | 房间重置 |

## 与 PerfLog / CoreLog 互指

- RegistryLog 的 `RegistryDelta`/`Despawn` 与 PerfLog 同源镜像（Audit 仅 RegistryLog + CoreLog）。
- CoreLog `Presentation/RegistryAudit` 与 RegistryLog `RegistryAudit` 同 trigger，用 `accepted=false` 标 ghosts。
- 画面坐标细节回 PerfLog；**缺卡主嫌疑优先 RegistryLog**。

## 采集工作流（复现侧）

1. 正常 Play → 发牌就位 → **idle 等 ≥10s**（覆盖 IdleWatch 全档）
2. 看到 bug 也不用按键；直接停 Play 或打完一局
3. 取四件套同 `sessionId`：`registrylog` / `perflog` / `corelog` / `battlelog`
