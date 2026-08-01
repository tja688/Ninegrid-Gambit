---
status: accepted
---

# 触发可见因果与基础触发表现

## 决策

1. **触发可见因果**：效果后果上屏之前，其触发条件必须已在场上成为玩家可见事实。同批内核已算完 ≠ 可提前播触发。
2. **落地闸门**：产品直觉覆盖一切触发文案；本 ADR 硬约束的是**无命中帧、靠运动落地才成立**的触发（如旋转/位移后的 `OnSelfMove` / `OnMoveToSlot`）。交战命中帧已由 Impact / `onCombatHit` 承担，不另叠规则。
3. **基础触发表现**：凡经 Triggered 反应且 `ExecuteEffectAction` 成功 Apply 的**卡牌**效果，Core 必须留下 `EffectTriggered(OwnerUid)`；表现层在 Impact 时对**仍可视在场**的持有者播最小可见反馈（v1=缩放）。进阶定向特效另票。
4. **显式排除**：基础交战/反击/齐射裸伤；OnActivate、光环与 RuleModifier；遗物无持有者（`OwnerUid=0`）；亡语/自移除等持有者已离场——不强制缩放，反馈另案。

## 为什么

内核一批解算常含「先移位、再按新格位触发」。若表现在旋转/位移开始前冲刷 Impact，玩家会先看到持有者脉冲或扣血，落地后却无事发生——核没错，**观感因果倒置**，战场不可读。卡面数值锚点（ADR-0005）与翻牌门控（ADR-0016）已处理相邻问题；本决策把「字面触发条件 = 可见触发瞬间」与「有触发必有持有者最小反馈」立成独立不变量，避免 AI 把 Batch 指令序误当成可见时序，或只播后果不播触发。

## 考虑过的替代

- **无例外「凡产品触发必脉冲」**：否决——撞上交战裸伤、OnActivate/光环、遗物无持有者、离场亡语等真实路径。
- **扩写 ADR-0005 兼管产品因果**：否决——0005 管锚点提交；本决策是编排可读性，单独编号便于护栏与扩展。
- **v1 即配置落点/目标特效**：否决——先钉持有者基础缩放；定向特效另票统一处理。

## 后果

- `CONTEXT.md` 增补「触发可见因果」「基础触发表现」。
- 盘面 Drain 等无命中帧通道：Impact 须在运动落地之后（既有 ADR-0005 实现缝继续服从本不变量）。
- 新增 Triggered 卡牌效果须走 `ExecuteEffectAction`（或等价发 `EffectTriggered`），禁止只推后果指令。
- 结构/回归护栏与通道扫描可引用本 ADR；具体漏网修法另票。

## 相关

- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — 统一时间线 / Batch-ack
- [ADR-0005](0005-card-face-beat-commit.md) — Impact / Settled 锚点与运动后冲刷
- [ADR-0007](0007-unified-presentation-pipeline.md) — `EffectTriggerPulseBeatHandler` 等装饰消费
- [ADR-0016](0016-card-face-orientation.md) — 战斗通道延后翻牌（同类可读时序）
- CONTEXT「触发可见因果」「基础触发表现」
