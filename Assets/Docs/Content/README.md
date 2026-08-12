# Content 区文档总览（NineGrid.Content / NineGrid.Content.Editor / NineGrid.DevTest）

> 本目录是预发布权威代码事实文档库 `Assets/Docs/` 的 Content 分册，覆盖三个程序集共 **149 个 .cs 文件**（快照日期 2026-08-12，全部逐文件读码成文）。文中相对路径基准为 `Assets/Scripts/NineGrid.Foundation/`。
> 分篇：[01 内容目录与加载链路](./01-内容目录与加载链路.md) · [02 效果模板与参数化装配](./02-效果模板与参数化装配.md) · [03 卡面表现JSON与视觉目录](./03-卡面表现JSON与视觉目录.md) · [04 音频与特效内容真源](./04-音频与特效内容真源.md) · [05 内容编辑器与卫生工具](./05-内容编辑器与卫生工具.md) · [06 编辑器工作台体系](./06-编辑器工作台体系.md) · [07 DevTest工具箱](./07-DevTest工具箱.md)

## 三个程序集的职责边界

| 程序集 | 路径 | 一句话职责 | 环境 |
|---|---|---|---|
| `NineGrid.Content` | `NineGrid.Content/` | 内容运行时层：把磁盘 JSON（一卡一文件 + tables + 音频/VFX 绑定表）投影成 Core 可消费的纯 C# Catalog 与表现层可消费的绑定/资产解析 API | 运行时（Editor + Player） |
| `NineGrid.Content.Editor` | `NineGrid.Content.Editor/` | 内容编辑器层：卡牌表现编辑器（UI Toolkit 窗口 + localhost 网页工作台）、音频/VFX 调音工作台、卫生校验、迁移工具 | 仅 Editor |
| `NineGrid.DevTest` | `NineGrid.DevTest/`（含 `Editor/` 子程序集） | 开发测试工具：小键盘 DevKeys 级联栈、主菜单 QuickTest 入口、作弊命令、日志导出 | Editor / Development Build（Release 仅空壳安装器） |

相互关系：`Content.Editor` 写盘 → `Content` 读盘投影 → `NineGrid.Core`（规则）与 `NineGrid.Presentation`（表现）消费；`DevTest` 不碰内容数据，只经表现层 Dev 缝驱动流程与战斗。`Content` 因 Core `noEngineReferences: true` 承担「磁盘源 → GameContentCatalog」的唯一投影职责（ADR-0008）。

## 内容数据流全链路（JSON 磁盘 → 索引 → 运行时目录 → 消费）

```
【作者侧】NineGrid.Content.Editor
  卡牌表现编辑器（窗口 / 网页工作台，同一 Session 真源）
  音频/VFX 调音工作台 · AI 初始绑定 · 迁移工具
        │ 保存 = 双写（cards/tables）或单写正式 Resources（音频/VFX 绑定）
        ▼
【磁盘真源】
  Assets/Arts/ContentVisual/cards/*.json（一卡一文件，Authoring 权威，341 文件）
    ⇄ 字节镜像 ⇄ Assets/StreamingAssets/ContentVisual/cards/*.json（Player 副本）
    └ _index.json：磁盘扫描快照索引（校验与外部工具用，运行时不依赖）
  Assets/Arts/ContentVisual/tables/*.json ⇄ StreamingAssets 同名镜像
    effect_templates / reward_pools / economy / node_deck_rules / monster_decks / visual_effects
  Assets/Resources/audio/：audio_bindings.json · audio_music.json · audio_manifest.json + BGM/ SFX/ 素材
  Assets/Resources/VFX/vfx_bindings.json
  Assets/Resources/ContentArt/：卡牌美术单一根（JSON 路径字符串 + #切片名 指向此处）
        │
        ▼
【运行时层】NineGrid.Content
  CardPresentationConfigCatalog（cards 内存目录：Editor 先 Arts 后 Streaming；Player 只 Streaming）
  ContentCatalogBootstrap.Load()：Invalidate 静态缓存 → 表加载 → 卡/技能/遗物/房间投影
    （effectAssemblies 经 EffectAssemblyResolver 解析进 Catalog.Effects）
    → 遭遇牌组（表行 + 卡面 deckId 推导成员） → RewardPoolQueryExpander 展开奖池
  AudioBindingCatalog / MusicBindingCatalog / VfxBindingCatalog（Resources 装载，五维选择器最高特异性解析）
  CardPresentationSpritePath / VfxMaterialFrameSource（路径 → Sprite/帧，Editor 走 AssetDatabase、Player 走 Resources）
        │
        ▼
【消费方】
  NineGrid.Core：GameContentCatalog（造卡、发牌、奖池、层主题绑定、效果 DSL 执行）
  NineGrid.Presentation：卡面装配与描述投影、店/奖/鉴预览、AudioSystem/MusicSystem/VfxSystem 播放决策
```

