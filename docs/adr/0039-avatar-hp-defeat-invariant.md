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
   **表演缓释（2026-08-19 补遗）**：Core 进入 `Defeat` / HP≤0 仍是即时规则事实。`EnsureBattleEndedIfAvatarDefeated` 只**武装**收口；`RaiseBattleEnded`（`HardClearIntents` + 死亡面板）必须等当前导演主线跑空，并先等到血条归零缓动与 Avatar 死亡退场。提前 Raise 会清掉后续 Present（机关位移、效果打击、飘字），观感就是「还没被打到就弹出死亡面板」。主线已空或 Present 被跳过时仍立即 Flush，避免 0 血僵尸局。

6. **判死谓词唯一（2026-08-11 补遗）**：合法指令裁决（`PhaseSystem.IsAvatarDefeated`）与战败收束（`DefeatIfAvatarDeadAction`）必须共用同一谓词——`AvatarDefeatFollowUp.IsAvatarDefeated`：`AvatarUid≤0` ∨ 注册表查无 ∨ 一手 HP≤0。**不看 Zone**。任何令合法指令收缩为「仅回收/丢弃」僵尸集的状态，都必须能被 `DefeatIfAvatarDeadAction` 收束成 `Defeat`，禁止出现「裁决判死、收束不认」的永久软锁。

7. **Avatar zone 自愈（同批补遗）**：Avatar 在册且 HP>0 但 `Zone != Avatar`（被 `Removed`/`Graveyard` 等异常置死）属**非法状态**：不参与判死（见第 6 条），`StartNode` 战斗路径检测到即经 `MoveAvatarAction→SetAvatar` 归位修复；表现探针 `[AvatarDefeatProbe]` 在 StartNode 后仍见非法 zone 时 `LogError` 归因。

## 为什么

0 血僵尸局（`hp=0` + `InteractionLoop` + 仍可 `Attack`）来自死亡缝合不完整：`DefeatIfAvatarDead` 几乎只挂在 `DealDamage`；`ChangePhase(InteractionLoop)` 可在 `NodeStarted` Post 链致死之后覆盖 `Defeat`；合法指令只看相位不看 HP。Batch-ack 缓释只延迟战败**表演**，不是 Core 僵尸根因。

**第 6/7 条的动机（2026-08-11）**：线上复现过反向僵尸——进战斗后 `InteractionLoop + pendingChoice=None` 下攻击/用牌/点空格全部 `notLegal` 拒绝、拾取（免门禁可信路径）与卖卡仍可用、战败永不触发。根因是两个判死谓词不一致：旧 `PhaseSystem.IsAvatarDefeated` 复用 `IsCardAlive`（含 Zone 判定），而 `DefeatIfAvatarDeadAction` 只读 HP 且对「注册表查无」静默 no-op；Avatar zone 被异常置死（HP>0）即落入「裁决判死、收束不认」的缝隙，成为跨节点永久软锁（进房硬切格 5 会跳过 `StartNode` 原有的 `MoveAvatarAction` 归位，使 zone 非法状态得以残留）。回归见 `AvatarVitalityInvariantTests`。

## 后果

- 新增/修改 HP 写入 Action 时须检查是否需 `DefeatIfAvatarDead` follow-up。
- `StartNode` 与开局效果链须在开发构建下满足：HP≤0 ⇒ `phase==Defeat`（诊断断言 / 探针）。
- 表现层仍禁止战中常态 `SyncFromCore`（ADR-0005）；Opening 白名单 `SyncFromCore` 不变。

## 相关

- [ADR-0005](0005-card-face-beat-commit.md) — Impact 提交 HP；战中 HUD 不经 SyncFromCore
- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — Batch-ack 与战败表演时机
- [ADR-0012](0012-enemy-action-phase-volley.md) — 齐射中玩家死亡进 Defeat
