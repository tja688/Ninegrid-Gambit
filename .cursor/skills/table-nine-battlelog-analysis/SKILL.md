---
name: table-nine-battlelog-analysis
description: >-
  Analyze TableNine BattleTrace / FlowTrace JSON against design and Core.
  Combat mode: damage/design numeric triangulation via BattleLog.
  Flow mode: reconstruct full run timeline via FlowLog (skipped reward, room bugs).
  Use when the user asks to 分析战斗日志/BattleLog/BattleTrace、流程日志/FlowLog/FlowTrace、
  一反击就死、跳过奖励、房间卡住、全流程还原、对照设计文档/数值表查战斗问题、
  或根据日志复测/改接线.
---

# TableNine 战斗日志 / 流程日志分析

双模式：先按用户意图分流，再走对应流水线。

| 用户意图 | 模式 | 读什么 |
|----------|------|--------|
| 伤害/反击/数值对不对、设计对得上吗 | **战斗分析** | `Assets/Notes/BattleLog/battlelog-*.json` |
| 跳过环节/房间 bug/点了啥/全流程还原 | **流程溯源** | `Assets/Notes/FlowLog/flowlog-*.json`；需伤害细节时用同 `sessionId` 打开 BattleLog |

---

## 何时用

- 用户丢了 BattleLog / FlowLog / Trace JSON，或说「看最新战斗/流程日志」
- 「一反击就死 / 伤害不对」→ 战斗分析
- 「跳过了奖励 / 房间卡住 / 缺了某步」→ 流程溯源
- 要区分 **Core 错了** vs **表现接线错了** vs **数值/表漂移** vs **流程壳漏步**

## 日志从哪来

1. 用户显式给了路径 / 粘贴 → 用这份
2. 否则取 **最新**：
   - 战斗：`Assets/Notes/BattleLog/battlelog-*.json`
   - 流程：`Assets/Notes/FlowLog/flowlog-*.json`
   （按修改时间）
3. Play 结束自动导出两边（同 `sessionId`/`seed`）；DevTest Keypad4 立即导出 Battle+Flow

两边通过相同 `sessionId` 互指；Flow 的 `CombatHitSummary.refBattleOpIndex` 指向同局 BattleLog `opIndex`。

---

# 模式 A：战斗分析

精炼流程：读日志 → **数值三角对照** → 对照 Docs 规则 + Core → 分层定性 →（有疑点）EditMode 复测 → 按用户意图汇报或改代码。

Schema 要点（每条 Op）：

