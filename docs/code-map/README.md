# Code Map（轻量入口）

> **权威长期文档**：本目录 + [`docs/adr/`](../adr/) + 根目录 [`CONTEXT.md`](../../CONTEXT.md)。  
> 旧源码镜像库 `Assets/Docs/九宫牌局-代码文档/` 已删除；勿再恢复为权威。

本地图描述 **仓库当下事实**，不描述未落地的目标树。

## 先读

| 文档 | 用途 |
|------|------|
| [presentation.md](./presentation.md) | `NineGrid.Presentation` 目录、装配、QF 边界、扩展点 |
| [tests.md](./tests.md) | 验证门槛（冲刺期无自动化测试套件） |
| [ADR-0001](../adr/0001-battle-presentation-unified-timeline-batch-ack.md) | 统一时间线 / Batch-ack |
| [ADR-0002](../adr/0002-card-chassis-and-face-templates.md) | 单底盘五卡面 + Commit |
| [ADR-0004](../adr/0004-input-intake-two-axis-gating.md) | IntentIntake 两轴门禁 |
| [ADR-0005](../adr/0005-card-face-beat-commit.md) | 卡面数值表演锚点提交 |
| [ADR-0006](../adr/0006-windows-high-polling-mouse-mitigation.md) | Win Player 高回报率鼠标兜底 |
| [ADR-0007](../adr/0007-unified-presentation-pipeline.md) | 多处理器统一表现管线 |
| [ADR-0008](../adr/0008-single-source-content-and-resources-loading.md) | 一卡一文件 JSON + ContentArt Resources 加载 |
| [ADR-0009](../adr/0009-parameterized-effect-templates.md) | 效果参数化模板、分类三轴、词条、奖池查询 |
| [ADR-0010](../adr/0010-self-declared-effect-responsibility.md) | 效果责任自陈、拆除外部场域门禁 |
| [ADR-0011](../adr/0011-monster-attack-pattern-intrinsic.md) | 怪物攻击模式为内生几何属性（**部分 superseded by ADR-0038**：频率表废止；**#77/#80/#82/#81 已落地**的几何与敌方开火仍有效） |
| [ADR-0012](../adr/0012-enemy-action-phase-volley.md) | 敌方行动阶段：齐射与盘面冻结（**#79/#80/#81 已落地**：Core 报名/逐条/收尾 + 四模式单向打击；表现 Counter 分拍 + ActionCount Commit） |
| [ADR-0013](../adr/0013-action-countdown-unified.md) | 倒计时语义与一次性开火窗口（**部分 superseded by ADR-0038**：模式与同步技能改为卡级共享倒计时；**#76/#79/#80/#81 数学与上屏已落地**） |
| [ADR-0014](../adr/0014-theme-ids-are-legacy-opaque.md) | 主题化 contentId/deckId 是历史残留不透明主键；卡组仅内部渠道；勿被虚构命名带偏 |
| [ADR-0016](../adr/0016-card-face-orientation.md) | 牌面朝向 Core 权威；背面双向惰性（不可伤害 / 不敌方开火 / 攻击倒计时冻结）；独立 `faceDownTick.*` |
| [ADR-0017](../adr/0017-trap-card-kind-and-dual-bucket.md) | 机关卡 `CardKind.Trap`；双桶交战；赏金排除；清关条件见 ADR-0026；静默 CounterAttackBanned；五套卡面 |
| [ADR-0018](../adr/0018-trigger-visible-causality.md) | 触发可见因果；Triggered 卡牌基础触发表现（v1 持有者缩放） |
| [ADR-0019](../adr/0019-avatar-board-walk.md) | 非战斗 Avatar 正交跳格；IntentIntake `boardWalk`；终点可图标格 |
| [ADR-0020](../adr/0020-board-as-interaction-surface.md) | 场地即交互面；场地图标落格 + 驻留提交；简要解释文字框 / 楼层提示（#89） |
| [ADR-0021](../adr/0021-run-progression-in-core.md) | 跑图进度与节点编排归 Core；8 节点/层；4/7 非战斗（清关兑金条款见 ADR-0025/0026） |
| [ADR-0022](../adr/0022-node-loadout-model.md) | 关卡装填：主题序列 + 玩家侧重生成；携带卡包已退役（ADR-0025） |
| [ADR-0025](../adr/0025-item-slots-run-persistent-hold.md) | 道具卡格跑图内持续持有；双容量；商店升级；回收；携带卡包退役 |
| [ADR-0026](../adr/0026-leave-trap-sole-clear-condition.md) | 离开机关为战斗房唯一清关；门/离开；默认 ⌈N/2⌉ 洗入，层主房改击破开局层主 |
| [ADR-0027](../adr/0027-relic-drag-recycle-and-rmb-inspect.md) | 遗物栏拖入回收区丢弃 + 右键详述 |
| [ADR-0028](../adr/0028-damage-formula-armor-and-reduction.md) | 标准伤害公式：三层护甲、伤害减免、无视护甲（公式层已落地；遗物内容另票） |
| [ADR-0029](../adr/0029-content-guardrail-stable-theme-deck-mapping.md) | 内容护栏：七套稳定 ID—策划槽位—sequence 映射契约（#127；`ThemeDeckStableMapping` + 映射正确 / 正式可达两层校验 + 禁 displayName 断言） |
| [ADR-0030](../adr/0030-regular-trap-opening-loadout.md) | 常规机关装填契约（#135）：正式战斗开局随机三张常规机关，`RegularTrapPool` 池过滤/无放回/种子可复现；离开机关与特殊机关不入池；`RewardSystem.BuildNodeDeckOptions` 唯一注入点 |
| [ADR-0031](../adr/0031-attribute-room-pick-two.md) | 属性房三选二会话（#136）：进房 3 加权候选 → 玩家选 2 → 结果经 `RunModel.AttributePickDefIds` 本关开局注入；自动随机注入退役 |
| [ADR-0032](../adr/0032-item-cards-usable-outside-battle.md) | 部分道具卡非战斗可用（`usableOutsideBattle` 卡级声明）：RoomChoice / RewardItemChoice 相位放行，RoomEvent 不放行；首批恢复药水/生日蛋糕/钱袋子；三处相位门禁改卡级裁决 + 非锁步冲刷表演 + 内容卫生告警 |
| [ADR-0033](../adr/0033-help-card-rarity-distribution-control.md) | 道具卡稀有度分级投放：常规 White 进入随机来源池，特殊卡走定向来源 |
| [ADR-0034](../adr/0034-board-stabilization-refill-batch-ack.md) | 盘面稳定化归 Core；补牌按独立切片与 Present ack 锁步推进；融合结果由 Core 管理延后抽牌 |
| [ADR-0035](../adr/0035-dual-description-projection-and-assembly-param-refs.md) | 装配参数引用唯一化（`{装配id.键}`）；检查描述 vs 局内描述投影双套；倒计时 Settled 提交（#156：`EffectCountdownChanged` → `CommittedCountdownRemaining` 快照重投影；#157：Battle/Run 作用域 + 离战真重置；**遗物栏图标计数**：OwnerUid=0 读 Avatar 计数器，`SourceDefId=relic.*` 路由 HUD，禁止误写玩家卡面）；描述格 26；范围限机关/遗物/道具；**#158 首批牌店可升级伤害升格**（help.bomb/help.throwing_knife：伤害为装配实参 + 描述限定令牌 + faceIntro 草稿 ≤26，双侧镜像）；**#159 道具卡全集逐卡审计**（19 张 live HelpCard：6 张简单式令牌升格 `{装配id.键}`——healing_potion/swap_card/sturdy_shield/teleport_card/gold_card/kidnapping，其余散文保留；全卡 faceIntro 草稿 ≤26；归档卡豁免原样）；**#160 遗物全集逐卡审计**（51 张 live Relic：terror_mask `{value}` 等简单式/写死数字/叙事同值双胞胎（阈值/百分比/步长/格位）全部唯一化入库对齐内核常数；junk_sword 描述按内核 OnKill 修正；全卡 faceIntro 草稿 ≤26；归档 9 件豁免原样）；**#161 机关全集逐卡审计**（10 张 live Trap：healing_spring 非稳定装配 id `fx.6c652d05` 修正为 `trap.healing_spring.heal_on_move`；简单式 `{amount}`/写死数字（捕熊10、刺藤伤害1）升格 `{装配id.键}`；节奏 every/格位 slot 叙事同值双胞胎入库；滚石「破坏」按内核改「移除」；revive_stone 打出物按内核特5改「重生骷髅」；全卡 faceIntro 草稿 ≤26） |
| [ADR-0036](../adr/0036-audio-cue-binding-and-music-ownership.md) | 声音提示与绑定真源；Resources 音频根；MMSoundManager 播放 Adapter；**#170/#179 已落地**唯一期望音乐状态、BGM JSON、Music 轨切歌代数/收口、PerfTrace 排期/取消链路与 EditMode 结构护栏 |
| [ADR-0037](../adr/0037-inspect-detail-is-glossary-rows.md) | 右键详述效果区改词条行：`[[名字]]` 默认展开、`[code]` 图标仅 Inspect hover；告别 `design_text` 堆砌 |
| [ADR-0038](../adr/0038-card-rhythm-dual-channel.md) | 卡级节奏：行动/移动双通道 + 节奏源/周期 + 共享倒计时；攻击模式仅几何；技能同步触发同拍；图标矩阵（**#184 Core/内容/卡面接线已落地**：`CardRhythmRules` / `OnCardRhythmFire` / 怪物 JSON `rhythmSource`+`rhythmPeriod`；机关全量改配另票） |

