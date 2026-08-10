---
status: accepted
---

# 装配参数引用唯一化与双套描述投影

## 决策

1. **描述数值唯一指向装配实例参数。** 卡面基础描述（检查描述）中的玩家可见数字一律写成 `{装配id.键}`；简单式 `{value}`/`{amount}` 与 `{卡defId.键}` 前缀式退役。每条装配须有稳定 `id`；`templateId` 只作作者辅助，不作解析权威。
2. **可见数字都进装配实参栏。** 可被规则修饰或平衡常改的数（首批重点：牌店可升级伤害）必须是真参数；纯叙事永不改的数也须进参数栏（值可与文案一模一样），禁止系统外写死。
3. **卡面描述恒静态。** **卡面介绍**（`faceIntro`，风味短句）≠ **卡面基础描述**（检查用静态规则概括）。实例可见表面（场上/手牌/道具格/遗物栏）与店/奖/鉴预览上的 `Basic_Description` **与检查描述同文**（`CardFaceDescriptionProjector` 只填 `description` + 装配初始实参，永不消费已提交倒计时剩余）。右键检查永远静态检查描述。
4. **效果倒计时改走专用 UI，不再改写描述。** 静态侧保留「每〈事件〉N〈单位〉 / …次后」句式。剩余次数只经结算指令在 **Settled** 提交（键为完整 `装配id.键`，`projectKey` DSL 声明），禁止 View 直读内核。**机关**（`period>1` 的 `projectKey`）→ 卡面 `ActionCount` 槽（`ShowActionCount`）；**遗物** → 遗物栏图标左下角计数 TMP。Run 作用域跨战斗忠实剩余；Battle 作用域离战真重置。JSON 字段 `liveTemplate`（局内描述模板）**已退役**。
5. **描述格硬上限 26。** 基础描述、介绍共用：字符 / 每个 `{…}` / 每个 `[…]` 各算 1 格。
6. **范围。** 本决策只约束真实接线的机关卡 / 遗物 / 道具卡（HelpCard）；怪物攻击行动倒计时仍走既有行动计数槽（[ADR-0013](0013-action-countdown-unified.md)），不在本双套描述范围内。归档弃用内容不改。**主要内容卡牌**（批量描述导出/导入语境）：`Monster` / `Relic` / `HelpCard` / `Trap` 四类；表现层编辑器里所属卡组显示名含「归档」的成员及 `deck.relic_archive` / `deck.help_archive` / `deck.transition` / 怪物 `isReserve` 不算正式接线（见 `CONTEXT.md` · `CardPresentationPrimaryCardRules`）。
7. **迁移工艺。** 逐卡对照内核原子与人手文案审计，禁止盲脚本替换；保留手写句子，只修写死/错线；顺手补 `faceIntro`（简洁有趣，≤26 格，后人手打磨）。
8. **遗物栏图标计数（addendum）。** 遗物 `ActivateRelic` 的 OwnerUid=0，倒计时计数器落在 Avatar 上；`CommitEffectCountdownRemaining` / `ClearEffectCountdownRemaining` 在 CardUid=0 时读 Avatar 计数器，并以 `SourceDefId=relic.*` 广播。表现层：`SourceDefId` 以 `relic.` 开头时**优先**路由到遗物栏 HUD（勿写入 Avatar 卡面）；图标裸数字只吃 Settled 已提交剩余（未提交前用装配 `threshold`/`every` 初值）。Collider 仍在锚点，显示壳为「标准遗物图标模板」。

## 为什么

旧简单式与「设计 5、填出 1」类错线同源：同键多装配时首个命中、或参数根本未暴露。检查描述必须保持说明书式静态「每 N 次」。投影数字走 Settled 与血/攻同一输出纪律（[ADR-0005](0005-card-face-beat-commit.md)），避免权威已变、表演未完就改字。遗物若按 CardUid 硬绑会因 OwnerUid=0 丢事件，或误把剩余写上玩家卡面。局内描述模板改写剩余造成描述跳动，与行动计数槽/遗物栏计数重复表达；退役 `liveTemplate` 后倒计时体感集中到专用 UI。

## 考虑过的替代

- **同句括号「（还剩 X）」**：否决——别扭，且与人手「每 N 次」文风冲突。
- **局内描述直读内核计数器**：否决——打破卡面显示值提交纪律，会提前变数。
- **局内描述模板动态插剩余（`liveTemplate`）**：否决（阶段一退役）——与 ActionCount / 遗物栏计数重复，描述跳动干扰阅读。
- **无倒计时卡/未获得预览走另一套静态通路**：否决——双通路易分叉；改为投影层恒在、与检查描述同文。
- **叙事数字允许不进参数栏**：否决——审计无法区分「摆烂写死」与「有意非参数」。
- **遗物倒计时另开一套非 Settled 通路**：否决——与卡面投影纪律分叉；统一 `projectKey` + Settled，仅多一条 SourceDefId 路由。

## 后果

- 修订 `CONTEXT.md` 中卡面基础描述 / 卡面介绍，并更新局内描述投影、装配参数引用、描述格。
- [ADR-0009](0009-parameterized-effect-templates.md) 第 4 点「卡面 `{参数}`」收紧为本 ADR 的限定式契约；初始插值仍成立，战中剩余改由机关 ActionCount / 遗物栏计数承担。
- 编辑器校验、内容审计、Settled 倒计时提交需落地；悬停简要（局内不触发）不在本 ADR 范围。
- 遗物栏计数与机关 ActionCount 共享同一 `EffectCountdownChanged` 缝；试点内容（如 `relic.terror_mask`）须在模板 body 暴露 `{{projectKey}}` 并由装配实参填写。

## 相关

- [ADR-0005](0005-card-face-beat-commit.md) — 卡面显示值结算锚点提交
- [ADR-0009](0009-parameterized-effect-templates.md) — 参数化效果与卡面 `{参数}`
- [ADR-0013](0013-action-countdown-unified.md) — 倒计时语义与计数设施
- [ADR-0027](0027-relic-drag-recycle-and-rmb-inspect.md) — 遗物栏拖弃与右键详述
- `CONTEXT.md` — 卡面基础描述、卡面介绍、局内描述投影、装配参数引用、描述格