一卡一文件 JSON 的 schema（`CardPresentationConfigDto`，schemaVersion≥2 进玩法 Catalog）把**身份（contentId/kind）、分类三轴（deckId/role/tags）+ rarity、两级描述（description/faceIntro）、怪物几何与节奏（attackPattern/rhythmSource/rhythmPeriod/sequence/level）、数值（stats/gold）、效果装配（effectAssemblies：templateId+argsJson）、表现引用（sprites 七槽/mainVisual/animations 五槽/extraSlots）、房间字段（weight/openingInjects/iconPrefab/boardSlot 等）**收在同一文件（ADR-0008/0009）。文件名 = contentId 的 `.` 换 `_`。

## 阅读入口建议

- 上线后查「内容不生效/加载不出」：01 篇（加载链与卫生校验九类 Finding）→ 03 篇（路径解析与双写纪律）。
- 查「效果装错/描述数字错」：02 篇（装配解析与 `{装配id.键}` 契约）。
- 查「音效/特效响不响、放错素材」：04 篇（绑定解析语义）→ 05/06 篇（作者链与卫生校验）。
- 复现与调试：07 篇（QuickTest 入口、作弊命令、日志导出）。

## 关联 ADR 速查

ADR-0002（底盘+五卡面）、0008（单一权威+Resources）、0009（参数化模板）、0010（责任自陈）、0011/0038（攻击模式/节奏）、0014（主题 ID 不透明）、0022（节点装填）、0029/0030（内容护栏）、0032（非战斗可用）、0033（稀有度投放）、0035（双描述投影）、0036（音频真源）、0037（词条行）、0040（VFX 真源）、0042（教学关内容 `trap.tutorial.*`）。

## 阅读代码时发现的可疑问题（供后续排查，非阻塞）

1. `EffectAssemblyResolver.IsNumericArgKey` 与 `EffectDesignTextParameterizer.IsNumericArgKey` 是两份内容相同的白名单——新增数值键需两处同步，易漂移。
2. `effect_templates.json` 早期行的 `design_text` 存在编码历史遗留（GBK 写入痕迹），部分工具下显示乱码；仅影响作者备注展示，不进玩家界面。
3. `ContentCatalogTableLoader` / 经济表 / 节点规则表缺文件仅 LogWarning 静默降级——Player 若漏打 StreamingAssets 会表现为空目录而非硬错误。
4. `VfxBindingPlayback.ResolveTint` 把「RGBA 全 0」解释为「未配置→白色」，作者无法表达真全透明黑。
5. `CardPresentationEditorSession` 打开怪物卡面即执行 skillIds 展开并标脏——纯浏览也可能产生未保存改动（设计使然，但易误触保存）。
6. `MonsterLoadoutPresentationValidator` 的同步技能节奏检测（模板 id 含 `delivery.move`/`gear_delivery`）与投影层 `DetectSyncRhythmSkills`（扫 body `OnCardRhythmFire`）口径不一致，前者可能漏报。
7. `DamageLogPanel` 未接入 `DevTestSceneInstaller`，当前无场景常驻实例，F1 面板实际不可达（需手动挂）。
8. `EditorWorkbenchTransport` 的 delta 信封与 snapshot 信封 payload 相同（均为全量），语义上「delta」名不副实；前端不可依赖增量假设。

---

## 文件覆盖清单（149 / 149）

「篇」列为该文件的主要归属文档（01–07）。

### NineGrid.Content（69 文件）

