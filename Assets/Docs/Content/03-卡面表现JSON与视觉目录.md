# 卡面表现 JSON 与视觉目录（NineGrid.Content · CardPresentation + 视觉族）

> 覆盖范围：`CardPresentation/` 全部 12 文件（`CardDescriptionTokenRules` 详见 [02](./02-效果模板与参数化装配.md)），以及 `Catalog/` 中的 ContentVisual 视觉族、卡框样式、特效库表。
> 上游：编辑器写盘（见 [05](./05-内容编辑器与卫生工具.md) / [06](./06-编辑器工作台体系.md)）；下游：01 篇的目录投影与表现层卡面装配。

## 职责综述

本子域定义**一卡一文件 JSON 的数据形状与读写纪律**（ADR-0008：身份、数值、效果装配、表现引用同文件），并提供表现层解析视觉资产的工具链：`Assets/Resources/ContentArt` 单一美术根、`path#spriteName` 子切片编码、动画槽解析回退链。另含一组**已非权威**的 `*VisualCatalogSO`（ScriptableObject 视觉目录族）——ADR-0008 明确内容侧 SO 退休，它们目前作为「JSON 未配置路径时」的兜底图层与 `SpriteCatalogBootstrap` 运行时兜底存在。

## 关键类型表

### CardPresentation/（JSON 数据层）

| 类型 | 文件 | 一句话职责 |
|------|------|------|
| `CardPresentationConfigDto`（及嵌套 DTO） | `CardPresentation/CardPresentationConfigDto.cs` | 一卡一文件 JSON 的完整形状：身份/分类三轴/描述两级/攻击模式与节奏/数值/精灵槽/主视觉/动画槽/额外槽/装配引用/怪物编排/房间字段 |
| `CardPresentationConfigCatalog` | `CardPresentation/CardPresentationConfigCatalog.cs` | cards 目录内存缓存（contentId → DTO）：Editor 先 Arts 后 Streaming 补缺；Player 只读 Streaming；带测试注入缝 |
| `CardPresentationJsonIO` | `CardPresentation/CardPresentationJsonIO.cs` | 单卡 JSON 读写：`contentId` 点号 → 下划线文件名；`SaveAuthoring` 同步双写 Arts + StreamingAssets 并 ImportAsset；嵌套字段缺省补全 |
| `CardPresentationIndexIO` | `CardPresentation/CardPresentationIndexIO.cs` | `_index.json` 扫盘生成 / 读写 / 一致性校验（索引↔磁盘双向、重复检测、双侧索引字节一致、镜像校验） |
| `CardPresentationAuthority` | `CardPresentation/CardPresentationAuthority.cs` | 权威契约门面：`HasConfig` / `TryGetOwnedDescription` / `TryGetOwnedDisplayName`（JSON 有值即权威） |
| `CardPresentationPrimaryCardRules` | `CardPresentation/CardPresentationPrimaryCardRules.cs` | 「主要内容卡牌」四类（Monster/Relic/HelpCard/Trap）与归档排除规则（archive 卡组 / `deck.transition` / 怪物 isReserve / 卡组显示名含「归档」） |
| `CardPresentationContentArt` | `CardPresentation/CardPresentationContentArt.cs` | ContentArt 单一美术根常量与路径工具：Resources 相对键提取、legacy `Arts/Images` 前缀改写、单卡全部资产路径字段收集（断链扫描输入） |
| `CardPresentationSpritePath` | `CardPresentation/CardPresentationSpritePath.cs` | 按资产路径加载 Sprite：`path#spriteName` 拆装、Editor 走 AssetDatabase、含 `/Resources/` 走 Resources.Load、Multiple 图集编码 |
| `CardAnimSlotIds` | `CardPresentation/CardAnimSlotIds.cs` | 动画槽代号常量：idle/attack/hurt/death/lunch；未知规范化为 idle |
| `CardPresentationAnimResolve` | `CardPresentation/CardPresentationAnimResolve.cs` | 纯 DTO 动画槽解析：请求槽 → 可用帧槽 → 回退 idle → 回退静态 mainIcon |
| `CardDescriptionTokenRules` | `CardPresentation/CardDescriptionTokenRules.cs` | 描述令牌契约与描述格（详见 02 篇） |
| `VisualEffectContentArt` | `CardPresentation/VisualEffectContentArt.cs` | 像素特效精灵表根 `Assets/Resources/ContentArt/Multiple/Effects` 常量与 legacy 路径改写 |

