---
status: accepted
---

# 本地化：中文唯一真源 + 外挂翻译表覆盖

## 决策

1. **中文是唯一真源**：内容 JSON、场景 TMP、代码字面量里的中文永不因本地化改写；其他语言以外挂翻译表（`Assets/Resources/Localization/<lang>/` 下 cards / glossary / ui 三表）在运行时覆盖。不引入 `com.unity.localization`。
2. **缺键回退中文，永不空串**：查表 miss → 中文默认值；缺文件 = 空表 = 全回退（**静默**，不报错刷屏）；坏 JSON 只 LogWarning 一条且**不替换旧表**（`LocalizationCatalog` 严格 TryParse）。
3. **卡面文本覆盖缝唯一**：`CardPresentationConfigCatalog.TryGet` 按 contentId 覆盖 displayName / description / faceIntro（返回浅拷贝覆盖副本，不污染中文 DTO），覆盖发生在 `{装配id.键}` 令牌投影**之前**，Inspect 与实例同路生效；`monster_decks.json` 的 `display_name` 与 deck JSON 共用 deckId 键（兜底缝在 `CardInspectDetailComposer.ResolveDeckIntro`）。
4. **Find 串与稳定 id 永不翻译**：场景查找串（`transform.Find` / `FindDeep` / 节点名常量）、contentId / deckId / skillId / 装配 id、`{装配id.键}` 令牌、`[code]` 图标代号、日志 / 诊断 / Editor / Cheat / QuickTest 文案一律不进翻译范围。
5. **词条双键匹配**：`[[词条]]` 的匹配键 = 中文名 ∪ 各语言译名（与当前语言无关；`CardFaceDescriptionIconCatalogSO` 构建 lookup 时按 glossary 表登记外语键，冲突时中文键权威）——保证未翻 / 已翻描述都不断链。显示名与解释按当前语言取 glossary，缺回中文。
6. **代码串门面**：玩家可见代码字面量一律 `L10n.Tr(key, 中文默认值)`（`NineGrid.Core.Localization`，Core / Content / Presentation 全程序集可用）；含格式化的取 `{0}` 模板。文案 SO 资产保留中文，在消费处包 Tr。
7. **语言偏好**：PlayerPrefs 键 `NineGrid.LanguagePreference.v1`（`"zh"` / `"en"`，默认 zh），`ILanguageSettingsSystem` 唯一读写；切换只发生在主菜单，时序 = 写 PlayerPrefs → 重载表 → 发 `Changed` → 订阅者自刷（`SceneTextLocalizer` / 主菜单按钮 label）。
8. **场景静态标签**：单一 `SceneTextLocalizer` 组件（MainScene 一处，显式序列化 TMP 引用 + ui 键数组，禁止 Find），Awake 捕获场景中文原文作默认值。

## 为什么

冲刺期内容仍在高频改动，任何「翻译进源文件」的方案都会把翻译状态和内容状态搅在一起；外挂表 + 中文默认值让翻译可以并行、可缺失、可回滚，游戏在任何表状态下都可玩。词条双键是因为描述文本与词条表可能分批翻译，单键（仅当前语言）会让「英文描述 + 未翻词条表」或反之瞬间断链。

## 后果

- 新增玩家可见字面量必须走 `L10n.Tr`；新增场景标签必须挂进 `SceneTextLocalizer`；操作手册见 [`docs/localization/README.md`](../localization/README.md)。
- 消费方缓存表内容的（DTO 覆盖副本、词条名 lookup）必须按 `LocalizationCatalog.TablesVersion` 失效。
- Core 拒因（`result.Reason`）翻译后仍可上屏；表现层**禁止**再按中文 reason 串做逻辑匹配（英文技术 reason 如 `Not enough gold` 是协议串，不翻）。
- v1 范围 zh ↔ en；26 描述格约束只管中文（仅编辑器卫生），英文靠翻译侧精简（目标 ≤ ~34 拉丁字符）。
- 词条双键 v1 只登记「当前语言表」的外语名；多语言全集命中（任意语言互查）留待三语时扩展。

## 相关

- [ADR-0008](0008-single-source-content-and-resources-loading.md) — 一卡一文件 JSON 真源
- [ADR-0035](0035-dual-description-projection-and-assembly-param-refs.md) — `{装配id.键}` 令牌投影
- [ADR-0037](0037-inspect-detail-is-glossary-rows.md) — 词条行与 `[[…]]` 匹配
- `docs/localization/README.md` — 协作格式权威（表 schema / 键规范 / 操作手册）
