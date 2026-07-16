---
name: 根治击杀补牌错位
overview: 根因不是 VisualAim 算错，而是致死命中被表现层拆成了两个独立事务：第一个事务在 PostKill 之前擅自补牌，随后按旧 `combatSlot` 卸尸时误卸了新牌；尾部 Sync 又只移动 L2 视觉，留下 L0 根 Collider 在旧格。方案将致死命中与 PostKill 合并为同一有序表现批次，并建立 UID 卸格与输入命中的硬不变量。
todos:
  - id: merge-lethal-presentation
    content: 将致死命中 delta 与 PostKill Deal/Rotate 合并为单一有序 Drain
    status: pending
  - id: guard-vacate-by-uid
    content: 为场地卸格增加 expected uid 校验并覆盖所有致死调用点
    status: pending
  - id: enforce-input-frame-invariant
    content: 收束 Sync 落锚并统一卡牌 hover/click 的占格与 L0/L2 门禁
    status: pending
  - id: regression-and-log-verification
    content: 补 EditMode 回归并用帮助卡/击杀两条真实链路核验三轨日志
    status: pending
isProject: false
---

# 根治击杀补牌与点击错位

## 已确认的根因

- 正常帮助卡路径是同一批 `Deal → Rotate`；击杀路径当前却是 `Remove → DrainPostRemoveRefill(Deal)`，随后才单独 `ResolvePostKillBoard(Rotate)`。因此 `[DealVisualTargetResolver](Assets/Scripts/Cards/DealVisualTargetResolver.cs)` 无法跨两个 Drain 看见后续 Rotate，上次 VisualAim 补丁只修了批内前瞻，没有修事务被拆开的根因。
- 在 `[FieldBattleManagerSingleton](Assets/Scripts/Cards/FieldBattleManagerSingleton.cs)` 中，致死命中先执行 `DrainCombatHitBoardDeltaAsync`；该 Drain 因 `RemovedUids` 在 `[InBattleManagerSingleton](Assets/Scripts/Flow/InBattleManagerSingleton.cs)` 中提前调用 `DrainPostRemoveRefillAsync`，新牌已经占据死亡格。之后 `VacateSlotForExplore(combatSlot, combatVictim, ...)` 只按 slot 卸格、不校验 uid，于是实际卸掉新牌。
- 正常局日志 `20260716-141143` 已完整复现：uid 14 在 slot 2 发牌成功后被 `OccupancyVacate`，旋转只登记 7 张卡，出现 `ViewWithoutFieldOccupancy/OrphanAtWrongAnchor`；Sync 再将其登记到 slot 3，并用 `Ground.Place.Converge` 仅移动 L2。标准卡的 `BoxCollider2D`/`GroundCardHitProxy` 位于 L0 根节点（`[Standard Card.prefab](Assets/Prefabs/Standard%20Card.prefab)`），所以视觉在 slot 3、点击区仍在 slot 2，正好解释当前症状。

## 实施方案

1. **把致死命中恢复为一个表现事务**
  - 调整 `[FieldBattleManagerSingleton.cs](Assets/Scripts/Cards/FieldBattleManagerSingleton.cs)`：命中致死时不再先独立 Drain `hitResult`；先取得 PostKill Core 结果，再将命中阶段步骤与 PostKill 的 `Deal → Rotate` 按事件先后合并，尸体按 uid 卸格后只提交一次 `RequestDrainPostKillBoard`。
  - 非致死命中仍走原来的即时 Drain；PostKill 的 Core 顺序和 QFramework 内核不改。
  - 保留现有 VisualAim 前瞻与 flight 栅栏：合批后它才能在真实 `Deal → Rotate` 序列上发挥作用，使补牌从牌库直飞旋转后的最终格。
2. **把卸格 API 改为身份优先，禁止按旧格误伤新 occupant**
  - 收紧 `[GroundFieldManagerSingleton.VacateSlotForExplore](Assets/Scripts/Cards/GroundFieldManagerSingleton.cs)`：卸格前必须验证 `slot occupant uid == expected card uid`；不一致时拒绝卸格并记录包含 expected/actual/slot 的异常，绝不清掉实际 occupant。
  - 致死、反击致死、Avatar 致死等调用点统一走该校验；补回 EditMode 回归用例，覆盖“旧 combatSlot 已被另一 uid 占据时不得卸错卡”。
3. **建立视觉、占格、Collider 的最终一致性**
  - 将 `[InBattleManagerSingleton.SyncBoardOccupancyFromCore](Assets/Scripts/Flow/InBattleManagerSingleton.cs)` 定位为异常恢复而非正常动画：已有视图的 relocate/reanchor 必须最终 `SnapHome`，保证 L0 落最终锚且 L2 归零，禁止 Sync 返回后留下永久 L2 偏移。
  - 在 `[GroundCardHitProxy.cs](Assets/Scripts/Cards/GroundCardHitProxy.cs)` 统一 hover/click/pickup/monster battle 门禁：要求 uid 有占格、场地未锁、无 Deal/Slot convergence，且 L0 输入根与注册格锚对齐；所有卡种在分发点击前都经过同一检查。
  - 增加输入根与视觉世界坐标/注册锚的诊断，出现残留时记录明确 anomaly；`RefreshSlotHitColliders` 只负责空槽代理，不再被当成卡牌 Collider 一致性的修复手段。
4. **回归验证真实链路**
  - 新增/扩展 Cards EditMode 测试：致死批次合并顺序、UID 安全卸格、L2 残留时输入拒绝、Sync/SnapHome 后根与视觉重新一致；保留并运行 `[DealFlightMathTests.cs](Assets/Scripts/Cards/Tests/DealFlightMathTests.cs)` 的 VisualAim 用例。
  - Unity 编译与相关 EditMode 全绿后，分别复测帮助卡拾取和怪物击杀；停止 Play 后按同一 session 对照 Core/Perf/Registry。
  - 验收必须同时满足：致死链只有一个包含 `Deal → Rotate` 的表现批；新牌无后续误 `Vacate`；无 `ViewWithoutFieldOccupancy`、`OrphanAtWrongAnchor`、`SlotWorldMismatch`；飞行结束时 uid 的注册格、视觉坐标、L0 Collider 根一致；旧死亡格只响应当前 occupant，补牌视觉本体可正常 hover/点击。