### Catalog/（视觉目录族）

| 类型 | 文件 | 一句话职责 |
|------|------|------|
| `ContentVisualKind` | `Catalog/ContentVisualKind.cs` | 视觉条目种类枚举（HelpCard/Monster/Relic/Skill/Room/MonsterDeck/Avatar/ChoiceOption/Trap） |
| `ContentVisualDefinition` | `Catalog/ContentVisualDefinition.cs` | 视觉条目值对象：contentId + Kind + Description |
| `ContentVisualCatalog` | `Catalog/ContentVisualCatalog.cs` | 视觉条目字典容器（contentId → Definition） |
| `ContentVisualResolvedView` | `Catalog/ContentVisualResolvedView.cs` | 解析结果聚合：显示名/描述/五张直暴露槽 Sprite/卡框样式与颜色 |
| `ContentVisualResolver` | `Catalog/ContentVisualResolver.cs` | contentId + Core Catalog + 视觉目录 + Sprite 提供者 → `ContentVisualResolvedView`；稀有度→卡框样式、Boss/Elite→框色、按 Kind 取显示名 |
| `ContentVisualSpriteCatalogSO`（+ `ContentVisualSpriteEntry` / `IContentVisualSpriteProvider` / `ContentVisualDirectSlotSprites` / `ContentVisualSpriteCatalogSet`） | `Catalog/ContentVisualSpriteCatalogSO.cs` | SO 视觉目录基类：contentId → 五槽 Sprite（主图标/卡面背景/卡背三件套）；类型级 FallbackIcon；`CatalogSet` 按 Kind 路由六个 SO |
| `HelpCardVisualCatalogSO` / `MonsterVisualCatalogSO` / `RelicVisualCatalogSO` / `SkillVisualCatalogSO` / `MiscVisualCatalogSO` / `ChoiceOptionVisualCatalogSO` | `Catalog/*VisualCatalogSO.cs` | 六个按 Kind 特化的 SO 壳（Misc 承载 Avatar/Room/MonsterDeck） |
| `ContentVisualSpriteCatalogBootstrapSO` | `Catalog/ContentVisualSpriteCatalogBootstrapSO.cs` | Resources 引导 SO（`ContentVisual/SpriteCatalogBootstrap`）：让 Player 构建能拿到 CatalogSet |
| `CardFrameStyleCatalog` / `CardFrameStyleDefinition` | `Catalog/CardFrameStyleCatalog.cs` / `CardFrameStyleDefinition.cs` | 卡框样式表：七个必备样式 id（white/blue/gold/red/normal/elite/boss）→ 颜色 |
| `ContentColor` | `Catalog/ContentColor.cs` | 引擎无关 RGBA 颜色结构（供 Core 边界内传递框色） |
| `VisualEffectCatalog` / `VisualEffectEntryDto` | `Catalog/VisualEffectCatalog.cs` / `VisualEffectEntryDto.cs` | `visual_effects.json` 特效库索引（Editor 素材浏览用途；id/分类/变体/sheetPath/默认 fps/scale）；**不参与运行时绑定默认值合并**（ADR-0040） |

## 核心流程与数据流

### JSON 文件命名与双写

- 文件名 = `contentId` 的 `.` 换 `_`（`trap.flame` → `trap_flame.json`）。
- `SaveAuthoring` 同一份序列化文本**同时写** `Assets/Arts/ContentVisual/cards/`（Authoring）与 `Assets/StreamingAssets/ContentVisual/cards/`（运行时），Editor 下再 ImportAsset——这是「Authoring↔Streaming 字节镜像」卫生校验成立的前提。
- `ToJson` 时 `kind=="Deck"` 强制清空 legacy `deckKind`/`monsterDefIds`（遭遇编排已迁 tables）。
- `EnsureNestedDefaults` 把所有嵌套对象/数组补成非 null（JsonUtility 反序列化残缺文件的防御）。

