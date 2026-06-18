# Assets/Scripts P0-P4 落地质检报告

质检日期：2026-06-18  
质检范围：`Assets/Scripts`  
依据文档：`C:\Users\jinji\Desktop\文档\MyNote\游戏开发项目\引擎工作区\九宫牌局架构`

## 结论总览

| 阶段 | 规划主题 | 落地结论 | 主要判断 |
|:--|:--|:--|:--|
| P0 | 工程基建 | 部分落地 → **验收链路已补** | 三层 asmdef、Architecture、RNG/Log/Config 已有；正式验收改为 Unity EditMode Test Runner（`Assets/Notes/CI`），17 条用例 MCP 验证通过。 |
| P1 | 数据模型层 | 基本落地 | CardInstance、Registry、Board/Deck/Player/Run、初始工厂与迁移测试齐备；缺独立控制台入口，仅有报告字符串/单测验证。 |
| P2 | 属性管线 | 基本落地 | Modifier、StatPipeline、RuleModifierRegistry、条件与 Scope 清理测试覆盖较完整；生命周期自动清理由后续系统触发，当前仍需手动调用。 |
| P3 | 动作流水线 + 触发骨架 | 部分落地 | GameAction、队列/反应栈、EventLog、TriggerSystem 与最小 Action 集已有；但 PRE 触发、旁路约束、完整 EventLog 断言仍偏薄。 |
| P4 | 棋盘/发牌/状态机 | 部分落地 | 可跑一个最短无技能战斗并进入奖励选择；但完整节点 FSM、道具/房间流、部分非法操作、补牌/剩余敌池语义未闭环。 |

当前整体状态：P0-P3 作为“Core 地基”已成型；P4 达到“最短路径可跑”，但还没有达到规划中“脚本化输入一串操作完整跑完一个节点”的验收强度。

## 本次验证记录

- 静态检查了 `Assets/Scripts/NineGrid.Core`、`NineGrid.Content`、`NineGrid.Presentation`、`NineGrid.Core.Tests`。
- 执行 `dotnet test 'Ninegrid Gambit.sln' --no-restore`：退出码为 0，但没有测试执行输出。
- 执行 `dotnet test 'Ninegrid Gambit.sln' --no-restore --list-tests -v normal`：构建成功，0 警告 0 错误，但未列出 NineGrid 测试。
- 发现 solution 当前没有生成 `NineGrid.Core` / `NineGrid.Core.Tests` 对应 csproj；`Get-ChildItem -Recurse -Filter '*NineGrid*.csproj'` 无结果。
- **修补后**：Unity MCP `run_tests(assembly_names=["NineGrid.Core.Tests"])` → **17/17 EditMode 通过**；正式验收见 `Assets/Notes/CI/README.md`。

结论：现有 `P0InfrastructureTests` 到 `P4NodeFlowTests` 是有效的 Unity/NUnit 测试源码证据；**正式验收已改为 Unity Test Runner**（`Assets/Notes/CI/run-core-tests.ps1`），不再以 solution 级 `dotnet test` 作为 P0 绿灯口径。

## P0 工程基建

已落地证据：

- `NineGrid.Core.asmdef` 存在，引用 `QFramework`，且 `noEngineReferences: true`。
- `NineGrid.Content.asmdef` 引用 `NineGrid.Core`，且 `noEngineReferences: true`。
- `NineGrid.Presentation.asmdef` 引用 `NineGrid.Core` / `NineGrid.Content` / `QFramework`，表现层允许 Engine 引用。
- `NineGridArchitecture` 注册了 `IRngUtility`、`ILogUtility`、`IConfigUtility`，以及 Core Models/Systems。
- `DeterministicRngUtility` 支持 seed、`CaptureState`、`RestoreState`。
- `P0InfrastructureTests` 覆盖 Architecture 可解析、RNG 同种子、RNG 状态恢复、Config/Log 内存桩。