| 字段 | 用途 |
|------|------|
| `reason` | `PlayerAttack` / `CounterAttack` / … |
| `attacker`/`target` | 打前 snap：atk/hp/armor/**defId**（有效属性） |
| `events` | 门内 EventLog：`DamageDealt` amount/delta/remainingHp |
| `presentation` | `targetKilled` / `avatarDefeated` |
| `phaseBefore`→`phaseAfter` | 是否进 `Defeat` |

## 分析流水线（按序，勿跳）

```
1 读日志末 1～3 条 CombatHit Op，还原「谁→谁、打前有效数值、伤害、是否死」
2 数值维度（必做）：Docs 意图 ↔ Luban/Catalog 基础 ↔ Trace 有效值 + DamageDealt
   → 详见 references/numeric-tables.md
3 对照 Docs 规则（先手/反击/旋转）写出「机制期望」
4 CodeGraph / 读 ApplyCombatHit、DealDamage、Attack，写出「Core 实际」
5 定性：数值？机制 Docs↔Core？日志是否符合 Core？
6 有 Core/公式疑点 → EditMode 复测（references/test-harness.md）
7 Core 绿且数值自洽 → 再查表现接线（FieldBattle / reason / 命中帧）
8 按用户意图收束：分析只汇报；要求改才动对应层（默认不改 Core）
```

### 数值维度（摘要）

战斗离不开设定数值。每条相关 Op 至少回答：

1. **表基础**：该 `defId` 在 Luban/`cards.json`（或职业表）的 ATK/HP/Armor
2. **有效值**：Trace snap 是否被修正抬高/压低（差要能用遗物/技能/光环解释）
3. **公式**：`伤害公式.md` → 期望扣甲/扣血 vs `DamageDealt`
4. **漂移**：Docs≠表、表≠有效基础、有效 ATK≠伤害 → 打标签（见 numeric-tables）

| defId 类 | Docs | 真实数 |
|----------|------|--------|
| `monster.*` | `Assets/Docs/九宫牌局/07-数据/怪物卡数据.md` | `Assets/Tools/Luban/Datas/cards.json` → StreamingAssets `tablenine_tbcard.json` |
| `help.*` | `…/帮助卡数据.md` | 卡面常 0；直伤看 `effects.json` |
| `avatar.default` | `…/05-职业与层级/职业.md` | **`ProfessionCatalog.cs`**（不在 TbCard） |
| 公式 | `…/01-机制规则/伤害公式.md`、`属性.md` | Core `DealDamageAction` |

完整路径与 rg 示例：[`references/numeric-tables.md`](references/numeric-tables.md)。

### Docs 规则优先读

| 问题类型 | 文档 |
|----------|------|
| 先手/反击/击杀不反击 | `Assets/Docs/九宫牌局/01-机制规则/战斗机制.md`、`…/先攻.md` |
| 伤害/护甲/有效属性 | `…/伤害公式.md`、`属性.md`；`RUL_战斗.md` RUL_005 |
| 旋转/补牌 | `战斗机制.md`；`RUL_战斗.md` RUL_006；契约测 R1 |
| 单位/卡意图数值 | `Assets/Docs/九宫牌局/07-数据/*` |

### Core 优先看

- 门禁：`PhaseSystem.ApplyCombatHit`（单段伤害，无内建反击）
- 整段：`PhaseSystem.Attack`（可能含先手/反击语义）
- 伤害：`DealDamageAction` → Avatar 致死 `DefeatIfAvatarDead`
- 表现分段：`FieldBattleManager` → `PendingTraceReason` → `ApplyCombatHitFromCore`
- 灌数：`ContentSystem` / `TableNineLubanCatalogFactory`；化身例外 `ProfessionCatalog`

**关键认知**：表现「玩家攻 + 未杀则反击」= **两次** `ApplyCombatHit`（`PlayerAttack`→`CounterAttack`）。勿用单次 `Attack` 心智硬套分段 Trace。

## 定性口诀（战斗）

| 现象 | 先怀疑 |
|------|--------|
| Docs/表基础与 Trace 有效值或 DamageDealt 对不上 | **数值漂移**（docs≠luban / 修正未解释 / cheat） |
| 数值自洽，机制与 Docs 不符且 EditMode 复现 | **Core** |
| EditMode+表都对，Play 手感不对 | **表现接线** |
| Docs 歧义或表未同步 | **设计/落地缺口**，勿当结算 bug 乱改 |

硬约束：非明确要求不改 Core；不手改 `.unity`；不「顺手改公式」让某一局 Trace 好看。

## 汇报模板（战斗分析）

```markdown
## 战斗日志结论
- 日志：`Assets/Notes/BattleLog/...`（seed / sessionId）
- 关键 Op：#n reason=… → …（一句话）

## 数值对照
- defId：Docs意图 = … / Luban基础 = … / Trace有效 = …
- 期望伤害（公式）= …；DamageDealt amount/delta/remainingHp = …
- 漂移标签：（无 / docs≠luban / formula_mismatch / …）

## 机制期望（Docs）
- …

## Core 实际
- …

## 判定
- [ ] 数值问题 / [ ] Core / [ ] 表现接线 / [ ] 设计歧义 / [ ] 合理无问题

## 证据
- …

## 建议下一步
- …
```

---

# 模式 B：流程溯源

精炼流程：读 FlowLog → 按 `events` 还原时间线 → 标缺失/异常 name → 对照挂点 → 定性壳 vs Core 门拒。

Schema 要点（每条 Event）：

| 字段 | 用途 |
|------|------|
| `category` | `Loop` / `UI` / `CoreGate` / `CombatSummary` / `Deck` |
| `name` | 稳定短名，见 [`references/flow-events.md`](references/flow-events.md) |
| `loopState` | 当时 `MainGameLoop.LoopState` |
| `phaseBefore`→`phaseAfter` | Core 门相位 |
| `accepted` | 门禁是否接受 |
| `refBattleOpIndex` | 战斗摘要 → 同 session BattleLog |
| `payload` | 扁平字段（选项、defId、deckEmpty…） |

## 溯源流水线（按序）

```
1 读最新 flowlog（或用户指定），记下 sessionId/seed
2 按 events[].index 列出时间线：SetState / StartRun / StartNode / CombatHitSummary / Reward* / Room* / Victory|Defeat
3 对照期望路径（主菜单→战斗→结算→奖励→房间→…）标出「该出现却缺失」或 accepted=false
4 若缺 RewardPresented：查 MainGameLoop.PlayRewardChoiceAsync 跳过条件 + Core phase/pending
5 若 RoomChosen/EnterRoom 被拒：读 payload.reason + PhaseSystem
6 需伤害细节：用同 sessionId 打开 battlelog，按 refBattleOpIndex 跳转
7 定性：流程壳漏步 / Core 门拒 / 选择 UI 未完成 / 合理（设计上跳过）
8 按用户意图汇报；要求改才动对应层（默认不改 Core）
```

### 常见缺步对照

| 现象 | 先查 |
|------|------|
| 通关后无 `RewardPresented` 或 `skipped=true` | phase≠RewardItemChoice / pending 空 |
| 有 `RewardPresented` 无 `RewardChosen` | 选择器未完成 / 提前取消 |
| 无 `RoomPresented` 或 skipped | phase≠RoomChoice / 选项不足 2 |
| `EnterRoom` accepted=false | payload.reason |
| `PostKillBoard` deckEmpty=true 后异常 | 发牌/清场逻辑 |
| 有战斗无 `CombatHitSummary` | FlowTrace 未启用 / 门禁未走桥 |

## 汇报模板（流程溯源）

```markdown
## 流程溯源结论
- 日志：`Assets/Notes/FlowLog/...`（seed / sessionId）
- 同局 BattleLog：`Assets/Notes/BattleLog/battlelog-{sessionId}-seed{seed}.json`（若有）

## 时间线（关键）
- #i SetState A→B
- #j StartNode / CombatHitSummary …
- #k RewardPresented / RewardChosen …
- （标出缺失或 accepted=false）

## 异常
- …

## 判定
- [ ] 流程壳漏步 / [ ] Core 门拒 / [ ] UI 选择未完成 / [ ] 设计上跳过 / [ ] 合理无问题

## 建议下一步
- …
```

---

## 用户意图分流

| 用户说 | 做 |
|--------|----|
| 分析 / 合不合理 / 汇报 | 只出简报（选对模式） |
| 修 / 改 / 落地 | 按定性改层；改数走 Catalog→Luban sync（effect-landing） |
| 复测 | 只跑/补测试 |

## 参考

- 流程事件名与挂点：[`references/flow-events.md`](references/flow-events.md)
- 数值查表：[`references/numeric-tables.md`](references/numeric-tables.md)
- EditMode 复测：[`references/test-harness.md`](references/test-harness.md)
- Luban 同步流程：`.cursor/skills/table-nine-effect-landing/`
- Trace 产出：`Assets/Scripts/Flow/Diagnostics/BattleTraceRecorder.cs`、`FlowTraceRecorder.cs`
