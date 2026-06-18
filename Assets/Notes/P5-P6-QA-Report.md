# Assets/Scripts P5-P6 落地质检报告

质检日期：2026-06-18  
质检范围：`Assets/Scripts`  
依据文档：`C:\Users\jinji\Desktop\文档\MyNote\游戏开发项目\引擎工作区\九宫牌局架构`

## 结论总览

| 阶段 | 规划主题 | 落地结论 | 主要判断 |
|:--|:--|:--|:--|
| P5 | 效果系统 DSL | **基本落地，但严格验收仍需加固** | 原子接口、`[EffectAtom]` 注册、DSL 解析、触发/条件/目标/动作原子库、生命周期激活/失活、10 个代表机制测试均已具备；但校验器还偏“顶层字段校验”，未做原子 schema/未知原子/黄金样例快照回归，部分硬技能用简化替身验证机制。 |
| P6 | 全内容落地 | **部分落地，未达到 P6 验收** | 已有 C# 内存内容目录、经济/奖励/房间部分逻辑和 P6 测试；但未接 Luban，内容仍写在 `TableNineContentCatalog.cs`；大量内容是 `PendingAtom`，测试还主动要求 pending 数量存在；缺“全内容 DSL 通过五防线 + 回放全绿 + 改数值只动配表”的闭环。 |

当前整体状态：**P5 已能支撑继续内容试制；P6 还处在“内容目录和代表内容接入”的原型阶段**。若按架构规划签收，P5 可列为“可用但需补验收防线”，P6 应列为“未完成”。

## 本次验证记录

- 静态检查了 `NineGrid.Core/Effects`、`NineGrid.Core/Systems`、`NineGrid.Content/Catalog`、`NineGrid.Core.Tests`。
- 使用 codegraph 查看 `Assets/Scripts` 索引结构：当前脚本目录共 61 个 C# 源文件，包含 `NineGrid.Core`、`NineGrid.Content`、`NineGrid.Presentation`、`NineGrid.Core.Tests` 四块。
- Unity MCP `refresh_unity(scope=all, compile=request)`：刷新后 Unity ready。
- Unity MCP `run_tests(mode=EditMode, assembly_names=["NineGrid.Core.Tests"])`：**44/44 通过**，0 failed，0 skipped，耗时约 6.05s。
- Console 读取：无编译错误；仅有 Unity Test Framework 的 PerformanceTesting setup/cleanup warning，以及保存 `TestResults.xml` 的测试运行信息。

## P5 效果系统

### 已落地证据

1. 原子接口与注册表已落地：
   - `ITrigger / ICondition / ITarget / IAction` 定义在 `Assets/Scripts/NineGrid.Core/Effects/EffectAtoms.cs`。
   - `EffectAtomRegistry` 通过 `[EffectAtom]` + 反射发现已加载程序集原子，并按 Trigger/Condition/Target/Action 四类建表。

2. 首批原子库覆盖度较高：
   - Trigger：15 个，包括 `OnBattle`、`OnKill`、`OnRemove`、`OnUseHelpCard`、`OnSelfMove`、`OnMoveToSlot`、`OnEnter`、`OnArmorBreak`、`OnDamageTaken`、`OnFatalDamage`、`OnNodeStart`、`OnNodeEnd`、`OnRotate`、`OnInteract`、`OnCumulative`。
   - Condition：6 个，包括 `AtSlot`、`Adjacent`、`HpBelow`、`HasCard`、`OwnsRelicSet`、`LevelParity`。
   - Target：8 个，包括 `Self`、`Player`、`EventCard`、`RandomMonster`、`AllMonsters`、`OrthoAdjacent`、`SlotCard`、`Column`。
   - Action：17 个，包括 `Sequence`、`WeightedRandom`、`Repeat`、`Conditional`、`DealDamage`、`Heal`、`GainArmor`、`ModifyGold`、`Move`、`Swap`、`Rotate`、`ShuffleInto`、`Spawn`、`AddModifier`、`GrantSkill`、`RemoveCard`、`DeactivateSelfEffect`。