未完全落地 / 风险：

- 规划验收要求“测试工程能 `dotnet test` 绿灯”。当前 `dotnet test` 没有跑到 NineGrid 测试项目，不能视为达成。
- `NineGrid.Core.Tests.asmdef` 是 Editor/TestAssemblies 风格，`noEngineReferences: false`，并非严格纯 C# 测试工程。
- “Core 里写 `using UnityEngine.UIElements` 会编译失败”目前主要依赖 asmdef `noEngineReferences`，缺少自动化守门测试或 CI 检查。

判定：P0 基建代码基本具备；**自动化验收链路已闭环**（Unity Test Runner + `Assets/Notes/CI` 脚本）。

## P1 数据模型层

已落地证据：

- `CardInstance` 包含 `Uid`、`DefId`、`Kind`、`StatBlock`、`CounterBag`、`Zone`、`Slot`、`EffectIds`。
- `CardRegistry` 支持 uid 自增、按 uid 获取、移动、清理。
- `BoardModel` 支持 9 槽、化身 uid/slot、福地标记、槽位放置/清除。
- `DeckModel` 支持抽牌堆、玩家池、敌方池、道具牌格。
- `PlayerModel` 支持 `StatBlock`、金币、遗物、技能、互动计数。
- `RunModel` 支持层、节点、种子、房间、阶段。
- `SlotId`、`CardKind`、`ZoneId`、`GamePhase` 等关键枚举/值类型已定义。
- `InitialGameFactory` 可构造初始状态并生成文本报告。
- `P1ModelTests` 覆盖化身在槽 5、空网格/空卡池、区域迁移从抽牌堆到棋盘槽。

未完全落地 / 风险：

- 规划里写“控制台能构造一局初始状态并完整打印”。当前是 `InitialGameFactory.BuildReport` + 单测断言，没有独立 console runner。
- P1 要求“不写规则逻辑”。当前后续 P2-P4 已继续落地，所以这一条仅作为阶段历史口径，不构成当前问题。

判定：P1 数据模型基本落地。

## P2 属性管线

已落地证据：

- `StatBlock` 存储基础值与 `StatModifier` 集合。
- `StatModifier` 覆盖 `Stat` / `Op` / `Layer` / `Source` / `Scope` / `Condition`。
- `StatPipeline` 按 base -> Persistent -> Conditional -> Temporary 现算。
- `EffectiveStatQuery` 已接入 QFramework Query。
- `RuleModifierRegistry` 支持 Add/Remove/RemoveBySource/ClearByScope/Evaluate。
- 条件层已有 `AtSlotCondition`、`HpBelowPctCondition`、`AdjacentCondition`。
- `P2StatPipelineTests` 覆盖持久加成、条件满足/不满足、三类临时 Scope 清理、按 Source 移除、RuleModifier 恢复倍率查询。

未完全落地 / 风险：

- Scope 的“何时失效”当前通过显式 `ClearModifiersByScope` 实现，测试也是直接调用；尚未和换敌、战斗结束、单次消耗等 P4/P5 生命周期事件自动挂接。
- RuleModifier 已能查询，但 P4 的互动距离判断目前仍是固定相邻判定，未使用 `RuleId.InteractionDistance`。

判定：P2 管线本体基本落地，生命周期联动属于后续整合缺口。

## P3 动作流水线 + 触发总线骨架

已落地证据：

- `GameAction` / `GameActionResult` / `GameActionContext` 已定义。
- `ActionPipelineSystem` 有主队列、反应栈、`RunToCompletion`、PRE/POST trigger dispatch、最大深度保护。
- `EventLog` 有序追加 `CoreGameEvent`，事件包含 sequence、action、actor/target/card、slot、amount/delta/remaining/message。
- `TriggerSystem` 支持按 `TriggerPoint + TriggerTiming` 注册、注销、分发。
- 最小 Action 集已包含 `DealDamage`、`Heal`、`GainArmor`、`ModifyGold`、`RemoveCard`、`Kill`。
- `P3ActionPipelineTests` 覆盖“击杀 -> 触发 -> 再伤害 -> 再击杀”的深度优先顺序，以及伤害先护甲后血量、血量最低 0、死亡进墓地。