| # | 文件（`NineGrid.Content/` 下） | 一句话说明 | 篇 |
|---|---|---|---|
| 1 | `ContentAssemblyMarker.cs` | 程序集空标记类 | 01 |
| 2 | `Audio/AudioAssetManifest.cs` | 正式音频固定清单 JSON 模型（guid/路径/键/哈希/kind/loadPolicy） | 04 |
| 3 | `Audio/AudioAssetManifestLoader.cs` | manifest 加载与固定 Resources 键查询（不扫目录） | 04 |
| 4 | `Audio/AudioAssetPaths.cs` | 音频根路径常量与正式/隔离/legacy 边界判定 | 04 |
| 5 | `Audio/AudioBindingCatalog.cs` | SFX 绑定表模型与最高特异性解析（含 BindingKey、authoringStatus、严格/容错解析） | 04 |
| 6 | `Audio/AudioCueRequest.cs` | 声音提示请求 struct（cueId + 五维稳定选择器 + 诊断 UID） | 04 |
| 7 | `Audio/MusicBindingCatalog.cs` | BGM 状态绑定表模型与解析（六个期望音乐状态） | 04 |
| 8 | `CardPresentation/CardAnimSlotIds.cs` | 动画槽代号常量（idle/attack/hurt/death/lunch）与规范化 | 03 |
| 9 | `CardPresentation/CardDescriptionTokenRules.cs` | ADR-0035 描述令牌契约与描述格 26 计量纯函数 | 02 |
| 10 | `CardPresentation/CardPresentationAnimResolve.cs` | 动画槽解析回退链（请求槽→idle→静态主图标） | 03 |
| 11 | `CardPresentation/CardPresentationAuthority.cs` | JSON 权威门面（HasConfig / 拥有描述 / 拥有显示名） | 03 |
| 12 | `CardPresentation/CardPresentationConfigCatalog.cs` | cards 目录内存缓存（Editor 先 Arts 后 Streaming；测试注入缝） | 03 |
| 13 | `CardPresentation/CardPresentationConfigDto.cs` | 一卡一文件 JSON 完整形状（含全部嵌套 DTO 与 EffectAssemblyDto） | 03 |
| 14 | `CardPresentation/CardPresentationContentArt.cs` | ContentArt 单一美术根常量、Resources 键提取、legacy 改写、资产路径收集 | 03 |
| 15 | `CardPresentation/CardPresentationIndexIO.cs` | `_index.json` 扫盘/读写/索引↔磁盘与双写镜像校验 | 03 |
| 16 | `CardPresentation/CardPresentationJsonIO.cs` | 单卡 JSON 读写与 Authoring+Streaming 双写、嵌套缺省补全 | 03 |
| 17 | `CardPresentation/CardPresentationPrimaryCardRules.cs` | 「主要内容卡牌」四类与归档排除规则 | 03 |
| 18 | `CardPresentation/CardPresentationSpritePath.cs` | `路径#切片` Sprite 加载（AssetDatabase / Resources 双通道）与编码 | 03 |
| 19 | `CardPresentation/VisualEffectContentArt.cs` | 像素特效精灵表 Resources 根常量与 legacy 改写 | 03 |
| 20 | `Catalog/CardFrameStyleCatalog.cs` | 卡框样式表（七个必备样式 id → 颜色） | 03 |
| 21 | `Catalog/CardFrameStyleDefinition.cs` | 卡框样式值对象 | 03 |
| 22 | `Catalog/ChoiceOptionVisualCatalogSO.cs` | ChoiceOption 视觉目录 SO 壳 | 03 |
| 23 | `Catalog/ContentCatalogBootstrap.cs` | 生产内容装配唯一入口 `Load()`（Invalidate→表→卡→牌组→奖池展开） | 01 |
| 24 | `Catalog/ContentCatalogTableLoader.cs` | 奖池/经济/节点规则/legacy 白名单四表加载与 tables 目录解析 | 01 |
| 25 | `Catalog/ContentColor.cs` | 引擎无关 RGBA 颜色结构 | 03 |
| 26 | `Catalog/ContentHygieneValidator.cs` | 内容卫生九类校验（索引/镜像/技能链/装配/模板引用/空壳/归档/非战斗目标/描述契约） | 01 |
| 27 | `Catalog/ContentJsonCatalogProjector.cs` | schema≥2 DTO → Card/Skill/Relic/Room 投影与装配解析入 Catalog | 01 |
| 28 | `Catalog/ContentVisualCatalog.cs` | 视觉条目字典容器 | 03 |
| 29 | `Catalog/ContentVisualDefinition.cs` | 视觉条目值对象（contentId+Kind+描述） | 03 |
| 30 | `Catalog/ContentVisualKind.cs` | 视觉条目种类枚举（九种） | 03 |
| 31 | `Catalog/ContentVisualResolvedView.cs` | 视觉解析结果聚合（显示名/五槽图/框样式与色） | 03 |
| 32 | `Catalog/ContentVisualResolver.cs` | contentId → ResolvedView 解析（稀有度→框样式、Boss/Elite→框色、按 Kind 取名） | 03 |
| 33 | `Catalog/ContentVisualSpriteCatalogBootstrapSO.cs` | Resources 引导 SO（Player 兜底加载 CatalogSet） | 03 |
| 34 | `Catalog/ContentVisualSpriteCatalogSO.cs` | SO 视觉目录基类 + 五槽条目 + Provider 接口 + 六 SO 路由集 | 03 |
| 35 | `Catalog/EffectAssemblyResolver.cs` | 模板+实参→ContentEffectDefinition 解析（含 EffectTemplateDefinition/EffectJsonWriter） | 02 |
| 36 | `Catalog/EffectDesignTextParameterizer.cs` | design_text 字面量 ↔ `{param}` 参数化（中文数字/量词启发式） | 02 |
| 37 | `Catalog/EffectTemplateCatalog.cs` | `effect_templates.json` 静态缓存加载 | 02 |
| 38 | `Catalog/HelpCardJsonCatalogProjector.cs` | 帮助卡投影历史入口（委托通用投影器） | 01 |
| 39 | `Catalog/HelpCardVisualCatalogSO.cs` | HelpCard 视觉目录 SO 壳 | 03 |
| 40 | `Catalog/MiscVisualCatalogSO.cs` | Avatar/Room/MonsterDeck 杂项视觉目录 SO 壳 | 03 |
| 41 | `Catalog/MonsterDeckCatalogBuilder.cs` | 遭遇表行 + 卡面 deckId 推导成员 → MonsterDecks | 01 |
| 42 | `Catalog/MonsterDeckTableCatalog.cs` | `monster_decks.json` 静态缓存（deck_id/deck_kind/display_name） | 01 |
| 43 | `Catalog/MonsterVisualCatalogSO.cs` | Monster 视觉目录 SO 壳 | 03 |
| 44 | `Catalog/RelicVisualCatalogSO.cs` | Relic 视觉目录 SO 壳 | 03 |
| 45 | `Catalog/SkillVisualCatalogSO.cs` | Skill 视觉目录 SO 壳 | 03 |
| 46 | `Catalog/TableNineContentCatalog.cs` | 小型 EditMode 测试夹具（fixture.heal 一套） | 01 |
| 47 | `Catalog/ThemeDeckFloorTierMapping.cs` | 七套 → 难度档（层池）硬编码契约 | 01 |
| 48 | `Catalog/ThemeDeckFormalReadiness.cs` | 正式可达性报告（Reachable 常绿门禁） | 01 |
| 49 | `Catalog/ThemeDeckMappingVerifier.cs` | 槽位映射正确性校验（重复/缺槽/错位/Boss 标志/跨套复用） | 01 |
| 50 | `Catalog/ThemeDeckNarrowContract.cs` | 逐套窄交付契约（#128 两套） | 01 |
| 51 | `Catalog/ThemeDeckStableMapping.cs` | 七套 × sequence 1–5 稳定 contentId 契约快照 | 01 |
| 52 | `Catalog/VisualEffectCatalog.cs` | `visual_effects.json` 特效库索引加载（Editor 素材索引，不参与运行时默认值合并） | 03 |
| 53 | `Catalog/VisualEffectEntryDto.cs` | 特效库条目 DTO | 03 |
| 54 | `Vfx/IVfxDomainHost.cs` | 视觉域宿主接口（父级/坐标转换/跟随/排序边界/遮罩） | 04 |
| 55 | `Vfx/VfxBindingAuthoringStatuses.cs` | VFX 作者态常量（aiDraft/humanConfirmed） | 04 |
| 56 | `Vfx/VfxBindingCatalog.cs` | VFX 绑定表模型与 Cue/State 宽松+Strict 解析（含选择器规则） | 04 |
| 57 | `Vfx/VfxBindingKey.cs` | 绑定键组合（identity + 五维选择器） | 04 |
| 58 | `Vfx/VfxBindingPlayback.cs` | 播放参数缺省统一（speed/tint/timeBase） | 04 |
| 59 | `Vfx/VfxBindingResolveError.cs` | Strict 解析错误载体（NotFound/Ambiguous/EmptyIdentity） | 04 |
| 60 | `Vfx/VfxCueRequest.cs` | VFX 提示请求 struct | 04 |
| 61 | `Vfx/VfxMaterialFrameSource.cs` | materialKey → sheetPath → Resources 精灵帧加载与排序 | 04 |
| 62 | `Vfx/VfxParticlePresetIds.cs` | particle 播放器 27 个稳定预设 ID 表（21 Pulse + 6 Loop） | 04 |
| 63 | `Vfx/VfxPlayerRegistry.cs` | 四播放器登记与能力位（Pulse/State/变体池/覆盖白名单） | 04 |
| 64 | `Vfx/VfxProjectilePresetIds.cs` | projectile 播放器 8 个弹道预设 ID 表（全 Pulse） | 04 |
| 65 | `Vfx/VfxSortingBounds.cs` | 宿主允许的排序层与 order 区间 | 04 |
| 66 | `Vfx/VfxSpatialContext.cs` | 一次请求的运行时定位依据（源/靶位置快照、域宿主、语义数量） | 04 |
| 67 | `Vfx/VfxSpatialOwnership.cs` | 空间所有权枚举（Attached/Independent） | 04 |
| 68 | `Vfx/VfxSpriteSheetFrameOrder.cs` | 精灵表帧名排序规则（spritesheet_N 优先） | 04 |
| 69 | `Vfx/VfxStateRequest.cs` | 持续视觉状态请求 struct | 04 |

