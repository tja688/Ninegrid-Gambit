---
status: accepted
---

# 牌面朝向权威与背面惰性

## 决策

1. **朝向权威在 Core**：`CardInstance.FaceUp`（默认正面）。表现层只 Commit 镜像并经 `CardFaceFlipPresenter` 播翻；禁止 DisplayMode / L4 本地态当规则真相。
2. **`[翻面]` = toggle**：`FlipCardAction` 切换朝向，发 `CoreEventType.CardFaceChanged`（`ResultValue`=0/1），Post 触发 `TriggerPoint.OnFlip`（`[翻面后]`）。
3. **主动翻开**：交战中、场上、正交相邻的背面卡，玩家可消耗一次互动仅做背面→正面（`RevealFace` / `RevealFaceAction`），同样走 `CardFaceChanged` + `OnFlip`。
4. **背面惰性（双向）**：
   - 背面卡**不可被伤害**（玩家 `Attack` / 交战命中、爆弹等 `AllMonsters` / `DealDamage`、范围选目标均拒或跳过）。
   - 背面怪**不参与敌方行动**：报名不 −1、不入开火名单；结算时若已背面则不开火且**不 Reset** 倒计时。
   - 该卡被动（Triggered / Modifier / RuleModifier）默认不生效。遗物（无 owner 实体）不走卡牌 FaceUp 门闩。
5. **攻击倒计时冻结**：背面期间 `AttackPatternCountdown` 保持翻面前数值，不随敌方报名推进。
6. **独立背面 Tick**：需要「背面仍计回合」的效果显式 `FaceDownTickCounters.Register`；键前缀 `faceDownTick.*`，与 `attackPattern.*` / `effect.*` 隔离。仅背面卡在敌方行动报名时 −1；查询接口供未来右键详情（本决策不规定 UI）。
7. **自管翻面豁免**：效果 `requires` 含 `ActiveWhileFaceDown` 时，不受背面**被动**惰性影响（可在背面触发/保持修饰）。不自动恢复敌方开火或受击。
8. **编排**：`CardFaceChanged` → `PresentationInstructionKind.UpdateFaceUp` → Beat=`Settled` → `CardFaceFlipBeatHandler` Commit + 经 `FlipPlaybackCoordinator` 串行播翻。
9. **翻牌时序门控**：`PresentStep` 默认在 `IPresentChannel.Begin`（hop / 移位等）之前先 `FlushUpdateFaceUp` 并等到 `FlipPlaybackCoordinator.IsIdle`；通道完成、`FlushBeats` 后再等 Idle，才 `TryAcknowledge`。同批多张翻牌全局串行，禁止 Handler 对 `PlayFlipAsync` 直接 `.Forget()`。**战斗通道例外**：攻击 / 反击 Present（`flushFaceUpBeforeBegin: false`）不提前刷当批 FaceUp，把 `UpdateFaceUp` 留到通道后的 `FlushBeats`（命中 Impact 之后），避免「先翻牌再扑向玩家」误解伤人触发技（如 `skill.rise_up`）。
10. **非锁步同刷**：`PresentEventLogSlice` 仍同步；需等播完的调用方（如 Alpha5）自行 `WaitIdleAsync`。

## 为什么

设计案将翻面定义为技能系统事件（`[翻面]` / `[翻面后]`），且 CONTEXT / ADR-0002 已否决 DisplayMode 推导朝向。背面是双向屏蔽：既不能打它，它也不能打你；攻击倒计时冻结后，「N 回合后翻回」等需求不能复用 `AttackPatternCountdown`，故另开 `faceDownTick.*`。

翻牌若在 Settled 上 fire-and-forget，会与 `PresentStep` 的 hop 通道抢时间线，且同帧多翻被 Presenter `_playing` 丢弃；故 hop / 移位通道「先翻再跳」。战斗通道若同样提前刷 FaceUp，会把同批的伤人触发翻面播在扑击动画之前，与「打到玩家后才翻」的可读时序冲突，故战斗 Present 延后到通道后 FlushBeats。

## 考虑过的替代

- **仅表现层 VisualFaceUp**：否决——无法参与攻击/被动门闩与效果触发。
- **仅禁玩家攻背面、背面怪仍可开火**：否决——与产品「翻面双向惰性」冲突（早期落地曾误读为此）。
- **背面仍推进 AttackPatternCountdown / 复用其做翻回计时**：否决——与冻结语义冲突；翻牌计时应独立注册。
- **无豁免 token、按触发点启发式跳过**：否决——自管翻面技能须显式声明，避免隐式耦合。
- **翻牌与 hop 并行 / FlushBeats 后再 Forget 播翻**：否决——与「先翻再跳」产品时序冲突，且不占 ack。
- **把 UpdateFaceUp 挪到通道 Begin 前改 Beat**：否决——仍保留 Settled 归属；由 PresentStep 提前只刷 FaceUp Kind。
- **战斗通道也一律通道前先翻**：否决——伤人触发 Flip 会抢在反击 / 敌方开火动画之前，误导玩家。

## 后果

- 效果原子入口：`OnFlip` / `Flip` / `IsFaceUp`；内容技能另票落地。
- IntentIntake 增 `revealFace`；点邻接背面怪分流至翻开而非攻击。
- `DealDamageAction` 与 `TargetResolver.MonstersOnBoard` / `FilteredCards` 跳过背面；敌方 `Register`/`Resolve` 读 `FaceUp`。
- `FaceDownTickCounters` 为查询与 Tick 入口；右键详情 UI 后做。
- 结构护栏：`CommitPresentation` 白名单含 `CardFaceFlipBeatHandler`；`PresentationEventMap` 穷尽含 `CardFaceChanged`；Handler 禁直接 `.Forget()` `PlayFlipAsync`；`PresentStep` FaceUp 刷 / Idle 门控在通道 Begin 与 ack 两侧；战斗 Present 以 `flushFaceUpBeforeBegin: false` 延后当批 FaceUp。

## 相关

- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — Batch-ack；翻牌播完才就位
- [ADR-0002](0002-card-chassis-and-face-templates.md) — 朝向权威在 Core
- [ADR-0004](0004-input-intake-two-axis-gating.md) — RevealFace 经 IntentIntake
- [ADR-0005](0005-card-face-beat-commit.md) — Settled Commit
- [ADR-0007](0007-unified-presentation-pipeline.md) — 统一冲刷；翻牌占主线就位、装饰不占 ack
- [ADR-0012](0012-enemy-action-phase-volley.md) — 敌方行动阶段（背面不参与报名）
- CONTEXT「牌面朝向」
