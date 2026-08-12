# ContentVisual 本地化盘点（只读）

> 生成日期：2026-08-12  
> 范围：`Assets/Arts/ContentVisual/`（Authoring 真源）及其 Streaming 镜像、`CardFaceDescriptionIconCatalog.asset`、相关消费代码。  
> 方法：目录枚举 + Python 中文扫描（`[\u4e00-\u9fff]`）+ 源码只读。

---

## 1. 目录结构与数量统计

### 1.1 顶层结构

```
Assets/Arts/ContentVisual/
├── cards/          # 一卡一文件 JSON（340 张 + _index.json 索引）
└── tables/         # 表 JSON（7 个数据文件）
```

Streaming 镜像：`Assets/StreamingAssets/ContentVisual/`，与 Authoring **文件集合一致**（cards 侧逐文件名比对无差异）。

### 1.2 卡牌 JSON 按 `kind` 统计（340 张，不含 `_index.json`）

| kind | 数量 | 文件名惯例 |
|------|------|-----------|
| Monster | 74 | `monster_*.json` |
| Skill | 86 | `skill_*.json` |
| Relic | 65 | `relic_*.json` |
| Trap | 44 | `trap_*.json` |
| HelpCard | 31 | `help_*.json` |
| Deck | 18 | `deck_*.json` |
| Room | 13 | PascalCase（如 `Tavern.json`、`Shop.json`） |
| ChoiceOption | 8 | PascalCase（如 `Attack.json`、`Hp.json`） |
| Avatar | 1 | `avatar_default.json` |

### 1.3 Live / 归档 / 过渡 / 储备

分类规则（互斥主桶，优先级：archive > transition > isReserve > live）：

| 桶 | 判定 | 数量 |
|----|------|------|
| **live** | 非 archive、非 `deck.transition`、非 `isReserve` | **295** |
| **archive** | `deckId` ∈ `{deck.relic_archive, deck.help_archive}` | **16**（Relic 9 + HelpCard 7） |
| **transition** | `deckId == deck.transition` | **29**（均为 Monster；且全部 `isReserve=true`） |
| **isReserve** | 字段 `isReserve: true` | 29（与 transition 完全重叠） |

按 kind 分布：

| kind | live | archive | transition |
|------|------|---------|------------|
| Monster | 45 | 0 | 29 |
| Relic | 56 | 9 | 0 |
| HelpCard | 24 | 7 | 0 |
| 其余 | 全部 live | — | — |

### 1.4 `tables/` 各表

| 文件 | 行/条数 | 含中文 |
|------|---------|--------|
| `effect_templates.json` | 247 | **是**（`design_text` 全表） |
| `visual_effects.json` | 182 | 否（英文 id/displayName；无 `note` 字段） |
| `monster_decks.json` | 11 | **是**（`display_name`） |
| `reward_pools.json` | 9 | 否 |
| `node_deck_rules.json` | 6 | 否（纯数值） |
| `economy.json` | 1 | 否 |
| `effects.json` | 0（空数组） | — |

**仓库外但相关**：`Assets/Resources/audio/audio_music.json`（BGM 绑定，无中文、无 `note`）。

---

## 2. 玩家可见字段总表

### 2.1 卡牌 JSON 主字段

| 字段 | 出现文件类别 | 玩家可见位置 | 含中文文件数 |
|------|-------------|-------------|-------------|
| `displayName` | 全部 9 类 | 卡面名称槽；房间选项名（`RoomOptionFaceVisuals`）；属性升级选项名（`AttributeBoardPresenter`）；角色选择（`CharacterSelectPanel`） | **340/340** |
| `description` | ChoiceOption、Room、Deck(4)、HelpCard、Monster、Relic、Skill、Trap | 卡面 `Basic_Description` 槽（经投影+Composer）；房间选项简述；属性升级简述；**不进**右键详述主区（ADR-0037） | **290** |
| `faceIntro` | Avatar、HelpCard、Monster、Relic、Trap | 右键详述「背景介绍」框（`CardInspectOverlayPresenter`） | **196** |
| `designSlotName` | HelpCard、Monster、Relic、Trap（策划填写的子集） | **否**（注释：仅表现/编辑器，对照设计案） | 50 |
| `attackPattern` | Monster、Trap | **否**（解析为 `AttackPattern` 枚举→图标；中文为作者令牌，如「普通近战」） | 118 |
| `rhythmSource` | Monster | **否**（解析为节奏源枚举，如「行动计数」） | 74 |
| `level` | Monster | **否**（投影为 `Normal`/`FloorBoss` 枚举，非 UI 文案） | 74 |
| `iconPrefab` | Room（13） | **否**（Asset 路径，含中文目录名「地形图标」） | 13 |
| `animations.slots[*].path` | 部分 Monster 等 | **否**（资源路径） | 63 |
| `sprites.mainIcon` 等 | 极少数 | **否**（资源路径） | 3 |
| `effectAssemblies[].argsJson` | 多类 | **否**（数值/键值；描述中的 `{装配id.键}` 引用此处，玩家只见插值结果） | 0（无直接中文） |
| `openingInjects` | Room（5 张） | **否**（结构引用 `cardDefId`，无展示文案字段） | 0 |

