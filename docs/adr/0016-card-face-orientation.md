---
status: accepted
---

# 牌面朝向权威与背面惰性

## 决策

1. **朝向权威在 Core**：`CardInstance.FaceUp`（默认正面）。表现层只 Commit 镜像并经 `CardFaceFlipPresenter` 播翻；禁止 DisplayMode / L4 本地态当规则真相。
2. **`[翻面]` = toggle**：`FlipCardAction` 切换朝向，发 `CoreEventType.CardFaceChanged`（`ResultValue`=0/1），Post 触发 `TriggerPoint.OnFlip`（`[翻面后]`）。
3. **主动翻开**：交战中、场上、正交相邻的背面卡，玩家可消耗一次互动仅做背面→正面（`RevealFace` / `RevealFaceAction`），同样走 `CardFaceChanged` + `OnFlip`。
4. **背面惰性**：背面卡不可被玩家攻击；该卡被动（Triggered / Modifier / RuleModifier）默认不生效。遗物（无 owner 实体）不走卡牌 FaceUp 门闩。
5. **自管翻面豁免**：效果 `requires` 含 `ActiveWhileFaceDown` 时，不受背面惰性影响（可在背面触发/保持修饰）。
6. **编排**：`CardFaceChanged` → `PresentationInstructionKind.UpdateFaceUp` → Beat=`Settled` → `CardFaceFlipBeatHandler` Commit + 经 `FlipPlaybackCoordinator` 串行播翻。
7. **翻牌时序门控**：`PresentStep` 在 `IPresentChannel.Begin`（hop / 移位等）之前先 `FlushUpdateFaceUp` 并等到 `FlipPlaybackCoordinator.IsIdle`；通道完成、`FlushBeats` 后再等 Idle，才 `TryAcknowledge`。同批多张翻牌全局串行，禁止 Handler 对 `PlayFlipAsync` 直接 `.Forget()`。
8. **非锁步同刷**：`PresentEventLogSlice` 仍同步；需等播完的调用方（如 Alpha5）自行 `WaitIdleAsync`。

## 为什么

设计案将翻面定义为技能系统事件（`[翻面]` / `[翻面后]`），且 CONTEXT / ADR-0002 已否决 DisplayMode 推导朝向。先落地 Core 权威、事件链与门闩，再挂具体翻面军团技能，避免动画 POC 与规则分叉。

翻牌若在 Settled 上 fire-and-forget，会与 `PresentStep` 的 hop 通道抢时间线，且同帧多翻被 Presenter `_playing` 丢弃；故翻牌占主线就位，通道前先翻。

## 考虑过的替代

- **仅表现层 VisualFaceUp**：否决——无法参与攻击/被动门闩与效果触发。
- **背面仍可攻击**：否决——与本轮产品规则冲突。
- **无豁免 token、按触发点启发式跳过**：否决——自管翻面技能须显式声明，避免隐式耦合。
- **翻牌与 hop 并行 / FlushBeats 后再 Forget 播翻**：否决——与「先翻再跳」产品时序冲突，且不占 ack。
- **把 UpdateFaceUp 挪到通道 Begin 前改 Beat**：否决——仍保留 Settled 归属；由 PresentStep 提前只刷 FaceUp Kind。

## 后果

- 效果原子入口：`OnFlip` / `Flip` / `IsFaceUp`；内容技能另票落地。
- IntentIntake 增 `revealFace`；点邻接背面怪分流至翻开而非攻击。
- 结构护栏：`CommitPresentation` 白名单含 `CardFaceFlipBeatHandler`；`PresentationEventMap` 穷尽含 `CardFaceChanged`；Handler 禁直接 `.Forget()` `PlayFlipAsync`；`PresentStep` FaceUp 刷 / Idle 门控在通道 Begin 与 ack 两侧。

## 相关

- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — Batch-ack；翻牌播完才就位
- [ADR-0002](0002-card-chassis-and-face-templates.md) — 朝向权威在 Core
- [ADR-0004](0004-input-intake-two-axis-gating.md) — RevealFace 经 IntentIntake
- [ADR-0005](0005-card-face-beat-commit.md) — Settled Commit
- [ADR-0007](0007-unified-presentation-pipeline.md) — 统一冲刷；翻牌占主线就位、装饰不占 ack
- CONTEXT「牌面朝向」
