# 本地化架构设计规格（v1 中↔英）——实施依据

> 状态：已定稿，供实施代理执行。盘点依据：本目录 `inventory-content-json.md`、`inventory-scene-texts.md`、`inventory-code-strings.md`。
> 本文件是过程笔记；正式协作文档最终落 `docs/localization/README.md` + code-map/ADR。

## 0. 总原则

- **中文是唯一真源**：原内容 JSON、场景 TMP、代码字面量一律不动中文；英文以**外挂翻译表**运行时覆盖。任何语言缺键 → 回退中文，**永不空串**。
- **不引入 com.unity.localization 包**，自建轻量层（与仓库 QF/装配缝惯例同构）。
- v1 范围：zh ↔ en，主菜单一键切换（切换只发生在主菜单；局内实时重投影不做，切换后新投影/新面板即用新语言）。
- **不翻**：B 类 Find/节点名串（90 行/13 文件，见 inventory-code-strings.md）、contentId/deckId/skillId/装配 id、日志/诊断、Editor 工具、Cheat（F12）与 QuickTest 面板文案、AudioCue/VfxCue 中文 note。归档卡（`deck.relic_archive`/`deck.help_archive`/`deck.transition`/`isReserve`）v1 跳过并在术语文档记录。

## 1. 语言运行时

- `LanguageId`：`zh`（源语言）/ `en`；字符串码持久化，枚举可扩展。
- 持久化：PlayerPrefs 键 `NineGrid.LanguagePreference.v1`，值 `"zh"`/`"en"`；默认 `zh`（future：首次启动可读 `PlatformInfo.LanguageCode`，v1 不做）。
- 系统：`ILanguageSettingsSystem`（照 `PlayerAudioSettingsSystem` 同构：PlayerPrefs 读写 + `Changed` 事件 + `PresentationSceneRoot` 主菜单生命周期装配）。
- 查询门面：`LocalizationCatalog`（表加载 + 查询；静态门面可接受——性质是内容目录/装配缝，不是业务规则状态 Sink）。放置程序集由实施者按依赖方向定：卡面覆盖缝若在 `NineGrid.Content` 内，则 Catalog 放 Content，Presentation 经它消费。
- 切语言时序：写 PlayerPrefs → 重载表 → 发 `Changed` → 订阅者自刷（主菜单可见文本立即刷新）。

## 2. 翻译表（Resources 加载）

目录：`Assets/Resources/Localization/en/`（zh 无表）。三个 JSON，UTF-8（无 BOM），schema 字段预留：

### cards.json（卡牌/卡组文本，键 = contentId / deckId）
```json
{ "schema": 1, "language": "en",
  "entries": {
    "monster.skull_head": { "displayName": "…", "description": "…", "faceIntro": "…" },
    "deck.dragon": { "displayName": "…" }
  } }
```
- 字段全部可选，缺字段回退中文。`monster_decks.json` 的 `display_name` 消费点与 deck JSON 的 displayName **共用 deckId 键**。

### glossary.json（词条表，键 = 现行中文词条名，Trim 后）
```json
{ "schema": 1, "language": "en",
  "entries": { "灼烧": { "displayName": "Burn", "intro": "…" } } }
```

### ui.json（代码串 + 场景静态标签，扁平键）
```json
{ "schema": 1, "language": "en",
  "entries": { "menu.start": "Start", "notice.gold_insufficient": "Not enough gold" } }
```
- 键命名：dot-namespace 小写：`menu. / charselect. / summary. / save. / settings. / notice. / briefTip. / floor. / shop. / tavern. / reward. / tutorial. / inspect. / preview. / hud. / recycle.`。
- 代码侧 API：`L10n.Tr("key", "中文默认值")`——中文默认值内联在代码里（zh 即走默认值，en 查表缺键也回中文）。含格式化的用 `Tr` 取模板再 `string.Format`（模板里写 `{0}`）。

## 3. 覆盖缝（接线点）