**Deck 元数据**：`deck_*.json`（kind=Deck）的 `displayName`/`description`/`faceIntro` 用于右键详述「牌组介绍」（`CardInspectDetailComposer.ResolveDeckIntro`）；仅 4 张 Deck 有 `description` 中文。

**Skill 卡**：86 张独立 JSON，`displayName`+`description` 投影进 `GameContentCatalog.Skills`；运行时卡面**不直接展示**技能卡文案，机制语汇主要通过怪物/遗物 `description` 里的 `[[词条]]` 与 `[code]` 间接呈现。编辑器/Workbench 仍消费技能描述。

### 2.2 `tables/` 字段

| 字段 | 表 | 玩家可见 | 含中文 |
|------|-----|---------|--------|
| `design_text` | `effect_templates.json` | **否**（作者/编辑器备注；ADR-0037 后不再堆砌进右键详述） | 247/247 |
| `display_name` | `monster_decks.json` | **是**（右键详述「牌组介绍」兜底，当无 Deck JSON 时） | 11/11 |
| `note` | — | **本仓库 ContentVisual 表内不存在** | — |
| `visual_effects.json` | VFX 目录 | **否**（技术 id + 英文 displayName） | 0 |

### 2.3 描述语法令牌（嵌在 `description` / `faceIntro` 内）

| 语法 | 解析位置 | 玩家可见形态 |
|------|---------|-------------|
| `{装配id.键}` | `CardFaceDescriptionParamFiller`（经 `CardFaceDescriptionProjector.Project`） | 数值字面量 |
| `[[展示名]]` | `CardFaceDescriptionComposer.ExpandDoubleBrackets` + `CardGlossaryTerms.ExtractExplicitTerms` | 着色明文 + 右键词条行 |
| `[code]` | `CardFaceDescriptionComposer`（`TryResolve`：装配 Insertable 优先，否则词条表） | TMP 内联 sprite 或保留原样 |

---

## 3. 词条表结构与匹配规则

### 3.1 资产与类

- 资产：`Assets/Resources/Arts/Cards/CardFaceDescriptionIconCatalog.asset`（YAML ScriptableObject）
- 类：`CardFaceDescriptionIconCatalogSO`（`Assets/Scripts/NineGrid.Presentation/Cards/Presentation/`）

### 3.2 条目字段

| 字段 | 作用 |
|------|------|
| `code` | `[code]` 图标代号；空=纯文字词条 |
| `displayNameZh` | `[[名字]]` **精确匹配键**；右键词条行标题 |
| `explanation` | 右键词条行正文 |
| `partition` | 认知分区（产品编排，默认 `Others`） |
| `sprite` | 内联图标 |
| `color` | 可选着色（`a>0` 生效） |

**条目总数：12**（截至盘点日）。

### 3.3 样例（5 条）

| code | displayNameZh | explanation（摘要） |
|------|---------------|---------------------|
| `attack` | 攻击 | 攻击力：造成伤害时的基础数值 |
| `armor` | 防御 | 护甲：抵消受到的伤害 |
| `adjacent` | 正交相邻 | 仅影响与自身正交相邻的格子或单位 |
| `普通攻击` | 普通攻击 | 由玩家发起的，在攻击范围内的一次常规攻击 |
| `move` | 移动计数 | 所属对象每产生一次移动时，计数+1 |

### 3.4 `[[词条]]` 匹配规则（代码确认）

实现链：`CardFaceDescriptionComposer` → `catalog.TryGetByDisplayName(name, …)` → `CardFaceDescriptionIconCatalogSO._nameLookup`。

