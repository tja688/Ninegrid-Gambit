---
status: accepted
---

# 效果打击统一表演：场上卡的伤害/破坏走串行「攻击动作 + 受击反馈」，交战表演全局两倍速

## 决策

**凡「在场地格子上的卡」对其他卡（含 Avatar）造成伤害或移除（破坏），一律以既有普通攻击交战的「攻击动作 + 受击反馈」rig 呈现——多个目标串行逐个打；同时把攻击/受击表演整体提速两倍（时长减半），触发节拍随之减半。**

1. **归因不改 Core。** 效果 DSL 产出的伤害/移除事件本就携带 `SourceDefId`（效果实例 defId）；同批更早的 `EffectTriggered`（`Message == SourceDefId`）的 `CardUid` 即效果持有卡 = 打击者。归因、建组、编排全部在表现层完成（`Flow/Presentation/EffectStrikePlan`）。交战/单向打击（`SourceDefId` 为空）不入组，维持既有命中帧编排；`skill.holy_duel` 保持 ADR-0048 隔离区专用编排；打击者不是当前占格场上卡（如手牌道具「炸弹」）不入组，维持原飘字冲刷。
2. **打击暂扣区（strike hold）。** `BattleBeatScheduler` 新增与神圣决斗隔离区并列的第三条泳道 `mStrikeHeld`：开批时（`EffectStrikeChoreographer.OnBatchOpened`，经 `Evt_PresentationBatchOpened`）把打击计划内的 Impact 伤害指令（`ShowDamage` / `UpdateHp` / `UpdateArmor`，均为负向变化）移入暂扣区。暂扣指令**不被**命中帧 `FlushImpactExcept`、Drain「首个 Remove 前」锚点、全量 `ReportBeat(Impact)`（决斗惩罚帧）提前冲刷；只由打击组命中帧 `FlushStrikeHeldWhere(本组谓词)` 放行，`ReportBeat(Settled)` 前兜底全放（指令永不丢失）。
3. **串行打击编排。** 三个消费点，每组一次「打击者相对冲刺 → 命中帧（冲刷本组飘字/血甲 + 受击闪白/击退）→ 回位」：
   - **Drain 退场呈现前**（`BoardPresentationPlayer.PresentSkillRemovedCardsAsync` / `BattleSessionExecutor.BeginUseItemLethalVictimsAsync` / 攻击 Present 主目标卸尸前）：先串行打完**涉及该卡**的打击组——它作为受击者（滚石破坏卡片：先被撞击再碎裂）或打击者（捕熊陷阱：先打完再自毁）；
   - **PresentStep 两相之间**：通道运动全部落地 → 触发脉冲（第一相）→ 节拍 → **串行播剩余打击组** → 其余 Impact + Settled（第二相）。批内可见顺序更新为：**运动停稳 → 触发脉冲 → 效果打击（逐个）→ 其余飘字/血甲 → Settled**；
   - **兜底**：打击者/受击者不可解析、rig 失败、编排异常时，该组降级为普通冲刷（观感回到今天的飘字，但时点仍在运动落地之后）。
4. **通用打击 rig。** `CardAttackBasicAdapter.PlayEffectStrikeAsync`：场上任意打击者 → 任意受击者，强制相对冲刺 + 相对击退（rig 方向仅选烘焙时间线），不绑死亡回调（退场统一由移除/击杀呈现接手）；受击者将被移除时不回锚。经 `IFieldBattlePresentationSystem.PlayEffectStrikePresentAsync` 暴露；在攻击/反击 Present 内部（Drain 中）调用时不得新建战斗 CTS（会取消外层交战表演）。
5. **全局两倍速（`BattlePresentationSpeed.CombatMultiplier = 2`）。** 单点常量：交战 rig 的 DOTween Sequence `timeScale`（起手/冲刺/命中/击退/回位与命中、死亡回调全部等比缩短，玩家攻击/反击/齐射/决斗/效果打击全路径生效）、rig 缺 Sequence 的兜底等待（0.9s→0.45s）、起手音效对齐延迟、`PresentStep.TriggerCadenceSec`（0.35s→0.175s）。