3. DSL 解析与运行时执行闭环已存在：
   - `EffectDefinitionParser.ParseJson` 将 JSON 转为 `EffectDefinition`。
   - `EffectSystem.Activate` 会先校验，再按 `Triggered / Modifier / RuleModifier` 三类激活。
   - Triggered effect 注册进 `TriggerSystem`；Modifier 注入 `StatSystem`；RuleModifier 注入 `RuleModifierRegistry`。
   - `EffectSystem.Deactivate` 会注销触发器并移除已注入的 StatModifier / RuleModifier，生命周期兜底是有的。

4. P5 代表机制测试较完整：
   - `P5EffectSystemTests` 覆盖注册表发现、五防线基础错误、OnSelfMove + 条件 + Sequence、OnCumulative、OnFatalDamage、WeightedRandom、条件光环、RuleModifier、OwnsRelicSet、RandomMonster + Repeat。
   - 这些用例与规划列出的硬机制基本对应，且当前 Unity EditMode 全量测试通过。

### 未完全落地 / 风险

1. 五防线校验器还不够硬。  
   当前 `EffectValidator.Validate` 主要做 `typeTag`、必填字段、互斥字段、verb 一致性和一个字符串快照；没有基于 `EffectAtomRegistry` 校验未知 atom，也没有逐 atom 的必填字段/schema、目标限制、动作参数范围、黄金样例快照文件回归。结果是：`ValidateCatalog()` 对 implemented DSL 的校验可能通过，但未知/拼错 atom 要到激活或运行时才暴露。

2. P5 测试有“机制替身”成分。  
   例如 `SelfMoveAdjacentSequenceCanRunThreeActions` 证明 OnSelfMove + Adjacent + Sequence 能执行三段动作，但不是“重新组合头”真实的“移除两个指定骷髅部件并洗入大骷髅”。这对验证框架有价值，但距离“用 DSL 实现代表性硬技能并单测通过”仍差一层真实内容语义。

3. 原子实现所在层级与架构文档存在偏差。  
   架构文档建议 Core 放接口/注册表，Content 放具体原子实现；当前 `EffectAtomLibrary.cs` 位于 `NineGrid.Core/Effects`。这不阻塞原型，但会让 Core 越来越厚，后续 Content/Luban 生成内容与原子扩展的边界不够清楚。

4. 动态数值/动态目标表达力仍不足。  
   P6 pending 内容里已经出现“造成等同于玩家攻击/当前血量/当前护甲”“战斗损失护甲动态伤害”“相邻指定 def 组合移除”“随机帮助卡目标”等需求，说明 P5 词汇表还缺动态 value expression、目标过滤器、目标选择约束、逆向旋转/下一次伤害免疫等长尾能力。

P5 判定：**可作为效果系统原型主干验收，但不建议作为最终 P5 签收**。建议先补校验器与真实硬技能用例，再让 P6 大规模录入。

## P6 全内容落地

### 已落地证据

1. 内容目录已形成：
   - `TableNineContentCatalog.CreateDefault()` 在 C# 中组装 effects、help cards、relics、player skills、monster skills、monster cards、rewards/rooms。
   - `P6ContentLandingTests.DefaultCatalogValidatesImplementedDslAndTracksLongTailAtoms` 要求 cards >= 70、skills >= 50、relics >= 15、implemented effects >= 30。

2. 内容系统接入 Core：
   - `ContentSystem.CreateDraft` 可按 defId 生成 `CardDraft`。
   - `ApplyContentToCard` / `ActivateCardEffects` 可把卡牌与技能 effect 激活到运行时。
   - `ActivateEffectIds` 会跳过非 `Implemented` effect，避免 pending 内容运行时炸掉。

3. 经济/奖励/房间已有原型闭环：
   - `EconomySystem` 处理怪物移除金币、未用帮助卡结算、跳过奖励、商店删除帮助卡。
   - `RewardSystem` 支持奖励池、房间选择、节点牌组规则、精英/Boss 击杀奖励、部分房间结算。
   - `P6ContentLandingTests` 覆盖帮助卡使用触发、怪物移除金币、精英击杀奖励洗牌、节点牌组构建、金币房/泉水房结算。

### 未完全落地 / 风险

