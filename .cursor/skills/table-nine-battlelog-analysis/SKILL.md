---
name: table-nine-battlelog-analysis
description: >-
  Analyze TableNine BattleTrace JSON against Assets/Docs design and Core
  implementation; triage Core vs presentation; retest with EditMode harness.
  Use when the user asks to 分析战斗日志/BattleLog/BattleTrace、一反击就死、
  对照设计文档查战斗问题、或根据日志复测/改接线.
---

# TableNine 战斗日志分析

精炼流程：读日志 → 对照 Docs + Core → 分层定性 →（有疑点）EditMode 复测 → 按用户意图汇报或改代码。

## 何时用

- 用户丢了 BattleLog / Trace JSON，或说「看最新战斗日志」
- 「一反击就死 / 伤害不对 / 该不该旋转」等战斗合理性问题
- 要区分 **Core 错了** vs **表现接线错了**

## 日志从哪来

1. 用户显式给了路径 / 粘贴 → 用这份  
2. 否则取 **最新**：`Assets/Notes/BattleLog/battlelog-*.json`（按修改时间）  
3. Play 结束自动导出；DevTest Keypad4 可立即导出（`InBattleManager` 层）

Schema 要点（每条 Op）：

| 字段 | 用途 |
|------|------|
| `reason` | `PlayerAttack` / `CounterAttack` / … |
| `attacker`/`target` | 打前 snap：atk/hp/armor/defId |
| `events` | 门内 EventLog：`DamageDealt` amount/delta/remainingHp |
| `presentation` | `targetKilled` / `avatarDefeated` |
| `phaseBefore`→`phaseAfter` | 是否进 `Defeat` |

## 分析流水线（按序，勿跳）

```
1 读日志末 1～3 条 CombatHit Op，还原「谁→谁、打前数值、伤害、是否死」
2 对照 Docs（见下表）写出「设计期望」
3 CodeGraph / 读 PhaseSystem.ApplyCombatHit、DealDamage、Attack 路径，写出「Core 实际」
4 定性：Docs↔Core 一致？日志是否符合 Core？
5 有 Core 疑点 → EditMode 复测（见 references/test-harness.md）
6 Core 绿且日志符合 Core → 再查表现接线（FieldBattle / CombatHitSink reason / 动画命中帧）
7 按用户意图收束：
   - 只要分析 → 出简报，不改代码
   - 要求改 → 先改对应层（Core 须明确要求；默认不随手改 Core）
```

### Docs 优先读

| 问题类型 | 文档 |
|----------|------|
| 先手/反击/击杀不反击 | `Assets/Docs/九宫牌局/01-机制规则/战斗机制.md`、`…/先攻.md` |
| 伤害/护甲 | `…/伤害公式.md`；元设计 `…/RUL_战斗.md`（RUL_005） |
| 旋转/补牌 | `战斗机制.md`；`RUL_战斗.md`（RUL_006）；契约测 R1 |
| 元规则总览 | `Assets/Docs/九宫牌局-元设计/02-规则层-RUL/RUL_战斗.md` |

### Core 优先看

- 门禁：`PhaseSystem.ApplyCombatHit`（单段伤害，无内建反击）
- 整段：`PhaseSystem.Attack`（可能含先手/反击语义）
- 伤害：`DealDamageAction` → Avatar 致死 `DefeatIfAvatarDead`
- 表现分段：`FieldBattleManager` 设 `PendingTraceReason` → `CombatHitSink` → `ApplyCombatHitFromCore`

**关键认知**：表现层「玩家攻 + 未杀则反击」= **两次** `ApplyCombatHit`；日志里应看到 `PlayerAttack` 后接 `CounterAttack`。不要用「一次 Attack 命令」的心智硬套分段 Trace。

## 定性口诀

| 现象 | 先怀疑 |
|------|--------|
| 数值/相位与 Docs 不符，EditMode 同 seed 复现 | **Core** |
| EditMode 符合 Docs，Play 日志/手感不对 | **表现接线**（reason、命中帧、PostKill 缓释） |
| Docs 本身歧义 / 未落地 | **设计缺口**，汇报勿当 bug 乱改 |

硬约束（与项目规则一致）：

- 非明确要求 **不改 Core**；打点/分析失败不得改结算语义「顺手修死亡」
- 不手改 `.unity`

## 用户意图分流

| 用户说 | 做 |
|--------|----|
| 分析 / 合不合理 / 汇报 | 只出简报（模板见下） |
| 修 / 改 / 落地 | 按定性改对应层，改后可再跑 EditMode / 再打一局导出日志 |
| 复测 | 只跑/补测试，不扩 scope |

## 汇报模板（分析模式）

```markdown
## 战斗日志结论
- 日志：`Assets/Notes/BattleLog/...`（seed / sessionId）
- 关键 Op：#n reason=… → …（一句话）

## 设计期望（Docs）
- …

## Core 实际
- …

## 判定
- [ ] Core 问题 / [ ] 表现接线 / [ ] 设计歧义 / [ ] 合理无问题

## 证据
- DamageDealt amount/remainingHp；phaseAfter；必要时 EditMode 结果

## 建议下一步
- （分析结束停；或：补回归测 / 查 Field 命中帧 / 等用户授权改 Core）
```

## 参考

- EditMode 复测与样板：[`references/test-harness.md`](references/test-harness.md)
- Trace 产出：`Assets/Scripts/Flow/Diagnostics/BattleTraceRecorder.cs`
- 死亡回归样板：`CombatHitDeathRegressionTests`