### NineGrid.Content.Editor（52 文件）

| # | 文件（`NineGrid.Content.Editor/` 下） | 一句话说明 | 篇 |
|---|---|---|---|
| 70 | `AudioAiInitialBinder.cs` | #178 AI 全量初始绑定（启发式匹配 + 稀疏池增强 + 报告） | 05 |
| 71 | `AudioAssetAuditModels.cs` | #167 音频迁移审计报告 JSON 模型 | 05 |
| 72 | `AudioAssetHygieneValidator.cs` | 音频素材卫生六连校验与审计报告构建 | 05 |
| 73 | `AudioAssetMigrationRunner.cs` | #167 一次性素材迁移（GUID 保留、哈希核对、manifest 落盘） | 05 |
| 74 | `AudioBindingCatalogHygieneValidator.cs` | SFX Catalog 卫生（重复键/覆盖冲突/孤儿/断链/禁路径/参数域） | 05 |
| 75 | `AudioBindingEditorPreview.cs` | 非 Play Mode AudioUtil 试听缝 | 05 |
| 76 | `AudioBindingEditorSession.cs` | SFX 绑定作者会话（快照/工作副本/声明并集/历史键/保存拦截） | 05 |
| 77 | `AudioCueDeclarationEditor.cs` | 声音提示声明扫描菜单 | 05 |
| 78 | `AudioCueDeclarationScanner.cs` | `[AudioCue]` 反射扫描（空 id/说明、重复、未绑定） | 05 |
| 79 | `AudioWorkbenchEditorState.cs` | 音频工作台单一作者态（命令表/运行时观测/热应用/冲突门禁） | 06 |
| 80 | `AudioWorkbenchLauncher.cs` | 音频工作台菜单启动器 | 06 |
| 81 | `AudioWorkbenchServer.cs` | 音频工作台 loopback 入口（transport 装配） | 06 |
| 82 | `CardDescriptionBulkIO.cs` | 主要内容卡牌描述批量导出/导入（测试） | 05 |
| 83 | `CardFrameStyleXlsxIO.cs` | 旧卡框配色 xlsx 桥（Python，脚本缺失静默空表） | 05 |
| 84 | `CardPresentationEditorSession.cs` | 卡牌表现编辑会话核心（装载/脏/保存/自动描述/skillIds 展开/索引导出） | 05 |
| 85 | `CardPresentationEditorWindow.cs` | 表现层配置主窗口（五区侧栏 + 预览 + 参数 + 装配 + 词条页） | 05 |
| 86 | `CardPresentationFlipPreview.cs` | 编辑器翻牌预览（与实战 Flip 采样共曲线） | 05 |
| 87 | `CardPresentationMigration.cs` | 旧 Catalog SO/xlsx/Core → JSON 单向 seed 与条目门槛 | 05 |
| 88 | `CardPresentationWorkbench/CardPresentationWorkbenchAssetService.cs` | 工作台 PNG 资产服务（精灵裁切缩略图 + 离屏卡面/模板/样例渲染） | 06 |
| 89 | `CardPresentationWorkbench/CardPresentationWorkbenchEditorState.cs` | 表现层网页工作台作者态（包装 Session 的命令表与快照） | 06 |
| 90 | `CardPresentationWorkbench/CardPresentationWorkbenchHost.cs` | 域宿主（IEditorWorkbenchHost + AssetHost 桥） | 06 |
| 91 | `CardPresentationWorkbench/CardPresentationWorkbenchLauncher.cs` | 菜单启动器 | 06 |
| 92 | `CardPresentationWorkbench/CardPresentationWorkbenchServer.cs` | loopback 入口（域重载自动重启） | 06 |
| 93 | `ContentArtBreakLinkValidator.cs` | 资产路径断链扫描 + 内容卫生汇总菜单 + SkillVisualCatalog 悬空校验 | 05 |
| 94 | `ContentVisualEditorSession.cs` | 归档配图窗口会话（xlsx 行 + SO 五槽图 + 框色 + manifest 导出） | 05 |
| 95 | `ContentVisualEditorWindow.cs` | 归档配图窗口（批量指派/类型默认/框色页；JSON 条目只读） | 05 |
| 96 | `ContentVisualLubanMenu.cs` | Luban 退休公告菜单 | 05 |
| 97 | `ContentVisualSpriteKeyCodec.cs` | legacy 精灵键解码（仅一次性迁移用） | 05 |
| 98 | `ContentVisualXlsxIO.cs` | 旧 content_visual.xlsx 桥与通用 Python 调用 | 05 |
| 99 | `EditorWorkbench/AudioWorkbenchHost.cs` | Audio 域宿主薄桥 | 06 |
| 100 | `EditorWorkbench/EditorWorkbenchTransport.cs` | 通用 loopback 传输（鉴权/CSP/WS+长轮询/主线程泵/推送） | 06 |
| 101 | `EditorWorkbench/EditorWorkbenchTransportOptions.cs` | 传输参数包 | 06 |
| 102 | `EditorWorkbench/IEditorWorkbenchAssetHost.cs` | 可选二进制资产宿主契约 | 06 |
| 103 | `EditorWorkbench/IEditorWorkbenchHost.cs` | 工作台域宿主契约 | 06 |
| 104 | `EffectTemplateEditorIO.cs` | 效果模板表编辑读写（指纹脏跟踪、双写） | 05 |
| 105 | `MonsterDeckUsageInspector.cs` | 卡组遭遇接线状态只读诊断（五态） | 05 |
| 106 | `MonsterLoadoutPresentationValidator.cs` | 怪物遭遇配置一键校验（过渡期/交付就绪） | 05 |
| 107 | `MusicBindingEditorSession.cs` | BGM 绑定作者会话（快照/工作副本/保存回撤） | 05 |
| 108 | `Ui/ContentVisualWarmConsoleUi.cs` | 暖棕控制台 UI 构建工具库（主题与组件） | 05 |
| 109 | `Ui/SearchableChoiceField.cs` | 可搜索下拉控件（模糊+子序列匹配） | 05 |
| 110 | `VfxBindingCatalogHygieneValidator.cs` | VFX Catalog 卫生（键/冲突/孤儿/player 能力/素材与预设/覆盖白名单） | 05 |
| 111 | `VfxBindingEditorSession.cs` | vfx_bindings.json 原子读写核心（校验拦截 + 回读自校验） | 05 |
| 112 | `VfxBindingEditorWorkbenchSession.cs` | VFX 工作台绑定会话（声明∪绑定行、草稿、断链分流） | 06 |
| 113 | `VfxBindingParticlePresetRules.cs` | 粒子预设键可用性判定 | 05 |
| 114 | `VfxBindingProjectilePresetRules.cs` | 弹道预设键可用性判定 | 05 |
| 115 | `VfxCueDeclarationEditor.cs` | VFX 声明扫描菜单 | 05 |
| 116 | `VfxDeclarationScanner.cs` | `[VfxCue]`/`[PersistentVfxState]` 反射扫描 | 05 |
| 117 | `VfxWorkbenchEditorState.cs` | VFX 工作台单一作者态（三模式/热应用/清槽/冲突门禁） | 06 |
| 118 | `VfxWorkbenchHost.cs` | VFX 域宿主薄桥 | 06 |
| 119 | `VfxWorkbenchLauncher.cs` | VFX 工作台菜单启动器 | 06 |
| 120 | `VfxWorkbenchServer.cs` | VFX 工作台 loopback 入口 | 06 |
| 121 | `VisualEffectCatalogEditorIO.cs` | 特效库表编辑读写与目录树扫描合并 | 05 |

