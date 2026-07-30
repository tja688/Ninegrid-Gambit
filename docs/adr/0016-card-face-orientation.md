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
6. **编排**：`CardFaceChanged` → `PresentationInstructionKind.UpdateFaceUp` → Beat=`Settled` → `CardFaceFlipBeatHandler` Commit + Flip 动画。

## 为什么

设计案将翻面定义为技能系统事件（`[翻面]` / `[翻面后]`），且 CONTEXT / ADR-0002 已否决 DisplayMode 推导朝向。先落地 Core 权威、事件链与门闩，再挂具体翻面军团技能，避免动画 POC 与规则分叉。

## 考虑过的替代

- **仅表现层 VisualFaceUp**：否决——无法参与攻击/被动门闩与效果触发。
- **背面仍可攻击**：否决——与本轮产品规则冲突。
- **无豁免 token、按触发点启发式跳过**：否决——自管翻面技能须显式声明，避免隐式耦合。

## 后果

- 效果原子入口：`OnFlip` / `Flip` / `IsFaceUp`；内容技能另票落地。
- IntentIntake 增 `revealFace`；点邻接背面怪分流至翻开而非攻击。
- 结构护栏：`CommitPresentation` 白名单含 `CardFaceFlipBeatHandler`；`PresentationEventMap` 穷尽含 `CardFaceChanged`。

## 相关

- [ADR-0002](0002-card-chassis-and-face-templates.md) — 朝向权威在 Core
- [ADR-0004](0004-input-intake-two-axis-gating.md) — RevealFace 经 IntentIntake
- [ADR-0005](0005-card-face-beat-commit.md) — Settled Commit
- CONTEXT「牌面朝向」