未完全落地 / 风险：

- PRE 触发点没有专门测试；当前测试重点在 POST `OnKill`。
- EventLog “逐条断言”的覆盖还偏窄，主要断言击杀顺序和部分事件存在。
- “无直接改 Model 旁路”目前是人工代码审查口径，没有 analyzer/测试守门。静态扫描显示主要改动集中在 Model 方法、Factory、Action 内，系统层多数通过 Action，但没有机器约束。
- Heal/GainArmor/ModifyGold/RemoveCard 虽已实现，但 P3 测试没有逐项覆盖。

判定：P3 主干已落地，验收强度还需要补齐。

## P4 棋盘·发牌·内核状态机

已落地证据：

- `BoardSystem` 通过 Action 执行旋转、交换、补空槽。
- `RotateBoardClockwiseAction` 定义顺时针路径 `1 -> 2 -> 3 -> 6 -> 9 -> 8 -> 7 -> 4`。
- `FillEmptySlotsAction` 有稳定填充顺序，并从抽牌堆顶补牌。
- `DeckSystem` 能设置节点、开局发牌、判断棋盘/抽牌堆/节点清空。
- `PhaseSystem` 支持 `StartNode`、`Attack`、`PickupItem`、`ClickEmpty`、`UseItem`，并按阶段声明合法 Command。
- 交互后旋转/补牌路径已存在：击杀、拾取、点空格会进入 `ResolveInteractiveRotation`；未击杀攻击不会旋转。
- `P4NodeFlowTests` 覆盖稳定补牌/旋转、无技能击杀后金币结算并进入 `RewardItemChoice`、非法攻击被拒并广播 `Evt_ActionRejected`。

未完全落地 / 风险：

- 规划要求的节点状态机链路是“构建敌方池 -> 重置 -> 发牌 -> 互动循环 -> 通关检查 -> 道具三选一 -> 房间二选一 -> 房间事件 -> 下一节点”。当前实际只推进到 `RewardItemChoice`，之后 `RefreshLegalCommands` 又允许 `StartNode`，没有道具三选一、房间二选一、房间事件、下一节点语义。
- 规划指定 `PhaseSystem(FSMKit)`；当前是手写 phase switch，没有 FSMKit 状态对象或显式状态进入/退出。
- `UseItem(int itemUid)` 校验阶段、`itemUid` 存在、`ItemSlots` zone/kind（`Item`/`HelpCard`）；非法操作广播 `Evt_ActionRejected`（见 `P4NodeFlowTests` 五条用例）。
- 敌方池/玩家池为开局暂存区（`RUL_发牌` 第 3 步洗入抽牌堆），非节点内 reserve；`OpeningDealAction` 已 drain 剩余池牌，通关判定不再被滞留池牌阻塞。
- P4 测试未覆盖 `PickupItemCommand`、`ClickEmptyCommand`、`UseItemCommand`、攻击未击杀不旋转不计数、道具使用不旋转不计数、补牌耗尽、抽牌堆顶序、多敌/剩余敌池、完整脚本化操作串。

判定：P4 已跑通最短无技能战斗，不满足完整 P4 验收。

## 优先修复建议

