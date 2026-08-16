---
status: accepted
---

# 衍生卡检查面板导航（Spawn 关系解析 + 栈式返回）

## 决策

**「衍生卡」= 一张卡的效果装配会生成/涉及的另一张卡，唯一权威来源是效果模板 `Spawn` 动作的 `defId`；右键详述面板新增「衍生卡查看按钮」，从母卡切入衍生卡详情，退出时栈式返回母卡。** 四条不变量：

1. **解析是 Content 层静态投影，不依赖运行时 Catalog 解析状态。** `DerivedCardResolver`（`NineGrid.Content/CardPresentation/`）按卡 JSON `effectAssemblies[].templateId` 查 `effect_templates.json` 模板 `body`，递归收集 `action.atom == "Spawn"` 的 `defId`（去重、排除自身、跳过解析失败模板）；`effectIds` 只是装配 id 的冗余声明，不重复扫。结果带静态缓存，`ContentCatalogBootstrap.Load()` 时失效（与 `CardPresentationConfigCatalog` 同批，ADR-0008 内容重载纪律）。

2. **按钮接线走既有覆层命中通道。** 场景两面板（敌方/常规）各挂「衍生卡查看按钮」：`BoxCollider2D` 覆盖按钮底图世界尺寸 + `UiOverlayHitProxy`（新动作 `OpenDerivedCardInspect`，`CloseHitSort` 层）。文案动态：无衍生卡 →「衍生卡：无」；有 →「衍生卡：<显示名>」（多张顿号分隔，当前正式内容去重后均单张）；显示名取 `CardPresentationConfigCatalog.displayName`，缺失回退 defId。按钮无衍生卡或处于导航环时点击 no-op（不置灰、不改文案）。

3. **检查面板维护导航栈，退出衍生卡详情回到母卡详情。** 外部入口（右键菜单 `Open` / `OpenByDefId`）清栈重置；衍生卡按钮把当前条目入栈后切入衍生卡；`Close`（关闭钮 / 半黑屏 / 面板点击，行为统一）在栈深 > 1 时弹栈重放上一级（dimmer 保持持有、不闪半黑屏），栈深 = 1 才彻底关闭。**防环**：目标 defId 已在导航栈中则 no-op——衍生链可能成环（历史上复活石 ↔ 巨斧骷髅 曾互相衍生；现为 巨斧骷髅 → 复活石 → 巨剑骷髅 链式多跳），必须可终止。重放优先 live 卡已提交投影（保留局内数值，ADR-0035 检查描述仍静态投影），卡失效回退 defId 静态路径。

4. **衍生卡是 Monster 时走敌方面板并挂静态卡面。** 原 defId 路径固定常规面板；现按推断 kind 决定面板，敌方面板在无 live 卡时用 `EnsureLiveFaceByKind` 挂静态卡面（`kindOverride` 透传），衍生怪物（如 巨剑骷髅）可完整展示。

## 为什么

正式接线的卡牌中，死亡召唤/死亡之主 → 复活石、复活石 → 巨剑骷髅、多张遗物 → 教学卡等 `Spawn` 链已存在，但玩家在详情面板看不到「这张卡会涉及哪张卡」，也无法跳转查看——衍生关系是理解卡组连招（亡语链、遗物教学）的关键信息，藏在效果表里对玩家不可见。

**为什么解析放 Content 层**：卡 JSON（`effectAssemblies`）与模板表（`body`）都是静态投影输入，`CardPresentationConfigCatalog` / `EffectTemplateCatalog` 已在 Content 层提供直读口，无需等 `GameContentCatalog` 装配完成（其 `Effects` 依赖容器解析成功，失败会静默跳过）；展示层只消费 defId 列表。

**为什么栈式返回而不是「衍生卡内嵌返回按钮」**：关闭语义全局统一（关闭钮 / 半黑屏 / 面板点击都是「退一层」），玩家直觉一致；不新增 UI 元素，两个面板共享一套行为。dimmer 引用计数不被返回导航打断，半黑屏不闪。

**为什么防环用导航栈查重**：衍生链可能成环（复活石 ↔ 巨斧骷髅 历史上曾互相衍生；此后接线为 复活石 → 巨剑骷髅），不做查重可能无限入栈；查「目标是否已在当前导航链」语义精确——链式多跳（死亡召唤 → 复活石 → 巨剑骷髅）仍可达 3 层。

## 考虑过的替代

- **运行时 `GameContentCatalog.Effects` 解析衍生关系**：否决——`ContentEffectDefinition.Json` 只对装配解析成功的卡存在，容器类型缺失等失败会静默丢关系；检查面板应同 ADR-0035 一样与「运行时解析状态」解耦。
- **按钮常驻隐藏、仅在有衍生卡时显示**：否决——用户明确要求无衍生卡时文字框显示「衍生卡：无」，保持版面稳定、可发现性一致。
- **衍生卡详情内再放一个「返回」按钮**：否决——两个面板 × 两种形态都要维护，且与关闭语义重复；关闭即返回的栈式行为零新 UI。
- **按钮置灰表示不可点**：否决——像素风 TMP 无现成置灰样式，且「衍生卡：无」文案已足够传达；点击 no-op 不产生副作用。

## 后果

- **行为变化**：有衍生卡的卡（`monster.wandering_child` 巨斧骷髅、`skill.death_summon` 死亡召唤、`skill.lord_of_death` 死亡之主、`trap.revive_stone` 复活石、`relic.blood_shockwave` 血液冲击波、`relic.potion_bag` 药水袋、`relic.rotation_button` 旋转符文、`relic.swap_button` 交换之书、`relic.throwing_knife_bag` 飞刀袋、`relic.tower_child` 塔之子、`skill.gift` 礼物）详情面板按钮显示衍生卡名并可点击切入；无衍生卡显示「衍生卡：无」。衍生卡详情点关闭回到母卡详情，母卡再关才彻底关闭。
- **面板打开路径补强**：`Awake` 无条件 `BindScene`（幂等）——场景 presenter 序列化自旧代码版本时新字段不再悬空；`EnsureExists` 场景分支显式持有 `s_instance`——场景根 inactive 时（Awake 未跑）TryOpen 不再静默失败。
- **内容契约**：给卡配「会涉及的另一张卡」= 往其效果装配里加 `Spawn` 动作（模板 `action.atom == "Spawn"` + `defId`），无需改代码；解析失败/空模板静默跳过并缓存空结果。
- **回归**：无自动化测试（见 [`tests.md`](../code-map/tests.md) 冲刺期约定）；`recompile` 后 Console 无新增 Error，Play 手动验证覆盖：母卡→衍生卡→返回、3 层链式（死亡召唤→复活石→巨剑骷髅，Monster 切敌方面板）、防环 no-op、无衍生卡文案。

## 相关

- [ADR-0035](0035-dual-description-projection-and-assembly-param-refs.md) — 检查面板描述恒为静态检查描述（本决策同源：静态投影、不消费局内解析状态）
- [ADR-0037](0037-inspect-detail-is-glossary-rows.md) — 右键详情面板的词条行结构
- [ADR-0008](0008-single-source-content-and-resources-loading.md) — 内容装配与重载纪律（缓存失效挂载点）