| 规则 | 细节 |
|------|------|
| 匹配键 | `Entry.displayNameZh`（**不是** `code`） |
| 精确性 | **精确匹配**（字典键） |
| 大小写 | **区分**（`StringComparer.Ordinal`） |
| 空白 | 匹配前对 `[[…]]` 内文本 `.Trim()`；登记时对 `displayNameZh.Trim()` 建键 |
| 未命中 | 去括号显示原文 + `Debug.LogWarning`；右键词条行 `Matched=false` |
| `[code]` 匹配 | `TryGetByCode`：Ordinal 精确；保留装配槽名拒绝 |

**翻译断链风险**：若 `description` 英文化但 `[[…]]` 仍写中文名、或 `displayNameZh` 未同步改键，词条着色与右键解释行会失效。

---

## 4. 描述契约硬约束（翻译红线）

### 4.1 三类语法解析位置

| 语法 | 解析类 / 方法 |
|------|--------------|
| `{装配id.键}` | **填充**：`CardFaceDescriptionParamFiller.FillFromAssemblies`；**校验**：`CardDescriptionTokenRules.ValidateText` |
| `[[词条]]` | **渲染**：`CardFaceDescriptionComposer.ExpandDoubleBrackets`；**抽取**：`CardGlossaryTerms.ExtractExplicitTerms` |
| `[code]` | `CardFaceDescriptionComposer.Compose` → `TryResolve` |

投影统一入口：`CardFaceDescriptionProjector.Project` → `CardFaceDescriptionParamFiller`（Instance/Inspect 同文）。

### 4.2 描述格 ≤ 26

| 项 | 说明 |
|----|------|
| 计数规则 | 普通字符各 1 格；每个 `{…}`、`[…]`、`[[…]]` 各 **1 格**（`[[…]]` 优先于单括号） |
| 实现 | `CardDescriptionTokenRules.CountUnits` |
| 适用范围 | `description` + `faceIntro` |
| 约束卡种 | **仅** Trap / Relic / HelpCard，且 `deckId` 非 archive |
| **强制位置** | **仅编辑器/磁盘卫生**（`ContentHygieneValidator.ValidateDescriptionTokenContract` → category `description-contract`）；**运行时无强制** |
| 英文超长后果 | 卫生校验报错（合入前）；运行时 UI 可能溢出/换行异常，无硬截断 |

### 4.3 `description-contract` 触发条件

`ContentHygieneValidator.ValidateAll()` 之一，对每张 in-scope 卡调用 `CardDescriptionTokenRules.ValidateCard`：

1. 每张 `effectAssemblies[]` 须有稳定 `id`
2. `description` / `faceIntro` 中 `{token}` 须为 `{装配id.键}` 限定式（禁止简单式 `{value}`、defId 前缀式）
3. 令牌键须存在于对应装配 `argsJson`
4. `projectKey` 实参须等于「本装配 id.键」
5. 描述格 ≤ 26

**不触发**：Monster / Skill / Room / Deck / ChoiceOption / Avatar；`deck.relic_archive` / `deck.help_archive` 成员。

### 4.4 其他卫生类别（非翻译但相关）

`ValidateAll` 另含：index↔disk、mirror、skill-link、assembly、template-ref、empty-shell、archive-grant、usable-outside-battle。

---

## 5. 消费缝与推荐注入点

### 5.1 文本进入表现层主路径

```
磁盘 JSON
  → CardPresentationConfigCatalog.EnsureLoaded / TryGet
  → ContentCatalogBootstrap.Load
       ├─ ContentCatalogTableLoader（tables）
       └─ ContentJsonCatalogProjector.ApplyToCatalog（玩法 Catalog）
  → CoreCardPresentationMapper.TryApplyJsonPresentation
       ├─ snapshot.DisplayName / BasicDescription / FaceIntro / DetailDescription
       └─ CardFaceDescriptionProjector.Project（description 插值）
  → card.CommitPresentation(snapshot)
  → CardFacePresentationBinder（名称槽 + BasicDescription → CardFaceDescriptionComposer）
  → CardInspectOverlayPresenter（faceIntro、deckIntro、词条行）
```

### 5.2 `CardPresentationSnapshot` 文本字段

| 字段 | 来源 |
|------|------|
| `DisplayName` | JSON `displayName` |
| `BasicDescription` | JSON `description` 经 `CardFaceDescriptionProjector` |
| `FaceIntro` | JSON `faceIntro` 经 `CardFaceDescriptionParamFiller` |
| `DetailDescription` | 现等同 `BasicDescription`（`CardDetailDescriptionComposer` 已退役拼接） |