### NineGrid.DevTest（28 文件）

| # | 文件（`NineGrid.DevTest/` 下） | 一句话说明 | 篇 |
|---|---|---|---|
| 122 | `Cards/CardDeckManagerDevKeys.cs` | 卡组层 DevKeys（纯表现入场/发牌/增卡） | 07 |
| 123 | `Cards/CardHandManagerDevKeys.cs` | 手牌层 DevKeys（随机抓场地卡入手） | 07 |
| 124 | `Cards/DevTestStandardCardInstaller.cs` | 卡牌底盘预制体 DevTest 安装宿主（Release 空壳） | 07 |
| 125 | `Cards/GroundFieldManagerDevKeys.cs` | 场地层 DevKeys（翻牌/Core Flip/导演攻击/旋转/移除/即死） | 07 |
| 126 | `Cards/StandardCardViewDevKeys.cs` | 单卡数值调试（走 ApplyPresentation 不旁路 Set*） | 07 |
| 127 | `Commands/CheatBattleSessionCommands.cs` | 作弊命令三件（改血/改攻/强制胜利） | 07 |
| 128 | `Commands/CheatGameFlowCommands.cs` | QuickTest 启动命令与入口可用性查询 | 07 |
| 129 | `DevTestCompileGate.cs` | Dev 编译门闩标记 | 07 |
| 130 | `Editor/TestKeyCatalogEntry.cs` | 测试目录项值结构 | 07 |
| 131 | `Editor/TestKeyCatalogProvider.cs` | 测试目录构建（SO 声明 ∪ 运行时状态，供监视器） | 07 |
| 132 | `Editor/TestKeyStackConfigSOEditor.cs` | 级联栈 SO 自定义 Inspector（拖排/置顶） | 07 |
| 133 | `Flow/DamageLogPanel.cs` | F1 IMGUI 伤害日志面板（读 BattleTrace） | 07 |
| 134 | `Flow/DamageNumberManagerDevKeys.cs` | 已下线占位（不注册按键） | 07 |
| 135 | `Flow/DevTestSceneInstaller.cs` | MainScene 唯一 DevTest 安装宿主（#126） | 07 |
| 136 | `Flow/GoldGainFxManagerDevKeys.cs` | 金币飞入 DevKey（走正式播放链） | 07 |
| 137 | `Flow/InBattleManagerDevKeys.cs` | 战斗层 DevKeys（入场/结算/作弊/日志导出/Trace 开关） | 07 |
| 138 | `Flow/MainGameLoopManagerDevKeys.cs` | 流程层 DevKey（进入主循环测试） | 07 |
| 139 | `Flow/QuickTestEntryInputHandler.cs` | 主菜单 `\` 长按选关状态机 | 07 |
| 140 | `Flow/SelectorManagerDevKeys.cs` | 选择器层 DevKey（Bounce 三选一） | 07 |
| 141 | `TestKeyBinding.cs` | 按键绑定/注册动作/胜出条目三值结构 | 07 |
| 142 | `TestKeyBindingDefinition.cs` | SO 可序列化按键声明 | 07 |
| 143 | `TestKeyInputPoller.cs` | 常驻按键轮询器（RuntimeInitialize 自举） | 07 |
| 144 | `TestKeyLayerProfileSO.cs` | 测试按键层配置 SO | 07 |
| 145 | `TestKeyLayerRuntimeState.cs` | 运行时层状态（元数据+回调表） | 07 |
| 146 | `TestKeyManager.cs` | 级联栈按键管理器单例（裁决/轮询/监视 API） | 07 |
| 147 | `TestKeyModuleBehaviour.cs` | DevKeys 模块基类与注册 Builder | 07 |
| 148 | `TestKeyStackConfigSO.cs` | 级联栈配置 SO（末项最高优先级） | 07 |
| 149 | `TestKeyStackHost.cs` | 场景级联栈注入宿主 | 07 |
