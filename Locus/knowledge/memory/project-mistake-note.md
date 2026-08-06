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
updatedAt: 1786013921836
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
- `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 专属的 DevTest 组件只要序列化进构建场景或进正式构建依赖的预制体，Release 就必现 Missing Script（类不编译）。教训（#126）：场景/预制体只序列化「始终编译的安装宿主」（`DevTestSceneInstaller` / `DevTestStandardCardInstaller`），`#if` 块内运行时 AddComponent；发现渠道是 Release 无头冒烟 + 结构测试扫描 GUID。
- `unity command run_tests` 同步命令有 30s CLI 连接超时，长套件要用 `--async_tests true` 后轮询 `test_status`；已中断的同步 run 会让 pipeline server 挂死（所有命令超时而 Editor 本体正常），只能重启 Editor（先 `ai-workspace gate-restart`）。教训：别用同步 run_tests 跑全量。
- MainScene 曾有 214 处既有 Missing Script：已删除脚本 `TableNineSortingKey`（GUID d1f6b4c5…，类在仓库中不存在，HEAD 即有 107 处 GUID 引用）。**#126 已全部清理（214→0）**：107 处 GUID 引用用 `RemoveMonoBehavioursWithMissingScript` 删；107 处 `m_Script:{fileID:1115186359}` 无 GUID 破坏引用标准 API 删不掉（`DeleteArrayElementAtIndex` 拒绝 null 元素），需用 SerializedObject 访问 `objectReferenceInstanceIDValue` 后 ApplyModifiedProperties 让 Unity 自行丢弃；另有 1 孤儿文档随保存清理。护栏 `BuildSceneMissingScriptStructuralTests` 防回流。
- 多选道具卡（如 `help.swap_card`）第二次提交发生在 BoardSelect 门禁仍持有输入所有权期间；`SubmitUseItemIntentCommand` 若固定以 `ProtectedField` 提交，会被 IntentIntake 以 `ownerMismatch owner=BoardSelect target=ProtectedField` 拒绝并触发道具回手。完成提交必须使用 `BoardSelect` target surface，且不要为规避冲突提前释放门禁；回归测试应覆盖“初次无目标仍 RouteToBoardSelect + 完成后实际执行 Swap”。
<!-- locus:body:end -->
