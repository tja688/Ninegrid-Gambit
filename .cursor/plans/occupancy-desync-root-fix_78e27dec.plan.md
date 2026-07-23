---
name: occupancy-desync-root-fix
overview: 已确认本次并非单纯门禁判定错误：#46 让高压输入更容易暴露旧的 Core↔Presentation 占格竞态，而真正破坏盘面的高危点是“批量移动先卸格再尽力回登”、把 Core 仍持有的错位牌当幽灵销毁，以及异步完成后按可复用 uid 释放。现有未提交的末端 relocate/repair 已在本次 `req=36` 中失败，不能作为最终修复。
todos:
  - id: red-loop
    content: 建立真实几何与完整 Director 压力下可稳定触发 Cuid/P0 的失败测试
    status: pending
  - id: atomic-occupancy
    content: 实现占格批量原子提交并切换 General/Rotate/Swap 路径
    status: pending
  - id: lifecycle-contract
    content: 修正 ghost 分类与异步实体身份释放，移除末端自愈
    status: pending
  - id: mainline-window
    content: 确保 Core Apply 始终处于 Director 主线或有效租约内
    status: pending
  - id: stress-verify
    content: 完成定向、全量和 100 局高压回归验收
    status: pending
isProject: false
---

# 占格失步根因修复方案

## 已锁定的事实
- Unity Console 的首次有效断点是 `req=36; relocated=4; cleared=2; repaired=2; diff=8:C5/P0`：格 8 的 Core 仍持有 uid 5，而 Presentation 已无登记；`req=37/38` 只是同一破损继续传播。
- [GroundMotionExecutor.cs](Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs) 当前先对全部 `movingUids` 执行 `General.Vacate`，随后忽略部分 `TryRegister` 失败；这是能直接制造 `Cuid/P0` 的非事务写入。
- [BoardPresentationPlayer.cs](Assets/Scripts/NineGrid.Presentation/Flow/BattleSession/BoardPresentationPlayer.cs) 的 Deal/末端 reconcile 会把“当前格与 Core 不符”混同为幽灵；工作区现有定向补位仍有 Deck/Hand/Place 静默失败，并且本次实测未修复。
- #46 的 latest-wins 与去除 FieldBusy/desync 熔断增加了连续 Drain 压力，但占格破坏源在表现几何与生命周期契约，不应回退统一门禁来掩盖。

## 实施步骤
1. **先建立能变红的确定性反馈环**
   - 在真实 [GroundOccupancyIndex.cs](Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundOccupancyIndex.cs)、`GroundFieldView`、`GroundMotionExecutor` 上新增最小 EditMode 夹具，复现“批量移动中一个计划缺视图/目标冲突”后出现 `Core uid / Presentation 0`；测试必须在当前代码下失败。
   - 扩展 [DrainVerticalSliceTests.cs](Assets/Scripts/NineGrid.Presentation/Tests/Flow/DrainVerticalSliceTests.cs)，用固定 seed、真实 Director/IntentIntake、延迟 ack 和同帧 Attack/Explore/Pickup 突发驱动完整 Drain；每次 ack 后断言 BoardModel 与 Ground snapshot 全格一致，并以 `OccupancyForceSyncGuard.InvocationCount > 0` 捕获用户的精确症状。
   - 临时给每个 BoardStep 前后、General move 回登结果、Deal ghost 分类、卡牌 Release 身份加统一诊断标签；复现后用“第一个从一致变不一致的 step”在 H1 批量移动、H2 Deal 误删、H3 stale async release 中定案，修完删除临时日志。

2. **把几何占格改为原子提交，消灭半完成盘面**
   - 在 [GroundOccupancyIndex.cs](Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundOccupancyIndex.cs) 增加批量 permutation API：提交前验证 uid 唯一、目标格唯一、所有视图存活、目标无外部 blocker；验证失败保持原快照不变并触发诊断，验证成功才一次性更新 slot↔uid 双向表。
   - 将 [GroundMotionExecutor.cs](Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs) 的 GeneralHop/Rotate/Swap 统一改走该原子 API，删除“全部 Vacate 后尽力回登”和忽略 `TryRegister` 返回值的路径；动画只消费已提交的几何计划，不再决定占格是否成功。

3. **修正 ghost 与生命周期定义，不在 Drain 尾部自愈**
   - 在 [BoardPresentationPlayer.cs](Assets/Scripts/NineGrid.Presentation/Flow/BattleSession/BoardPresentationPlayer.cs) 将占位者分类为：当前格正确、Core 在别格仍拥有、Core 已不拥有。只有第三类可 Release；第二类只能参与原子 relocate，绝不能销毁。
   - 移除/降回纯诊断当前 `RelocateMisplacedPresentationToCore + RepairMissingPresentationAgainstCore` 末端修补；保留 `#10` 断言作为不变量探针，避免把真实首因变成静默 force-sync。
   - 审计 [CardManagerSingleton.cs](Assets/Scripts/NineGrid.Presentation/Cards/CardManagerSingleton.cs)、[FieldBattlePresentationExecutor.cs](Assets/Scripts/NineGrid.Presentation/Cards/Battle/FieldBattlePresentationExecutor.cs)、[DealFlightCoordinator.cs](Assets/Scripts/NineGrid.Presentation/Cards/Ground/DealFlightCoordinator.cs) 的所有跨 `await`/`Forget` 清理：改用现有 `Release(ManagedCard)` 引用身份保护，禁止旧回调仅凭复用 uid 释放新节点实体；补一条结构护栏。

4. **封住 #46 暴露出的主线租约窗口**
   - 检查并调整 [CardHandManagerSingleton.cs](Assets/Scripts/NineGrid.Presentation/Cards/CardHandManagerSingleton.cs) 与 [IntentIntakeSystem.cs](Assets/Scripts/NineGrid.Presentation/Systems/IntentIntakeSystem.cs)：Core Apply 必须发生在 Director 主线/成功租约内；获取失败不得“继续表现”，也不得先改 Core 再抢锁；busy 时仍保持 latest-wins 缓冲语义。
   - 保留单一 IntentIntake、两轴门禁和 `OccupancyDesync` 仅诊断的 ADR 决策，不恢复 FieldBusy/BattleBusy 平行门禁。

5. **回归与高压验收**
   - 新增三类回归：批量移动失败原快照不变；Core-owned 错位牌永不被 ghost clear；旧异步句柄不能释放同 uid 新实体。
   - 运行受影响 EditMode 测试、全部 Editor tests，再用固定 seed 的无鼠标高压脚本循环至少 100 局/每局数百次同帧突发；验收条件为每个 ack 后全格一致、`OccupancyForceSyncGuard=0`、Console 无 `OccupancyDesyncLatched`/`#10`/异常。
   - 对当前 12 个未提交文件逐项保留或替换：不整批丢弃用户改动，但不会保留已被实测证伪的末端 reconcile、ForceEnd/preempt 或“无租约继续”逻辑。