> ADR-0011–0013 几何、倒计时数学与敌方行动阶段已落地（#81）；**节奏频率与共用计数**以 ADR-0038 为准（部分 supersede）。旧落地方案见 `Assets/Notes/怪物攻击模式与敌方行动阶段-落地方案-2026-07-29.md`（过程笔记，非权威）。
>
> ADR-0030（#135）：正式战斗开局随机三张常规机关（`RegularTrapPool`：Kind=Trap+稀有度 White 池过滤、无放回、种子可复现；离开机关/特殊机关不入池）。
>
> ADR-0031（#136/#137）：属性房改为玩家三选二会话（进房 3 加权候选 → 选 2 → `RunModel.AttributePickDefIds` 本关开局注入，消费后清空）；自动随机注入退役。Core 状态机（#136）与表现接线（#137：`AttributeBoardPresenter` 三候选真卡 + 离开图标场地板，点击经 `RewardChoiceCoreHook` → IntentIntake → Core）均已落地。

## 程序集一览

| 程序集 | 路径 | 职责 |
|--------|------|------|
| `NineGrid.Core` | `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/` | 规则核（QF）| **#139 道具卡归档约定**：`HelpCardDecks`（`deck.help` live / `deck.help_archive` 归档）——非策划现行道具卡（破击锤/血液转换/倍增塔/盾击教程/属性提升/庇佑/瞭望塔）归归档卡组，`RewardPoolQueryExpander.MatchesCard` 与 `ProfessionCatalog.AppendHelpCardsFromDeck` 均排除；**ADR-0033 道具卡稀有度分级投放**：`HelpCardDecks.RegularRarity`（White=常规）——`ProfessionCatalog.SeedItemGenerationRules` 来源池只收常规 White（食品卡/绑票/撞击教程按策划表由 Blue 改 White），特殊（蓝/金/红：金币卡/三宝箱卡/加攻/血量/加甲）仅经商店货架/房间注入/精英池/层主注入等定向渠道；`RewardSystem` 层主击杀固定注入 1 金宝箱卡 + 2 金币卡（#86）；盘面补位由 Core `IBoardStabilizationSystem` 统一裁决 |
| `NineGrid.Content` | `Assets/Scripts/NineGrid.Foundation/NineGrid.Content/` | Catalog：**schema≥2 一卡一文件 JSON 投影** + **tables JSON**（效果模板 / **奖池查询规则** / 经济 / **节点序列抽卡规则** `node_deck_rules`；卡上 `effectAssemblies` 解析进 `Catalog.Effects`；分类三轴 `deckId`/`role`/`tags`+`rarity`；怪物 `sequence`+`level`(普通/层主)；`ContentCatalogBootstrap.Load` 会 Invalidate 表现/模板静态缓存后重读盘，末尾 `RewardPoolQueryExpander`；业务只消费 `GameContentCatalog`）；`Room` 投影字段为 `weight` / `openingInjects` / `rewardPoolId` / `shopOfferCount`（ADR-0022，无 `GoldDelta`）；`TableNineContentCatalog.CreateDefault` 为小型测试夹具；**索引与双写**（#140）：`CardPresentationIndexIO` 扫盘生成 `_index.json`（磁盘为权威，Editor「导出索引」与 `CardPresentationEditorSession.TryExportIndex` 均扫盘、不再依赖会话状态），`ContentHygieneValidator` 校验索引↔磁盘双向一致 / Authoring↔Streaming 字节镜像 / skillIds→技能 JSON / 装配→效果模板 / 模板 body defId / 空壳技能 / 归档（relic/help_archive）正式可达性 / **描述令牌契约与描述格**（#154/#155 / ADR-0035：范围内机关/遗物/道具卡数值只许 `{装配id.键}`，装配缺稳定 id、简单式与 defId 前缀式令牌、令牌键悬空、检查描述/局内模板（`liveTemplate`，可空）/介绍超 26 格均报 `description-contract`；`CardDescriptionTokenRules` 纯函数，归档与范围外卡豁免）+ **倒计时投影**（#156 / ADR-0035：模板 DSL `projectKey` 声明倒计时投影令牌键（契约校验须「本装配id.键」且局内模板必引用），Core 效果触发器推进计数器后经 `EffectCountdownChanged` 广播剩余，表现层 Settled 写卡面快照 `CommittedCountdownRemaining` 并重投影局内描述——首例 `trap.flame`「剩余N次移动后[death]」，`tpl.trap.flame.remove` 的 `every` 升格为装配实参；**#157 倒计时/耐久全集 + Battle/Run 寿命**：`trap.revive_stone` 亦接线（`OnInteract` every 6 → 「剩余N次互动后[death]」，修掉旧简单式 `{count}`）；DSL 新增作用域标记 `scope: "battle"|"run"`（`CountdownScope`，仅作者/系统可见，绝不进入投影键/玩家文本，默认 battle），离战（清关 `CompleteNodeIfCleared` / 战败 `DefeatIfAvatarDeadAction`）由 `ResetBattleScopedCountdownsAction` 把 Battle 作用域计数器复位到阈值并广播剩余（authority+projection 同步重置），Run 作用域跨战斗忠实保留；效果卸载 `DeactivateEffectAction` 广播 `EffectCountdownCleared` → 表现层 `ClearCountdownRemaining` 移除已提交键回退静态）；**七套稳定槽位契约** `ThemeDeckStableMapping` + `ThemeDeckMappingVerifier` / `ThemeDeckFormalReadiness`（#127 / ADR-0029：映射正确 / 正式可达两层校验）+ **逐套窄内容契约** `ThemeDeckNarrowContract`（#128：基础/链接两套，槽位+Boss+槽位卡非 Reserve+牌组登记；#134 起全局正式轮换开放）+ **#134 归档与启用**：七套 `deck_kind` 切 `Unknown` 正式可选、`deck.transition` 表行 `Reserve` + 成员 `isReserve` 归档（保留 JSON，模板仍按 defId 直生；`FormalReadiness` / DeliveryReady 常绿正向门禁）+ **#135 常规机关装填**：`RewardSystem.BuildNodeDeckOptions` 末尾经 `RegularTrapPool`（`NineGrid.Core.Content`）按稀有度 White 过滤抽取三张常规机关（无放回、`IRngUtility` 种子可复现；池不足按池量）；离开机关/特殊机关不入池；QuickTest `\1–\9` 定向注入在此之上叠加，`\0` 与正式镜像一致）+ **旋转套技能权威链路**（#129：怪 JSON `skillIds` → `skill_*.json` 效果装配，不直挂 assemblies）+ **召唤套技能权威链路**（#130：同模式迁移 `deck.insect`，`big_skeleton` 保持仅 attackPattern 全向近战）+ **翻面套技能权威链路**（#131：同模式迁移 `deck.skeleton_legion` 五怪；背面默认惰性——效果带 IsFaceUp 条件或触发点天然不可背面发生，策划显式 OnFlip/OnDeal 翻面边沿触发保留）+ **烈焰套技能权威链路**（#132：同模式迁移 `deck.stone_legion` 五怪）+ **决斗套技能权威链路**（#133：同模式迁移 `deck.void` 五怪，attackPattern=无 显式合法、技能仍经 `skillIds` 挂载——无主动攻击 ≠ 无技能）；**音频素材真源**（#167 / ADR-0036：`Audio/AudioAssetPaths` 固定 Resources 根常量、`AudioAssetManifest` 固定 manifest 模型、`AudioAssetManifestLoader` 按固定键 `Resources.Load` 取用——不扫目录；manifest 与卫生校验由 Editor 工具生成，见 `NineGrid.Content.Editor` 行）；卡牌表现 JSON + **ContentArt** Resources 根（ADR-0008 / ADR-0009 / #69–#71） |
| `NineGrid.Content.Editor` | `…/NineGrid.Content.Editor/` | 卡牌表现编辑器（侧栏 **卡面 / 词条 / 效果池 / 特效库 / 卡组·卡背**：按 `deckId` 分组卡面（含 `deck.trap`）；卡面 **效果装配**默认只挂本卡种同类模板（道具/遗物/怪物技能/机关技能，可搜索；跨类遗留挂载保留标注）；**词条**顶层（`[[名字]]` / `[code]` 图标，升格自描述图标表，[ADR-0037](../adr/0037-inspect-detail-is-glossary-rows.md)）；效果池按四类分组；**特效库**一级分类+变体二级纯预览（`visual_effects.json`，与 DSL 效果池区分）；**卡组·卡背**页预览+翻转（按 deckId 选五套模板壳，空槽保留模板兜底；卡面页翻转按 `deckId` 回填组背））；`ContentArtBreakLinkValidator` 断链扫描（含 **iconPrefab** 预制体解析）+ **内容卫生汇总菜单**（#140：断链 + 索引↔磁盘（含磁盘重复 contentId）+ Authoring/Streaming 双写 + skillIds→技能 + 装配→模板 + 空壳技能 + 归档可达性 + `SkillVisualCatalog.asset` 悬空条目，聚合 `ContentHygieneValidator`）；`VisualEffectsMigrateRunner` 迁 Effects→ContentArt；**音频素材迁移与卫生校验**（#167：唯一 `Resources/audio` 根、SFX/BGM 导入策略、GUID/哈希/LFS/断链报告、隔离区护栏）`AudioAssetMigrationRunner` / `AudioAssetHygieneValidator`；**声音提示声明扫描**（#168：`AudioCueDeclarationScanner` / `Scan Audio Cue Declarations`，重复 cue ID / 空中文音效说明诊断）；**声音绑定调音工作台**（#169：`AudioBindingEditorSession` 磁盘快照+工作副本+指纹脏标记、单条/全部保存与回撤、cue/素材搜索和断链/失败过滤；`AudioBindingEditorWindow` 暖棕 UI、编辑器素材试听、Play Mode 历史定位）；**#178 AI 全量初始绑定**（`AudioAiInitialBinder` / `AudioBindingCatalogHygieneValidator`：草稿/人工确认、默认保留确认、强制重绑、稀疏随机池、未匹配/未使用/异常报告） |
| `NineGrid.Presentation` | `Assets/Scripts/NineGrid.Presentation/` | 表现层（原 Flow+Cards **合并后的单一程序集**）；#168 `IAudioSystem`/`AudioSystem` 安装在 `PresentationSceneRoot` 主菜单生命周期，业务声音经 `TriggerPulseHub` 转发到 `MMSoundManagerAudioPlaybackAdapter`；音频历史暴露稳定 `BindingKey` 供编辑器 Play Mode 定位；**#186** Editor/Development 暴露热调音缝（`ApplyWorkbenchCatalog` / `Suppressed` / `StopSfxSource` / `PreviewWorkbenchBinding` / Sequence+聚合快照，见 [ADR-0036](../adr/0036-audio-cue-binding-and-music-ownership.md)） |
| `NineGrid.Presentation.Editor` | `…/Editor/` | 编辑器工具 |
| `NineGrid.DevTest` | `Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/` | 小键盘 DevKeys；主菜单 QuickTest 入口（`QuickTestEntryInputHandler`）；**无**局内 `\` 调速；`DevTestSceneInstaller` 为 MainScene 唯一序列化的 DevTest 宿主（#126，Release 空壳 / Dev 运行时安装） |

旧程序集 `NineGrid.Flow` / `NineGrid.Cards` 的 **asmdef 已删除**；源码仍以 `Flow/`、`Cards/` **目录 + 命名空间** 共存于 `NineGrid.Presentation` 内（见 presentation.md）。

## 场景入口

```
PresentationSceneRoot (IController)
  └─ PresentationCompositionRoot.Install(PresentationSceneBindings)
        ├─ PresentationRuntimeSystem → PresentationDirector → BattleTimeline
        ├─ BattleSession / Geometry / FieldBattle / GameFlowShell / InputState …
        └─ Controllers 由 SceneRoot / Hook 接线绑定场景 Host
```

主场景：`Assets/Scenes/MainScene.unity`、`Assets/Scenes/UITestSence.unity`。

MainScene 装配卫生（#141）：退役绑定 / 失效 Marker / 隐式 Find 不得回流，详见 presentation.md「MainScene 装配卫生」。

## 维护约定

- 改表现层结构 / 通信范式 → 先改本目录，再改业务。  
- 长期行为不变量 → 写 / 改 `docs/adr/`。  
- 过程笔记 → `Assets/Notes/`（非权威）。
