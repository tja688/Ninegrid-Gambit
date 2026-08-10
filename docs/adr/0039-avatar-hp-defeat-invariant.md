---
status: accepted
---

# Avatar HP≤0 必须 Defeat；终端相位粘性；合法指令尊重存活

## 决策

1. **血量权威**：Avatar 当前血（`Stats.GetBase(StatId.Hp)`）≤0 时，Core 必须进入 `GamePhase.Defeat`。通过 Action Pipeline 的 `DefeatIfAvatarDeadAction` follow-up 达成，**不**在表现层 watch HUD，**不**引入 ReactiveProperty 式订阅。

2. **触发面闭合**：凡写入 Avatar 当前血并可能使其 ≤0 的 Action（至少 `DealDamageAction`、`ModifyBaseStatAction` 在 Hp / MaxHp 钳血后）须在 Apply 末尾挂上 `DefeatIfAvatarDeadAction` follow-up（幂等：已在 Defeat 时 no-op）。

3. **终端相位粘性**：`RunModel` 已为 `Victory` 或 `Defeat` 时，`ChangePhaseAction` **不得**再切到非终端相位（尤其禁止 `InteractionLoop` 覆盖 `Defeat`）。`StartNode` 末尾若 Avatar 已 0 血而相位仍非 Defeat，须补跑 `DefeatIfAvatarDeadAction`。

4. **合法指令**：`RefreshLegalCommands` 在 `InteractionLoop` 下若 `IsAvatarDefeated()`（读一手 HP），不得加入 `Attack` / `PickupItem` / `ClickEmpty` / `RevealFace` / `UseItem` 等战场交互指令。

5. **表现投影**：`AvatarDefeated` 投影旗标仍以 `phase == Defeat` 为准；`EnsureBattleEndedIfAvatarDefeated` 可兼读 Core phase / Avatar HP 作兜底，**不得**把战败权威上移到 HUD。

## 为什么

0 血僵尸局（`hp=0` + `InteractionLoop` + 仍可 `Attack`）来自死亡缝合不完整：`DefeatIfAvatarDead` 几乎只挂在 `DealDamage`；`ChangePhase(InteractionLoop)` 可在 `NodeStarted` Post 链致死之后覆盖 `Defeat`；合法指令只看相位不看 HP。Batch-ack 缓释只延迟战败**表演**，不是 Core 僵尸根因。

## 后果

- 新增/修改 HP 写入 Action 时须检查是否需 `DefeatIfAvatarDead` follow-up。
- `StartNode` 与开局效果链须在开发构建下满足：HP≤0 ⇒ `phase==Defeat`（诊断断言 / 探针）。
- 表现层仍禁止战中常态 `SyncFromCore`（ADR-0005）；Opening 白名单 `SyncFromCore` 不变。

## 相关

- [ADR-0005](0005-card-face-beat-commit.md) — Impact 提交 HP；战中 HUD 不经 SyncFromCore
- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — Batch-ack 与战败表演时机
- [ADR-0012](0012-enemy-action-phase-volley.md) — 齐射中玩家死亡进 Defeat