1. 未接 Luban，也没有外部配表源。  
   内容目前硬编码在 `Assets/Scripts/NineGrid.Content/Catalog/TableNineContentCatalog.cs`，`IConfigUtility` 仍是 `InMemoryConfigUtility`。这不满足 P6 “策划改一个数值/概率只动配表、无需改代码即可生效”的验收。

2. 大量内容仍是 `PendingAtom`。  
   `TableNineContentCatalog` 中大量帮助卡、遗物、玩家技能、怪物技能通过 `Pending(...)` / `PendingSkill(...)` 录入。更关键的是，`P6ContentLandingTests` 当前断言 `report.PendingEffectIds.Count >= 30`，这说明 pending 是被测试认可的现状，而不是待清零的异常。

3. `ValidateCatalog()` 对 P6 签收偏宽松。  
   `ContentSystem.ValidateEffectDefinitions` 对 pending effect 只加入 `PendingEffectIds` 后跳过，不视为 issue；因此 `report.IsValid == true` 并不代表“全内容可执行”，只代表“已标记 implemented 的内容能通过当前基础校验，引用不缺失”。

4. 缺全内容回放测试网。  
   当前有 P4 的脚本化节点回放测试和 P6 的若干功能测试，但没有“录制操作序列 + seed -> 回放覆盖全部技能/遗物/帮助卡/奖励/房间”的通用 replay runner，也没有按内容覆盖率统计。

5. 长尾原子指标尚不能证明 P5 词汇表足够厚。  
   P6 规划要求“为新内容新增的原子数应很低”；当前实际状态是大量内容先标 pending，未进入真实 burn-down，因此还不能评估长尾原子是否激增。

P6 判定：**内容目录、代表内容、经济奖励房间原型已落地；全内容数据化尚未完成**。严格按规划，P6 不能签收。

## 整改项与阶段拆分

| 阶段 | 目标 | 重要性 | 工作量估算 | 交付物 |
|:--|:--|:--|:--|:--|
| R1 | 加固 P5 校验器 | 高 | 1-2 天 | `EffectValidator` 接入 `EffectAtomRegistry`；未知 atom、缺字段、参数范围、互斥规则进入 validation issue；补 invalid atom/schema 单测。 |
| R2 | 补真实硬技能用例 | 高 | 2-4 天 | 将 P5 代表测试从“机制替身”升级为真实内容：重新组合头/落石/凤凰羽毛/废物老虎机/流浪幼崽/龙鳞甲/渴望/木套装等均使用 catalog DSL 或同源 DSL。 |
| R3 | 建立 Luban/配表入口 | 高 | 3-5 天 | 明确配表源目录；接 Luban 生成 Content 配置类；`IConfigUtility` 支持加载生成数据；保留 `TableNineContentCatalog` 作为测试 fallback 或迁移脚本。 |
| R4 | P6 pending 内容清零 | 最高 | 5-10 天 | 按帮助卡 -> 遗物 -> 玩家技能 -> 怪物技能分批把 `PendingAtom` 转为 implemented DSL；每批要求 `ValidateCatalog` 通过且对应测试覆盖。 |
| R5 | 回放测试网与 CI 闸门 | 高 | 3-5 天 | 通用 replay runner；内容覆盖矩阵；CI 中新增“pending 数为 0”“全部 implemented DSL 通过校验”“代表 replay 全绿”三道闸。 |
| R6 | 原子层级整理 | 中 | 1-2 天 | 评估是否把具体 atom 从 Core 移到 Content/Atoms；若保留在 Core，也需在文档中明确“通用原子属于 Core，内容专属原子属于 Content”的边界。 |

建议顺序：**R1 -> R2 -> R3 -> R4 -> R5 -> R6**。  
理由：先把校验器变硬，再补真实硬技能，之后再大规模接配表和清 pending；否则 P6 录入量越大，后期越难判断是“数据错了”还是“原子表达力不够”。

## 签收建议

- P5：允许作为“原型主干完成”进入下一步，但正式签收前至少完成 R1、R2。
- P6：不建议签收；至少完成 R3、R4、R5，并把 `P6ContentLandingTests` 从“允许 pending”改成“pending 必须为 0”。

## 本次质检改动

- 新增本报告：`Assets/Notes/归档/P5-P6-QA-Report.md`。
- 未修改 `Assets/Scripts` 下任何代码。