### 5.3 外挂翻译表（contentId + 字段覆盖）候选注入缝

按「越窄、越靠近消费」排序：

| 优先级 | 文件 | 方法 | 覆盖字段 | 说明 |
|--------|------|------|---------|------|
| ★1 | `CardPresentationConfigCatalog.cs` | `TryGet` / `EnsureLoaded`→`LoadFolder` | 全部 DTO 字符串字段 | **最窄单点**：不改 JSON，读盘后覆写 DTO |
| ★2 | `CoreCardPresentationMapper.cs` | `TryApplyJsonPresentation` | displayName, description→BasicDescription, faceIntro | 快照构建前覆写 |
| 3 | `CardFaceDescriptionProjector.cs` | `Project` | description（插值前/后） | 仅检查描述通路 |
| 4 | `CardFaceDescriptionParamFiller.cs` | `FillFromAssemblies` | description, faceIntro | 令牌填充层 |
| 5 | `CardFaceDescriptionComposer.cs` | `Compose` / `ExpandDoubleBrackets` | 描述渲染结果 | 需同步处理 `[[词条]]` 键策略 |
| 6 | `CardGlossaryTerms.cs` | `ExtractExplicitTerms` | 词条标题/解释 | 右键词条行 |
| 7 | `CardInspectDetailComposer.cs` | `Compose` / `ResolveDeckIntro` | faceIntro, deckIntro | 右键牌组介绍 |
| 8 | `ContentJsonCatalogProjector.cs` | `ResolveDisplayName` 及各 `TryProject*` | Catalog 侧 DisplayName/DesignText | 影响 Core 玩法层读名 |
| 9 | `ContentCatalogBootstrap.cs` | `Load` | 整包 Catalog | 最宽；tables 需另缝 |
| 10 | `CardFacePresentationBinder.cs` | `ApplyName` / `ApplyBasicDescription` | UI 最终绑定 | 兜底/UI 层 |
| 11 | `CardInspectOverlayPresenter.cs` | `Show` 系列 | 面板 TMP 文案 | 表现末端 |
| 12 | `RoomOptionFaceVisuals.cs` | 选项刷新逻辑 | Room displayName/description | 房间 UI |
| 13 | `AttributeBoardPresenter.cs` | 选项文案 | ChoiceOption displayName/description | 属性升级 UI |
| 14 | `CharacterSelectPanel.cs` | 角色名 | Avatar displayName | 选角 UI |
| 15 | `MonsterDeckCatalogBuilder` / table loader | monster deck `display_name` | 牌组介绍兜底 | tables 侧 |

**词条表**独立缝：`CardFaceDescriptionIconCatalogSO.TryGetByDisplayName` / `TryGetByCode`，或加载 asset 后替换 `entries`。

---

## 6. 抽样速览（每类 ≥1）

| 类 | 样例 contentId | displayName | description 片段 |
|----|---------------|-------------|-----------------|
| Monster | `monster.orc_commander` | 潜水鳄 | `[[普通近战]]，[[刺客领袖]]，[[起来]]` |
| Relic | `relic.golden_sword` | 机械巨弩 | `[attack]+{…}，[[普通攻击]]时[attack]-1` |
| HelpCard | `help.healing_potion` | 恢复药水 | `恢复{…}点[HP]` |
| Trap | `trap.bear_trap` | 捕熊陷阱 | `对下张[adjacent]的怪物/道具造成{…}点伤害` |
| Skill | `skill.blessing` | 庇佑 | `下一次受到伤害时，该次伤害变为0` |
| Room | `Tavern` | 卡店 | `道具卡强化/固定/扩容` |
| ChoiceOption | `Attack` | 攻击+1 | `永久+1攻击` |
| Deck | `deck.dragon` | 基础怪物卡组 | （空） |
| Avatar | `avatar.default` | 战士 | faceIntro: `深入地下城的战士…` |

---

## 附录：统计命令

盘点使用 Python 扫描 `Assets/Arts/ContentVisual/cards/*.json` 与 `tables/*.json`，中文判定正则 `[\u4e00-\u9fff]`。可复跑：

```python
# 见会话内 inventory 脚本；核心输出：kind 分布、主字段中文计数、live/archive 桶
```