| 链路 | 缝 | 要求 |
|---|---|---|
| 卡面 displayName/description/faceIntro | `CardPresentationConfigCatalog.TryGet`（盘点报告定位的最窄缝；实施前验证） | 覆盖须发生在 **令牌投影之前**（`CardFaceDescriptionProjector` 填 `{装配id.键}` 之前），Inspect 与实例同一路径自然生效 |
| 卡组介绍（右键详述） | `CardInspectDetailComposer` / monster_decks display_name 消费点 | 同 deckId 键查 cards.json |
| 词条（着色/右键词条行/hover 解释） | `CardFaceDescriptionComposer` 匹配键构建 + `CardInspectGlossaryListView` 行渲染 | 匹配键 = **zh 名 ∪ en 名**（两套都命中，与当前语言无关，保证未翻/已翻描述都不断链）；显示名与 intro 按当前语言取 glossary.json，缺回中文 |
| 代码串 | 各字面量处改 `L10n.Tr(key, zh默认)` | 只动 A 类 ~90 处；B/C 类禁改 |
| 场景静态标签（18 条） | 单一 `SceneTextLocalizer` 组件（挂 MainScene 一处，**显式序列化** TMP 引用 + key 数组；不 Find） | 订阅 `Changed` 即刷；PrefabInstance 覆写的 m_text 同样以实例 TMP 引用接入 |
| `BattleInfoPreviewCopySO` 等文案 SO | 消费处走 `L10n.Tr` | SO 资产里的中文当默认值 |

## 4. 主菜单切换按钮

- 复制 MainPanel 现有按钮惯例（SpriteRenderer + BoxCollider2D + `标准世界文字` 实例 label），新 GO `LanguageToggle`。
- `GameFlowController` 加 `languageHit` 序列化字段 + FindDeep 回退 + Update 轮询 + 悬停缩放（与 `settingsHit` 完全同构）；点击 → `ILanguageSettingsSystem.Toggle()`。
- 按钮 label 显示当前语言（zh 显示「中文」，en 显示「English」），切换后整主菜单可见文本立即刷新。
- 场景改完必须保存场景；`PresentationSceneBindings` 若需引用按惯例走 SceneRoot 注入，禁隐式 Find。

## 5. 长度与字体

- 26 描述格是**中文计量 + 仅编辑器卫生校验**，英文不受硬限制但有 UI 溢出风险：
  - 翻译侧：描述/介绍**极限精简**（目标 ≤ ~34 拉丁字符，能短则短）。
  - 基建侧：检查卡面 `Basic_Description`/`faceIntro` 槽 TMP 是否 autosize；不足则按语言施加字号系数或开启 autosize（只动表现，不动 JSON 契约）。
- 字体三套（SmileySans-Oblique-3 SDF、ZhengGeDianHei-16、ChangBanDianSong-12 SDF[Dynamic]）拉丁覆盖可用，v1 不换字体；编辑器抽查即可。

## 6. 翻译红线（表数据必须遵守）

1. `{装配id.键}` 令牌**原样保留**（含大小写与点号），位置可随英文语序移动。
2. `[[中文词条名]]` → `[[英文词条名]]`，且必须与 glossary.json 里该词条的 en displayName **逐字一致**。
3. `[code]` 内联图标代号（如 `[Action_Icon]`）原样保留。
4. `\n` 换行、`·` 等版式符号按英文习惯处理但保持行数近似。
5. contentId/deckId/skillId、资源路径、Find 串永不出现在译文改动里。
6. 术语一致性锚点（两侧共用）：攻击=Attack、护甲=Armor、血量/生命=HP、金币=Gold、遗物=Relic、道具卡=Item Card、机关=Trap、怪物=Monster、楼层/层=Floor、层主=Boss、卡组=Deck、交战=Combat、互动=Interaction、行动计数=Action Count、移动计数=Move Count、离开机关=Exit Trap、回收=Recycle、抽牌堆=Draw Pile。其余以 glossary 先行统一。

## 7. 验证门槛

- `recompile` 后 Console 无本票新增 Error/Exception/Assert（Unity MCP 优先；两击放弃纪律）。
- 主菜单切换冒烟：Play → 点按钮 → 主菜单文本变英文 → 再点变回中文 → 重启保持偏好。
- 用户随后人工全面检查漏翻。

## 8. 文档分工

- 基建侧：`docs/localization/README.md`（协作格式权威：架构、表 schema、键规范、四类补翻操作手册、红线、验证步骤、加新语言步骤）+ `docs/code-map/README.md`/`presentation.md` 同步 + 新 ADR（本地化不变量）。
- 翻译侧：`docs/localization/terminology.md`（中英术语全表 + 跳过的归档清单）+ 三张表数据本体。
