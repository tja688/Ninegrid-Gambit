---
id: kd_builtin_memory_project_mistake_note
type: memory
path: project-mistake-note.md
title: project-mistake-note
injectMode: full
summaryEnabled: false
commandEnabled: false
readOnly: false
aiMaintained: true
explicitMaintenanceRules: true
createdAt: 1784026817107
updatedAt: 1785502480457
---

# project-mistake-note

<!-- locus:maintain-rules:start -->
- Record only verified problems, rework causes, and avoidance steps
- Prioritize recurring pitfalls, constraints, regression points, and confirmed fixes
- Keep each entry short and focused on one lesson or constraint
- Keep the list within 20 items and merge duplicates regularly
- Remove outdated issues, non-reproducible issues, and unsupported guesses
<!-- locus:maintain-rules:end -->

<!-- locus:body:start -->
- 神圣决斗（skill.holy_duel）的 2 伤惩罚若用裸 `DealDamageAction` 入队，EventLog 不会发 `EffectTriggered` → 持有者不播效果触发脉冲（缩放）。惩罚必须走 `ExecuteEffectAction(instanceId, null, new[]{ DealDamageAction(...) })` 包装（PhaseSystem.ApplyHolyDuelMark）。教训：硬编码在 System 里的技能直伤，要主动补 EffectTriggered 事件，表现层脉冲靠它驱动。
- 效果触发脉冲链路：Core `EffectTriggered` 事件 → PresentationEventMap `TriggerEffect` → `EffectTriggerPulseBeatHandler` → `CardEffectTriggerPulseSink` → `PlayEffectTriggerPulse`。Core 侧只入队 DealDamage 而不发 EffectTriggered 时，战斗日志里看不到任何脉冲依据。
<!-- locus:body:end -->