### 加载优先级（`CardPresentationConfigCatalog`）

Editor（含 Play Mode in Editor）：先整载 Arts（overwrite），再补 Streaming 中 Arts 缺失的条目；Player：只有 Streaming。DTO 缺 `contentId` 时以文件名（下划线还原点号）为键。**运行时不读 `_index.json`**——索引只服务校验与外部工具。

### 资产路径与 Sprite 解析

JSON 里的图是**路径字符串**（无 GUID 保护，ADR-0008 的代价，靠断链校验器对冲）：

- 规范根：`Assets/Resources/ContentArt/`（`CardPresentationContentArt.RootAssetFolder`）；特效精灵表再收窄到 `…/ContentArt/Multiple/Effects`（`VisualEffectContentArt`）。
- Multiple 图集子切片：`路径.png#切片名`（`CardPresentationSpritePath.SplitPath/ComposePath`；编辑器写入时 `EncodeAssetReference` 只对多切片纹理附 `#`）。
- 加载：Editor 走 `AssetDatabase`（子切片按名匹配；无 `#` 的 Multiple 回退首切片/同名切片）；路径含 `/Resources/` 时截取相对键去扩展名走 `Resources.Load`（Player 唯一路径；ADR-0008 选 Resources 弃 Addressables 的落点）。
- legacy 迁移：`RewriteLegacyArtsImagesPath` 把 `Assets/Arts/Images/...` 前缀改写到 ContentArt 根（迁移工具与断链校验用）。
- `CollectAssetPathEntries`：把单卡 DTO 的 sprites 七槽 + `iconPrefab`（房间场地图标预制体，kind=prefab）+ 动画槽路径 + extraSlots 全部收集为 `(contentId, field, kind, path)` 条目——`ContentArtBreakLinkValidator`（Editor 篇）的输入。

### 动画槽解析回退链（`CardPresentationAnimResolve`）

`Resolve(config, requestedSlot)`：请求槽规范化（`CardAnimSlotIds.Normalize`，未知→idle）→ 找同名**可用**槽（sourceType ∈ {folder, atlas} 且 path 非空）→ 缺则回退 idle 槽 → 仍缺则返回 `StaticMainIcon`（卡面用主图标静态显示）。`lunch` 是可选特殊态（特殊攻击/施法）。帧序确定性由消费端配合 `VfxSpriteSheetFrameOrder` 类似的排序规则保证（卡面动画帧加载在表现层）。

### 视觉目录族与解析（SO 兜底层）

`ContentVisualResolver.TryResolve` 聚合三源：

1. `ContentVisualCatalog`（条目存在性 + Kind + 描述兜底）；
2. `GameContentCatalog`（显示名按 Kind 查 Cards/Relics/Skills/Rooms/MonsterDecks；Avatar 固定「玩家」；ChoiceOption 硬编码 Attack/Armor/Hp 三选项文案）；
3. `IContentVisualSpriteProvider`（五张直暴露槽图）。

卡框样式：HelpCard/Relic 按稀有度（White/Blue/Gold/Red → `frame.*`）；Monster 按 IsBoss→`frame.boss` / IsElite→`frame.elite` / 否则 normal。框色优先查 `CardFrameStyleCatalog`，缺表回退内建默认色（`ResolveDefaultFrameColor`）。

`ContentVisualSpriteCatalogSet` 按 Kind 路由到六个 SO；`TryGetDirectSlots`（直暴露槽路径）**只返回条目自定值**、不掺类型级 FallbackIcon——缺省回退语义归卡面模板（ADR-0002「图标缺省回退源模板，无全局卡背源」）。`TryGetWithFallback` 才会补 FallbackIcon（旧接口）。`ContentVisualSpriteCatalogBootstrapSO` 放在 `Resources/ContentVisual/SpriteCatalogBootstrap`，Player 兜底加载。

