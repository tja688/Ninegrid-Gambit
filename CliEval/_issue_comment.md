## 第五节观测缺口已补齐（日志打点落地）

**Core（诊断事件，不进表现批次）**
- 新增 `CoreEventType.EnemyActionResolved` + `EmitEnemyActionVerdictAction`（`PhaseActions.cs`；映射 `RequiresPlayback=false`）。
- `PhaseSystem` 敌方行动各裁决点发痕迹事件：
  - 报名端：非空名单发 `roster`（detail=uid 升序名单）；
  - 结算端：`fired`（detail 含 slot/avatar/sync）、`voidPosition` / `voidActionBanned`（**整窗作废及原因**，detail 含双方格位）、`skipFaceDown`（detail 含冻结剩余值）、`skipInvalid`、`abortAvatarDown`；均带 `patternFires` 与裁决时倒计时 `remaining`。

**Presentation（FlowTrace 旁路，全互动链覆盖）**
- 新增 `Flow/Diagnostics/RhythmFaceFlowTraceBinder`（`PresentationSceneRoot.WireHosts` 装载）：以 EventLog 游标扫描（`Evt_PresentationBatchOpened` 即时 + `Update` 兜底），覆盖交战/拾取/点空/翻开/敌方行动分拍全部链路，写入 CoreLog：
  - `Rhythm/CardFaceChanged`：uid、defId、faceUp 方向、动作（FlipCard/ConcealFace/RevealFace）、sourceDefId、cause —— 谁翻了谁、往哪翻、因为什么；
  - `Rhythm/ActionCountdownChanged`：uid、defId、delta、remaining —— 完整计数轨迹；
  - `Rhythm/RhythmFireOpened`：开火窗开启；
  - `Rhythm/EnemyActionVerdict`：上述 Core 裁决透传；
  - `CombatSummary/EffectTriggered` 改为**全链路**记录（原先只在交战意图链；`CoreBatchProjectionCoordinator` 交战路径只保留 `BaseStatModified`，避免双写）。
- 各事件均补 `defId` 字段，免去分析时 uid↔defId 反查。

**验证**：`unity command recompile` 完成，`recompile_status` `completed / failed=false / errors=[]`；Console 无本改动导致的新增 Error/Exception/Assert。

**文档**：`docs/code-map/presentation.md` 增 #206 诊断轨条目；`table-nine-battlelog-analysis` 技能补 Rhythm 轨字段说明。

下一步：正式打一局（最好复现紫蝎异常的打法），再按 Rhythm 轨对照第四节症状表定位根因。