**补记（2026-08-13c）——拾取旁路补第四消费点（倒刺打玩家丢动作的实际根因）。** 拾取（`ApplyPickupItemCommand`）推动互动数→Core 转盘在**拾取切片**内完成，但该切片此前由命令内 `BattleBeatFlush.PresentEventLogSlice` 立即 Impact→Settled 冲刷：打击组要么随 Settled 兜底被直接放行、要么根本无人编排——**倒刺经拾取转盘打玩家/怪的攻击动作全部静默丢失**（实测日志：只有击杀后的 PostKill 转盘批走 `AttackIntentScriptFactory` 的 PresentStep 才能看到倒刺打击，与「击杀邻怪后才打玩家」的观察一致）。修复：拾取切片的节拍冲刷延迟到手牌侧盘面 Drain（补牌+转盘运动）落地后，经新增 `BattleBeatFlush.PresentEventLogSliceWithStrikesAsync` 统一消费——OpenBatch（触发打击计划构建/暂扣）→ 触发脉冲 → 串行效果打击 → FlushBeats → FinishBatch，与 PresentStep 两相同构；顺带消灭拾取路径「旋转前掉血」错拍。拾取失败/无 Drain/Drain 取消路径按旧旁路立即冲刷兜底（节拍不丢失，仅无打击编排）。排期器 Settled 兜底放行日志升级为 Warning（进 Console 抓取轨，出现即代表仍有未播打击组）。

**补记（2026-08-13b）——命中反馈永不静默丢失（rig 层兜底）。** 交战/打击 rig 的 DOTween Sequence 可能生成失败，或被外部对参与者 transform 的 `DOKill`（卡面脉冲、收敛 SnapHome 等 `KillMotion`）中途整条杀掉——此前命中帧回调随之丢失，表现为「攻击/打击动作与受击反馈静默消失、只剩掉血」（倒刺打带甲玩家偶发丢反馈属此类）。`CardAttackBasicDirectionRig` 现把命中帧反馈（闪白 + `onCombatHit`）打包为可兜底补发的动作：Timeline 命中帧正常触达则原样播；Sequence 生成失败或播放中断时在 `PlayAsync` 末尾补发并留 `[CardAttackBasicDirectionRig] 命中帧未经 Timeline 触达` 告警（Console 抓取轨可持久化定位打断者）。配套：`CardAttackBasicAdapter.PlayEffectStrikeAsync` 返回是否真的播出，静默跳过如实上抛给编排器按降级冲刷处理。

**补记（2026-08-13）——借甲自然流失不入打击组。** 护甲图腾（`trap.armor_totem`）类借甲光环：邻接期间维持「基线 + 借出值」（借出的甲被消耗后下次刷新补回，基线随自有甲消耗下修）；离开邻接时未消耗借甲**自然流失**，Core 侧 `SyncAdjacentBorrowedArmorAction` 以 `cause = BorrowedArmorAuraKeys.DecayCause`（`borrowedArmorDecay`）发负向 `ArmorChanged`，`EffectStrikePlan.IsStrikeDamageInstruction` 按该 cause 排除——图腾从不「主动索取」，流失不得演成图腾攻击目标；且回收只取「当前甲 − 基线」的未消耗部分，永不扣目标自有护甲。

## 为什么

**症状：伤害/破坏缺可见来源。** 藤蔓机关（`tpl.trap.spike.move*`）与紫蝎翻面同批出伤时，玩家只看到自己瞬间掉一串血；滚石（`tpl.trap.rolling_stone.slot3`）移除卡片、捕熊陷阱（`tpl.trap.bear_trap.fill`）打伤害/移除时，卡片「怎么就突然没了」。根因：效果伤害只有 Impact 飘字/血甲，没有任何指向攻击来源的动作表演；且这些指令常被同批命中帧 `FlushImpactExcept` 一起冲掉，与交战飘字混在同一帧。

**为什么串行拉长也要打一遍。** 用户明确验收标准：宁可串行拉长演出，也要「谁打的我」可辨认。串行 + 全局两倍速对冲了总时长。

**为什么归因走 SourceDefId→EffectTriggered 而不是给事件加打击者字段。** `DealDamage` 原子的 `actor` 是规则语义（门保护、反伤目标），默认 `"Player"`，与「表演上谁打的」（效果持有卡）不一致；而 `EffectTriggered.CardUid` 恰是持有卡，且 Core 事件序保证触发先于其派生伤害（与神圣决斗扫描同一先例）。零 Core 改动、同一批内自洽。

