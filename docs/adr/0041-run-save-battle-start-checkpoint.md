---
status: accepted
---

# 跑图存档：战斗开始检查点 + RNG 状态恢复 + ES3 桥装配缝

## 决策

1. **存档颗粒度 = 战斗开始**：正式局每个战斗节点在 `GameFlowOrchestrator.PlayRealBattleAsync` 内、`EnsureBattleNodeBootstrap` 之后且 **`BuildNodeDeckOptions` 消耗 RNG 之前**，由 `RunSaveService.CaptureCheckpoint` 捕获一次完整快照（检查点）并刷新自动存档。恢复永远落在「某场战斗的开始」；**不做**对战内部节点恢复。局中任意时刻手动存档，写入的都是本场战斗开始时的检查点。

2. **快照内容单一真源在 Core**：`RunSaveSnapshot`（`NineGrid.Core/Setup/RunSaveGame.cs`）承载：RunModel 进度（层/节点/种子/房间/本层卡组/已用卡组/属性房选择）、壳层全局节点序号、PlayerModel 全量（职业/金币/互动数/双容量/来源池/固定卡/强化/遗物）、道具卡格 defId 序列、Avatar 基础属性与计数器（含遗物 Run 作用域倒计时进度）、遗物 run 贡献（#120）、以及 **RNG 内部状态**（`RngState` 四字 + step）。Core 侧只做 DTO 与捕获/恢复逻辑（`RunSaveGame.Capture` / `RestoreAfterCreate`）；JSON 序列化与落盘在表现层（Core `noEngineReferences`）。

3. **恢复 = 重建 + 覆盖 + RNG 还原**：读档以快照种子走既有 `BootstrapRun`（`InitialGameFactory.Create`）重建初始局，再按固定顺序覆盖恢复：遗物对齐（多余 `DiscardRelicAction` 弃掉、缺失 `ActivateRelic` 补装并冲刷 OnActivate）→ 道具卡格按 defId 重造 → Avatar 基础属性/计数器覆写 → 遗物 run 贡献写回并重装 Persistent modifier → 金币/互动数绝对对齐 → 跑图进度 + `SetPhase(NodeCompleted)` → **最后** `RestoreState(RNG)`。相同 RNG 状态重跑 `BuildNodeDeckOptions` + `StartNode`，开局发牌与遭遇与存档时完全一致（已在 Play 中逐格验证盘面与抽牌堆）。

4. **流程壳恢复模式**：`GameFlowRunOptions.CreateRestore(snapshot)` 为正式模式变体；`GameFlowOrchestrator.Start` 在启动节点循环前完成恢复版 Bootstrap，并把壳层全局节点序号对齐到目标节点前一格（`SetNodeProgressBeforeRestoredNode`，保证 `ResolveBattleContentNodeIndex` 与捕获时一致）；`EnsureBattleNodeBootstrap` 对恢复后的首个战斗节点**不得**再走 `BootstrapRun` 覆盖（`mRestoredBootstrapPending` 一次性豁免）。

5. **自动存档生命周期**：每个战斗节点开始自动写入 `auto` 槽（覆盖）；run 终局（胜利/失败，`ShowBattleEndAndReturnAsync`）清内存检查点并删除自动存档；手动槽（`manual_1..3`）保留。中途回主菜单/退出进程不清自动存档——这正是「中途退出后恢复」的路径。QuickTest 局不产生检查点、不写任何存档。

6. **落盘经 ES3 桥装配缝**：Easy Save 3 无 asmdef（编入 Assembly-CSharp），工程程序集无法直接引用其 API。表现层只依赖 `IRunSaveStore`（按槽位读写原始 JSON），生产实现 `NineGrid.SaveBridge.Es3RunSaveStore`（`Assets/Scripts/NineGrid.SaveBridge/`，**无 asmdef 目录**）在 `RuntimeInitializeOnLoad(SubsystemRegistration)` 经 `RunSaveStoreHook.Set` 注册。存储布局：persistentDataPath 下 `NineGridSaves/slot_{id}.es3`，单键 `snapshot` 存快照 JSON。这是**装配缝而非业务 Sink**（对齐既有静态 Hook 范式），禁止业务代码经此读写规则状态。

7. **版本护栏**：快照带 `version`（当前 1）；读到不兼容版本记警告并忽略该槽，不做静默迁移。

## 为什么

- 战斗内状态（盘面、批次、时间线）序列化成本极高且易碎；「战斗开始」是全部跨战斗持久状态（RunModel/PlayerModel/Avatar/遗物贡献）汇聚且无临时态的天然一致性边界。RNG 状态恢复使「重跑发牌」即可复现开局，无需保存任何盘面。
- 复用 `BootstrapRun` + 既有跨层 `preserveRunInventory` 的「重建 + 覆盖」模式，而非另造一条初始化链路，最大限度共享既有清理（效果运行时、pipeline、PresentationSync、表现面）纪律。
- 恢复顺序把「绝对值对齐」放在遗物激活之后，避免 OnActivate 的加金/改基础值副作用叠加到快照值上（跨层作弊路径曾有此瑕疵，本 ADR 路径不允许）。

## 后果

- 手动存档在消费房（商店/卡店）进行时，回滚点是**上一场战斗的开始**：读档会丢弃该战斗之后的购买/回收（并重打该战斗）。这是颗粒度决策的直接推论，不算数据缺漏。
- 新增内容若引入「跨战斗持久」的新状态位，必须同步进 `RunSaveSnapshot` 与 `RestoreAfterCreate`（并递增 `version`），否则读档静默丢失——review 时以本 ADR 第 2 条为检查单。
- Avatar 上以 Permanent `StatModifier` 表达且**不来自遗物**（非 `ActivateRelic` 可重建、非 run 贡献）的永久增益不在快照内；当前内容全部经 `ModifyBaseStat`（基础值，已覆盖）或遗物通道表达，新增此类效果时须先扩展快照。