**权威提醒**：卡 JSON `sprites.*` 有路径时表现层**不再读这些 SO**（DTO 注释「槽位图权威（有路径则不再读 ContentVisual SO）」）；SO 族处于 ADR-0008 声明的退休轨道，仅作历史条目兜底，新内容一律走 JSON 路径字符串。

### 特效库表（`VisualEffectCatalog`）

`visual_effects.json` 行：`id`（如 `explosion/med/orange` 风格的稳定键）、`category`、`variantId`、`size`、`color`、`sheetPath`（精灵表路径）、`defaultFps`/`defaultScale`（Normalize 补默认 12/1）、`displayName`、预留 `timingBindings`（恒空数组）。它是 **Editor 素材浏览与导入索引**；运行时 `VfxMaterialFrameSource`（04 篇）只用它解析 `sheetPath` 定位帧，**不合并** defaultFps/defaultScale 进播放决策（播放参数唯一真源是 `vfx_bindings.json`，ADR-0040）。

## 对外通信面

- **入**：磁盘 cards/tables JSON；编辑器保存调用 `SaveAuthoring`。
- **出**：DTO 目录（01 篇投影消费）；`CardPresentationAuthority` / `CardPresentationSpritePath` / `CardPresentationAnimResolve` 被 `NineGrid.Presentation` 的卡面 Mapper、描述投影器（`CardFaceDescriptionProjector`）、店/奖/鉴预览消费；`CardPresentationPrimaryCardRules` 被批量描述导出（`CardDescriptionBulkIO`）与编辑器侧栏过滤消费。
- **不出**：数值——卡面显示数值只经 Catalog 造卡后由结算指令在表演锚点提交（ADR-0002/0005），表现层不得拿 DTO `stats` 直写战中卡面。

## 关联 ADR

ADR-0008（一卡一文件权威、ContentArt Resources 根、断链校验义务、`_index.json` 扫盘）、ADR-0002（五套卡面模板、装配槽表、卡背归卡组）、ADR-0009（`deckId` 全卡种必填、分类三轴字段）、ADR-0011/0038（attackPattern / rhythm 字段语义）、ADR-0022（Room 字段与 openingInjects）、ADR-0032（usableOutsideBattle 字段）、ADR-0035（description/faceIntro 契约与「主要内容卡牌」）、ADR-0040（visual_effects 仅索引不参与运行时合并）。

## 不变量与坑

- **双写必须同批**：只写 Arts 不写 Streaming（或反之）会被 `mirror` 卫生校验抓住；手改 JSON 后记得两侧同步（或走编辑器保存）。
- **路径字符串没有改名保护**：挪图、重命名切片不会自动重定向，必须跑断链扫描（`ContentArtBreakLinkValidator`）。
- `CardPresentationConfigCatalog` 的键容错（文件名还原 contentId）只是防御——正式内容 `contentId` 必填且与文件名规则一致，否则索引校验会响。
- `schemaVersion` 仍有历史 `1` 文件（仅表现、不进玩法 Catalog，如 ChoiceOption/Avatar 类条目）；投影准入是 `>=2`，改内容时别把 schema 1 的纯表现条目误升。
- `stats.action` 与 `rhythmPeriod` 是镜像关系（ADR-0038）；投影时 `rhythmPeriod>0` 优先，否则回退 `stats.action`。两处都写时保持一致，避免卡面行动计数与实际节奏不一致。
- SO 视觉目录族的 `SetSprites`（旧签名）会 `preserveUnspecifiedBack: true` 保留卡背三件套，而 `SetDirectSlots` 默认整组覆盖——编辑器代码选错重载会静默清卡背。
- `VisualEffectCatalog.Normalize` 会给 fps/scale 补默认值，但 `sheetPath` 为空的行不报错——特效库条目断链靠 Editor 卫生校验，不在加载层拦。