**为什么顺带消除「旋转前掉血」。** 暂扣区让效果伤害指令躲过全部提前锚点（命中帧 / 首个 Remove 前 / 空投影批秒完成的 FlushBeats），统一锚定在「本批运动落地之后」的打击命中帧——这正是 ADR-0048 两相 Impact 的推广：把「运动停稳才见后果」从触发脉冲扩展到效果伤害本身。

## 考虑过的替代

- **给 `RemoveCardAction`/伤害事件补打击者 uid 字段（Core 协议扩展）**：否决——`EffectTriggered` 已携带持有卡，表现层可完整归因；避免动规则层协议。
- **打击编排全部放 PresentStep 两相之间**：否决——效果移除（滚石/捕熊）在 Drain 的 Remove 步就把受击者/打击者卸场了，两相时打击双方可能已不在场；必须在退场呈现前插消费点。
- **暂扣复用神圣决斗隔离区**：否决——决斗隔离区释放走全量 `ReportBeat(Impact)`，会把暂扣打击指令一并冲掉；两条泳道语义不同（决斗=定点释放全量，打击=按组谓词逐组释放 + Settled 兜底）。
- **逐 rig / 逐资产改时长做两倍速**：否决——DOTween Sequence `timeScale` 单点缩放覆盖烘焙 delay、duration 与回调延迟，改常量即全局生效、可回撤。

## 后果

- **行为变化**：藤蔓/滚石/捕熊/翻面出伤/反伤等场上卡效果伤害，从「瞬间掉血飘字」变为「来源卡逐个冲刺打击 + 受击闪白/击退 + 该目标飘字」；被破坏的卡先被撞击再碎裂退场；捕熊等自毁来源先打完再自毁。全体交战表演（玩家攻击/反击/齐射/决斗/效果打击）时长减半；触发脉冲→后续表演的节拍 0.35s→0.175s。
- **新不变量**：① 批内可见顺序 = 运动停稳 → 触发脉冲 → 效果打击（串行）→ 其余 Impact → Settled；② 效果伤害指令只在打击命中帧或 Settled 兜底放行，任何提前锚点不得冲刷暂扣区；③ 打击者判定 = 当批 `EffectTriggered` 持有卡且当前占格（离场来源降级）；④ 交战表演速度只经 `BattlePresentationSpeed` 单点调整。
- **已知限制（有意接受）**：① 战斗通道（攻击/反击）内翻面延迟到 FlushBeats，翻面触发的伤害（紫蝎在交战批内翻面）打击可能先于翻面动画可见（板面批不受影响：翻面在通道 Begin 前）；② Drain 内移除打击发生在触发脉冲第一相之前，机关的 `trap.trigger` 音效脉冲晚于打击（机关缩放脉冲本被 CoreKind 拦截，观感影响很小）；③ 卡面受击闪白/死亡退场等 SO 资产时长未随两倍速缩放（rig 时间线内的击退/回位已缩放）；④ 跨批场景（伤害事件批 N、可见运动批 N+1）仍需 Core 侧挂起（ADR-0044），本票只保证批内锚定。
- **回归**：`PresentationBeatOrderingContractTests` 新增——暂扣区躲过命中帧锚点与全量 Impact 报点、按组谓词放行、Settled 兜底不丢指令、打击计划归因（持有卡/移除标记）与排除（交战伤害/决斗/离场来源）。

## 相关

- [ADR-0048](0048-two-phase-impact-and-trigger-chain-dedup.md) — 两相 Impact 与链级去重：本票在两相之间插入打击编排，并把「运动落地才见后果」推广到效果伤害
- [ADR-0018](0018-trigger-visible-causality.md) — 触发可见因果：打击表演即「谁打的我」的可见因果
- [ADR-0044](0044-battle-window-deferred-board-motion.md) — 交战窗位移挂起：跨批错拍的 Core 侧先例
- [ADR-0007](0007-unified-presentation-pipeline.md) / [ADR-0005](0005-card-face-beat-commit.md) — 打击命中帧的冲刷仍走排期器 Handler 链同构出口
