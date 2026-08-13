---
status: accepted
---

# 动作管线失控链熔断遏制：不再让异常炸穿命令

## 决策

**`ActionPipelineSystem` 的反应链保护（深度 > 64 或单次 run 解算 > 4096 个动作）从「抛 `InvalidOperationException`」改为「熔断遏制」：丢弃超限分支、落 `PipelineFaultContained` 诊断事件、让命令原子收尾。**

1. **熔断不 Apply。** 超限的动作在 `ResolveAction` 入口被直接丢弃（不产生 ActionStarted/Finished，不触发任何反应），其祖先动作照常走完剩余触发与 FollowUp，`EventLog` 保持结构完整。
2. **双保险丝。** 深度上限 `MaxReactionDepth = 64` 捕获真·失控触发环（每层因果嵌套 +1）；总量上限 `MaxResolvedPerRun = 4096` 捕获有限深度的宽度爆炸（防主线程冻死）。均为常量，不开配置面。
3. **事实可审查。** 每次熔断追加 `CoreEventType.PipelineFaultContained` 诊断事件（携带被丢动作名、深度、已解算数；单次 run 最多落 8 条防刷屏），进 corelog 导出；该事件不进表现批次（同 `EnemyActionResolved` 模式）。
4. **现场可见。** Core 同时发 QF 事件 `Evt_PipelineFaultContained`，`BattleSessionExecutor` 订阅后 `Debug.LogError`——开发构建 Console 直接可见，QA 第一时间能发现内容死循环。
5. **熔断是兜底，不是许可。** 失控环的根治永远在内容层（效果 DSL 的 cause 防环标记、触发条件收敛）；熔断只保证「内容 bug 不把整局玩死」。触发熔断一律视为待修 bug。

### 补遗（2026-08-12 晚）：Apply / 触发分发异常同等遏制

**`action.Apply` 内抛出的任意异常（目标卡缺失 `KeyNotFoundException`、坏内容定义的 `InvalidOperationException` 等）与深度熔断同等对待**：丢弃该动作的事件/触发/FollowUp，落 `PipelineFaultContained`（message 前缀 `Action apply faulted: <异常类型>`），补 `ActionFinished` 保持 Started/Finished 配对，兄弟动作与命令收尾不受牵连。`DispatchTriggers` 的每个触发点分发（反应构建 `React`/`CanTrigger` 求值）同样单独遏制。理由：深度熔断只治了两种保险丝，Apply 内异常仍能炸穿命令、重现「遗物进装备栏但效果实例未挂、整局哑火」的二次灾害形态——防线必须覆盖全部异常出口。回归：`RelicMountResilienceTests`（FollowUp 链中途炸、遗物照常挂载、事件日志配对）。

## 为什么

**异常炸穿命令中途是比效果丢失严重得多的二次灾害。** 管线没有事务回滚：异常抛出时已 Apply 的动作留在 Core 模型里，`CoreCommandDispatcher.Send` 的批次不开、`Evt_PresentationBatchOpened` 不发，表现层永远收不到该命令的事实切片。后果是 Core 与表现盘面永久分叉：卡面还画在盘上但 Core 里已被移除（点击被门禁拒绝、"卡无法点击"）、遗物授予链被拦腰炸断（效果实例没挂上、整局哑火，2026-08-12 corelog-124012 的复合盔甲/生命护符事故）、流程协程死在半途。熔断遏制后命令保证收尾，两个世界保持锁步，最坏结果是「某条效果链被截断」——可见、可报、可继续玩。

**触发深度上限本身仍然必要。** 烈焰卡组事故（幽灵炎「剧烈燃烧」的 `EventFilterExcludeCause` 因 cause 管道断裂而永不命中 → OnDeal 无限自触发）证明内容层防环随时可能失手；上限是最后一道墙。问题从来不在墙，而在撞墙的方式——release 玩家撞上后整局报废且无日志可查。

**为什么不做事务回滚。** 模型层（CardRegistry/BoardModel/DeckModel/StatSystem…）无快照/回滚设施，为极端路径引入全量事务化的复杂度远超收益；熔断遏制以「截断而非回滚」达成一致性，代价只是被截断分支的效果不完整——这本来就是内容 bug 场景。

## 考虑过的替代

- **维持抛异常，表现层 try/catch 兜底**：否决——catch 到时批次已废，Core 状态已半应用，表现层无从知道哪些事实丢了；只能整局重置，玩家体验等同闪退。
- **事务回滚整条命令**：否决——见上，无基础设施，复杂度不成比例。
- **只修内容层防环，不动管线**：否决——单点修复不解决「下一个内容 bug 再炸一局」的系统性风险；release 无日志环境下必须保证局面可继续、事实可导出。
- **熔断时排空整个反应栈/队列**：否决——会把兄弟分支的合法反应一并丢掉；只丢超限分支的伤害面最小。

## 后果

- **行为变化**：曾经抛 `InvalidOperationException: Action reaction chain exceeded max depth.` 的场景改为静默截断 + Console 报错 + 诊断事件。依赖该异常语义的调用方（无）不存在；文档与测试同步更新。
- **新不变量**：任何命令执行后 `IsRunning` 必为 false、`EventLog` 结构完整（Started/Finished 配对）、批次照常打开——即使内容存在死循环。
- **日志审查**：corelog 扫描新增关注点 `PipelineFaultContained`（battlelog-analysis 技能的 Error/Exception/Assert 扫描应把它视为同级信号）。
- **回归**：`FlameIntenseBurningLoopRegressionTests`——真实内容验证剧烈燃烧 cause 防环修复（恰好 +1 张烈焰、cause 标记正确）；人造无防护 OnDeal 自触发环验证熔断不抛异常、留诊断事件、管线保持可用。
- 同票内容层修复：`ShuffleInto` 原子新增可选 `cause` 字段（缺省仍为 owner `SourceDefId` = 挂载卡 DefId），`tpl.skill.intense_burning.flame_deal` 显式携带 `"cause":"skill.intense_burning"` 使其排除条件真正生效。

## 相关

- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — Batch-ack 时间线：熔断保证批次边界不被异常破坏
- [ADR-0009](0009-parameterized-effect-templates.md) — 参数化效果模板：cause 防环标记属内容层根治手段
- [ADR-0010](0010-self-declared-effect-responsibility.md) — 效果自声明职责：拒绝/熔断皆事实化（`PipelineFaultContained`）
- 事故存案：2026-08-12 烈焰卡组 max depth 炸局（画面卡死、卡不可点击）；corelog-20260812-124012 遗物哑火（同类炸穿后果）