1. 先补 P0 测试链路：生成/维护 `NineGrid.Core` 和 `NineGrid.Core.Tests` csproj，或明确改成 Unity Test Runner/batchmode 验收，并把命令写入 Notes/CI。

   **修补情况（2026-06-18）**：采用 **Unity EditMode Test Runner** 作为正式验收链路（未维护独立 csproj）。原因：`NineGrid.Core` 引用 `QFramework`，而 QFramework 源码依赖 `UnityEngine`；`NineGrid.Core.Tests` 为 Editor + `TestAssemblies` 程序集，根目录 Unity 生成的 `*.csproj`/`*.sln` 亦在 `.gitignore` 中。已新增 `Assets/Notes/CI/README.md`、`run-core-tests.ps1`、`run-core-tests.sh`；本地 MCP 验证 `NineGrid.Core.Tests` **17/17 通过**。batchmode 命令：`.\Assets\Notes\CI\run-core-tests.ps1`（需先关闭本工程 Unity 编辑器）。
2. 补 P4 的非法命令验证：`UseItem` 至少校验 uid 存在、zone/kind 合法、当前阶段合法；补对应测试。

   **修补情况（2026-06-18）**：`PhaseSystem.UseItem` 在入队前增加校验：`itemUid > 0`、卡牌存在、`ZoneId.ItemSlots` + `DeckModel.ItemSlotUids` 登记、`CardKind.Item`/`HelpCard`；非法时走 `Reject` 并广播 `Evt_ActionRejected`。新增 5 条 `P4NodeFlowTests`（阶段非法 / uid 不存在 / zone 非法 / kind 非法 / 合法用道具），P4 测试类 **8/8 通过**。
3. 明确 Deck/Pool 语义：敌方池是否是全节点 reserve；若是，应有抽牌堆耗尽后从池补入的规则；若不是，开局应把参与本节点的敌方池清空或改名避免误判。

   **修补情况（2026-06-18）**：对照 `RUL_发牌`，敌方池/玩家池为**节点开局暂存区**，非节点内 reserve；`OpeningDealAction` 在开局选牌后把两侧池**剩余牌全部洗入运行时抽牌堆**，开局后池应为空。抽牌堆耗尽时按规则停止补牌，不从池回灌。`DeckModel`/`DeckSystem` 已加语义注释。新增 `OpeningDealDrainsStagingPoolsIntoDrawPile`、`NodeStaysActiveWhileEnemiesRemainAfterStagingPoolIsDrained` 测试。
4. 补完整 P4 回放式测试：攻击击杀、攻击未击杀、拾取、点空格、用道具、补牌、清场、非法操作、最终阶段全部断言。

   **修补情况（2026-06-18）**：新增 `P4NodeFlowTests.FullNodeReplayScriptAssertsAllInteractionPaths`，以固定种子 `42` 串行回放一整局节点：非法攻击 → 攻击未击杀（无旋转/无互动计数）→ 非法拾取怪物 → 非法点占格 → 用道具（无旋转）→ 击杀弱怪（金币+旋转+补空）→ 拾取金币牌 → 点空格 → 清场循环 → `RewardItemChoice` 终态断言（合法命令集、EventLog 关键事件、`DrawPileExhausted`/`SlotsFilled`/`CardDealt` 等）。抽取 `RotateUntilAdjacent`、`AssertUseCommandRejected` 等辅助方法。Unity EditMode **25/25 通过**（`NineGrid.Core.Tests`）。
5. 为 P3/P4 加守门：禁止 Core 直接引用 UnityEngine、禁止系统绕过 Action 改 Model，可先用 `rg`/Roslyn 简单检查，后续再进 CI。

   **修补情况（2026-06-18）**：新增 `Assets/Notes/CI/check-core-guards.ps1` / `check-core-guards.sh`（`rg` 扫描 Core 与 Systems）；白名单 `ActionPipelineSystem` / `TriggerSystem` / `StatSystem`。镜像 EditMode 测试 `P0ArchitectureGuardTests`（3 条）+ 共享扫描器 `CoreArchitectureGuard`。`run-core-tests` 脚本在 Unity batchmode 前自动跑守门。当前代码库守门 **0 违规**；全量 **28/28** EditMode 通过。

## 本次质检未改动项

本报告只新增质检文档；未修改 `Assets/Scripts` 下任何代码。
