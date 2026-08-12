# 02 · ADR 索引与代码对照（0001–0043）

> 权威代码事实快照 · 2026-08-12。用途：线上 bug 定位时「从行为规则找代码」。每条 ADR 给出：决策一句话、**已核实的落地代码位置**、关键行为不变量。ADR 原文在 [`docs/adr/`](../../docs/adr/)。
>
> 路径缩写：`P/` = `Assets/Scripts/NineGrid.Presentation/`；`C/` = `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/`；`CT/` = `Assets/Scripts/NineGrid.Foundation/NineGrid.Content/`。

## 表演编排与输出管线

| ADR（状态） | 决策 | 落地代码 | 关键不变量 |
|---|---|---|---|
| [0001](../../docs/adr/0001-battle-presentation-unified-timeline-batch-ack.md) 统一时间线/Batch-ack | 战斗表演收敛为唯一串行时间线；Core 一拍一批、由表演就位回执驱动 | `P/Flow/Presentation/PresentationDirector.cs`、`BattleTimeline.cs`；Core 缝 `C/Presentation/PresentationSyncSystem.cs`、`CoreCommandDispatcher.cs` | 表演未 ack 前 Core 不推进下一批；核永不先行；占格真相唯一 Core `BoardModel`；特效/音效=脉冲不占 ack |
| [0002](../../docs/adr/0002-card-chassis-and-face-templates.md) 单底盘+五卡面 | 一套卡牌底盘 + 玩家/怪物/道具/遗物/机关五套卡面模板按 Kind 显式挂入 L4 | `P/Cards/CardManagerSingleton.cs`（Kind→卡面表）、`CardChassisPaths.cs`、`StandardCardView.cs`（底盘宿主） | 卡面纯消费胖投影；可见提交绑定串行事件流；卡级排序黑盒不被卡面改写 |
| [0003](../../docs/adr/0003-diagnostic-correlation-layer-chainid-choreoseqid.md) 诊断两级关联键（**proposed**） | `chainId`（intent 生命周期罩全连锁）+ `choreoSeqId`（动作内栈式展开） | `P/Flow/Diagnostics/ChoreoTraceContext.cs` 与同目录九套 Recorder/Sink；chainId 分配在 `PresentationDirector` | 埋点 payload 须同带两键；全局可变 batch tag 已废除；`.Forget()` 须补 settle 端 |
| [0005](../../docs/adr/0005-card-face-beat-commit.md) 卡面锚点提交 | 卡面显示值只来自结算指令，在 Impact/Settled 锚点提交，永不直读 Core | `C/Presentation/PresentationEventMap.cs`（Beat 列）+ `PresentationBeat`；`P/Flow/Presentation/BattleBeatScheduler.cs`、`CardFaceStatHandler.cs` | 数值绝对值赋值不累加；无兜底无强制对账；改可见攻的路径必须发有效攻（禁发基础值） |
| [0007](../../docs/adr/0007-unified-presentation-pipeline.md) 统一表现管线 | 排期器升格为多处理器唯一分发出口；装饰（飘字/FX/金币/HUD）共用同一锚点表 | `BattleBeatScheduler` + `IBattleBeatHandler` 实现（`DamageFloaterBeatHandler`、`PlayerInfoHudBeatHandler`、`GoldGainBeatHandler`、`EffectTriggerPulseBeatHandler`）、`P/Flow/Presentation/BattleBeatFlush.cs` | 第一个 TryApply 成功者消费；装饰不 await 进主线 ack；不另建第二张锚点表；EventLog 旁路已删 |
| [0015](../../docs/adr/0015-card-slot-placement-local-space.md) 卡面槽位局部空间 | 主视图 Mask 锚定用祖先无关的局部空间矩阵，禁世界变换主路径 | `P/Cards/Anim/CardMainVisualMaskAnchor.cs`、`P/Flow/BounceFanChoicePresenter.cs` | 祖先 scale=0 期间不提交定位；localPosition 非有限值拒写 |
| [0016](../../docs/adr/0016-card-face-orientation.md) 牌面朝向 | 朝向权威在 Core `FaceUp`；背面双向惰性；翻牌 Settled Commit 全局串行 | `C/Domain/Actions/CoreActions.cs`（Flip/RevealFace）、`C/Domain/FaceDownTickCounters.cs`；`P/Cards/Presentation/CardFaceFlipPresenter.cs`、`P/Flow/Presentation/FlipPlaybackCoordinator.cs` | 背面不可被伤、不开火、被动默认失效、倒计时冻结；`ActiveWhileFaceDown` 仅豁免被动；战斗通道命中后才翻 |
| [0018](../../docs/adr/0018-trigger-visible-causality.md) 触发可见因果 | 效果后果上屏前触发条件必须已可见；Triggered 卡牌效果须持有者最小反馈（v1 缩放） | `C/Domain/Actions/EffectActions.cs`（`ExecuteEffectAction`→`EffectTriggered`）；`P/Flow/Presentation/` 的 `EffectTriggerPulseBeatHandler` 与 `BattleBeatScheduler.FlushImpactExcept` | 同批内核算完 ≠ 可提前播；盘面 Drain 的 Impact 在运动落地后、首个 Remove 前 |

## 输入、命中与落格

| ADR（状态） | 决策 | 落地代码 | 关键不变量 |
|---|---|---|---|
| [0004](../../docs/adr/0004-input-intake-two-axis-gating.md) IntentIntake 两轴门禁 | 输入唯一收口，时序互斥 × 输入所有权两轴裁决；忙时 strict-drop | `P/Systems/IntentIntakeSystem.cs`、`PresentationInputStateSystem.cs`；互斥权威 `PresentationDirector.IsMainlineBusy` | 任何输入路径不得绕过 Intake；忙时不缓冲；门禁内禁壁钟；`OccupancyDesyncLatched` 仅诊断断言 |
| [0006](../../docs/adr/0006-windows-high-polling-mouse-mitigation.md) Win 高回报率鼠标 | Player 内 `RIDEV_NOLEGACY` + 轮询注入 New Input System；命中走轮询 Router | `P/Platform/WindowsHighPollingMouseMitigation.cs`、`P/Flow/PointerHitRouter.cs`、`WorldPointerUtility.cs` | 禁 `OnMouse*`；Editor 禁开 NOLEGACY；`-ng-no-rawinput` 可整体关闭；引擎修复后可退出 |
| [0023](../../docs/adr/0023-slot-hit-frame-and-claim.md) 格位命中框与认领 | 命中权威=场景格位命中框；落格对象向格「认领」语义；棋盘作单一场地面 | `P/Cards/Ground/SlotClaimRegistry.cs`、`P/Cards/GroundFieldHitSurface.cs` | 运行时永不写命中框 size/offset；一格一认领者（冲突=断言）；九框恒开命中恒成功；表面优先级互异、权限不进几何 |
| [0024](../../docs/adr/0024-board-placement-and-prefab-authored-size.md) 落格与预制体尺寸权威 | 落格只对齐世界位置，尺寸权威唯一在预制体，运行时无自动改尺寸 | `P/Flow/BoardSlotWorldPlacement.cs`；战斗侧 `SlotFrameConvergence.SnapHome` | 禁 SetParent 到 `GroundAnchors`；禁运行时写 `localScale` 绝对值；动效倍率必须可还原的相对量 |

## 内容与效果体系

| ADR（状态） | 决策 | 落地代码 | 关键不变量 |
|---|---|---|---|
| [0008](../../docs/adr/0008-single-source-content-and-resources-loading.md) 内容单一权威 | 一卡一文件 JSON 唯一权威；废 Luban 与内容 SO；美术走单一 Resources 根 | `CT/Catalog/ContentCatalogBootstrap.cs`、`CT/CardPresentation/CardPresentationIndexIO.cs`、`P/Cards/Anim/CardAnimFrameSource.cs`；内容 JSON `Assets/Arts/ContentVisual/cards/` | `_index.json` 必须扫盘生成；断链校验是强制代价对冲；禁散建多 Resources 目录 |
| [0009](../../docs/adr/0009-parameterized-effect-templates.md) 参数化效果模板 | 效果=模板+装配引用（实参在卡上）；分类三轴 deckId/role/tags+rarity；词条项目级共享 | `C/Effects/EffectAtomLibrary.cs`（75+ 原子）、`EffectDefinition.cs`；模板/奖池查询 tables JSON 经 `ContentCatalogBootstrap` 装载展开 | 模板不绑定具体卡；一卡一文件看得全；`role`「防御」≠战斗护甲；奖池走查询规则非白名单 |
| [0010](../../docs/adr/0010-self-declared-effect-responsibility.md) 效果责任自陈 | 适用边界由效果自己声明（requires/conditions 两块），外部场域门禁拆除 | `C/Effects/EffectRequires.cs`（`EffectRequiresRuntime`）、`EffectAtomSchemas.cs`、`EffectNonTriggerProbe.cs`（`ProbeWhyNotTriggered`） | requires 不成立=装配错误、conditions 不成立=正常玩法（诊断分级）；同一效果不得因上下文双形态 |
| [0014](../../docs/adr/0014-theme-ids-are-legacy-opaque.md) 主题 ID 不透明 | `contentId`/`deckId`/遗留类名的主题词是历史虚构命名，按不透明主键处理 | 全仓约定；玩家称呼走 `displayName`/另行映射；机制键控硬编码（融合伙伴 ID 等）按本 ADR 保留 | 换皮动 displayName/表现装配，不动逻辑 ID；勿被字面世界观带偏 |
| [0029](../../docs/adr/0029-content-guardrail-stable-theme-deck-mapping.md) 七套稳定槽位契约 | 七套主题卡组 × 序列 1–5 的 contentId 快照契约 + 双层校验 | `CT/Catalog/ThemeDeckStableMapping.cs` + `ThemeDeckMappingVerifier` / `ThemeDeckFormalReadiness` / `ThemeDeckNarrowContract` | 测试禁断言生产 displayName；「映射正确」与「正式可达」分层；#134 起 Reachable 为常绿正向门禁 |
| [0033](../../docs/adr/0033-help-card-rarity-distribution-control.md) 道具卡稀有度投放 | White=常规进随机来源池；Blue/Gold/Red=特殊只走定向渠道 | `HelpCardDecks`（`C/Content/`）、`C/Setup/ProfessionCatalog.cs`（SeedItemGenerationRules）、`C/Systems/RewardSystem.cs` | 宝箱卡只由宝箱房/击杀掉落/遗物效果派发（商店不上架）；存档恢复来源池经 `FilterRegularSourcePool` 过滤 |
| [0035](../../docs/adr/0035-dual-description-projection-and-assembly-param-refs.md) 描述令牌与双套投影 | 描述数值唯一 `{装配id.键}`；检查描述与局内投影同文恒静态；倒计时剩余走专用 UI | `P/Cards/Presentation/CardFaceDescriptionProjector.cs`、`CT/CardPresentation/CardDescriptionTokenRules.cs`；Settled 链 `CardFaceStatHandler`→机关 `ActionCount` / 遗物 `RelicHudHook` 计数 TMP | 描述格硬上限 26；禁 View 直读内核计数器；`liveTemplate` 已退役；遗物 `SourceDefId=relic.*` 优先路由遗物栏 |
| [0037](../../docs/adr/0037-inspect-detail-is-glossary-rows.md) 详述=词条行 | 右键详情效果区只展示检查描述中 `[[词条]]` 抽出的词条行 | `P/Cards/Presentation/CardInspectGlossaryListView.cs`、`CardFaceDescriptionComposer.cs`、`P/Flow/CardInspectOverlayPresenter.cs` | `[[…]]` 按词条名精确匹配；`[code]` 图标默认不进列表仅 hover 解释；不再拼接 `design_text` |

## 战斗规则

| ADR（状态） | 决策 | 落地代码 | 关键不变量 |
|---|---|---|---|
| [0011](../../docs/adr/0011-monster-attack-pattern-intrinsic.md) 攻击模式内生（部分被 0038 取代） | 攻击模式=必填内生几何枚举（正交/斜角/全向/无），不进效果 DSL | 怪物 JSON `attackPattern` → Core 卡数据（`C/Content/ContentDefinitions.cs`/`Domain/CardDraft.cs`）；消费在 `C/Systems/PhaseSystem.cs` 敌方行动 | 缺失=装配错误非默认「无」；首版不可运行时改写；频率条款已废止（归 0038） |
| [0012](../../docs/adr/0012-enemy-action-phase-volley.md) 敌方行动阶段齐射 | 报名（uid 升序冻结名单）→逐条结算（资格复核）→收尾（统一处理死亡），阶段内盘面冻结 | `C/Systems/PhaseSystem.cs`（Register/ResolveNext/ResolveFinale 分拍入口）；表现 `P/Flow/Presentation/EnemyActionPhaseScheduler.cs` | 每只怪一拍一 Batch；玩家死亡阶段立即终止；单向打击不开交战作用域；`OnBattle` 仅交战作用域 |
| [0013](../../docs/adr/0013-action-countdown-unified.md) 倒计时统一（部分被 0038 取代） | 「每 N 次」统一为倒计时（还差几次）；开火窗口一次性，错过重置为 N | Core 计数器设施（`C/Domain/CardRhythm.cs`、Avatar `CounterBag`）；`C/Effects/EffectAtomLibrary.cs` 的 OnSelfMove/OnCumulative | 可被效果加减速不错位；累计计数器不按战斗/节点清除；上卡面必须走结算指令链路 |
| [0017](../../docs/adr/0017-trap-card-kind-and-dual-bucket.md) 机关卡双桶 | `CardKind.Trap` 一等公民；可交战桶（Monster\|Trap）vs 真怪物桶（仅 Monster） | `C/Domain/CardCombatRules.cs`（`IsBoardCombatTarget`/`IsTrueMonster`）；`ContentSystem.ActivateCardEffects` 静默挂 `CounterAttackBanned`；第五套卡面 `P/Cards/CardChassisPaths.cs` | Trap 无击杀赏金；清关条件见 0026；`SelectedCards kind=Monster` 默认=可交战桶 |
| [0028](../../docs/adr/0028-damage-formula-armor-and-reduction.md) 标准伤害公式 | 乘区/平板 → 伤害减免 → 当前护甲吸收（金甲只抵甲伤）→ 扣血；三层护甲 | `C/Domain/CoreEnums.cs`（`RuleId.DamageReduction`）、`C/Domain/Actions/CoreActions.cs`（`DealDamageAction.IgnoreArmor`）、`EffectAtomLibrary` DSL `ignoreArmor` | 卡面甲=当前护甲、HUD 甲=有效护甲；`ModifyBaseStat(Armor)` 同步 CurrentArmor 并发 `ArmorChanged`；无「回合清甲」；文案用「护甲」不用「防御」 |
| [0034](../../docs/adr/0034-board-stabilization-refill-batch-ack.md) 盘面稳定化归 Core | `IBoardStabilizationSystem` 是唯一欠补位裁决；补牌逐切片按 Present ack 锁步 | `C/Systems/BoardStabilizationSystem.cs`；`P/Flow/Presentation/BoardStabilizationScheduler.cs`；`BoardModel.MarkTrapVacated`；条件原子 `SelfNotDealtThisBatch`/`NotInOpeningDeal`（EffectAtomLibrary） | Present 未 ack 不解算下一切片；机关移除空位补牌 `cause=refillAfterTrapRemoval` 不触发补牌型效果；登场批/开局铺场批同免；融合延后抽牌归 Core |
| [0038](../../docs/adr/0038-card-rhythm-dual-channel.md) 卡级节奏双通道 | 行动/移动双内核通道 + 卡级节奏源二选一 + 周期 X + 共享倒计时；同拍开火 | `C/Domain/CardRhythm.cs`（`CardRhythmRules`）、PhaseSystem `OpenCardRhythmFireWindow`（`OnCardRhythmFire`）；怪 JSON `rhythmSource`+`rhythmPeriod` | 同卡模式与技能不得分绑通道；同步技能不再自带 `every`；攻击模式只剩几何 |
| [0039](../../docs/adr/0039-avatar-hp-defeat-invariant.md) HP≤0 必 Defeat | 写血 Action 挂 `DefeatIfAvatarDead` follow-up；终端相位粘性；判死谓词唯一 | `C/Domain/AvatarDefeatFollowUp.cs`（谓词：uid≤0∨查无∨HP≤0，**不看 Zone**）；PhaseSystem `RefreshLegalCommands`；回归 `AvatarVitalityInvariantTests` | 禁「裁决判死、收束不认」；`ChangePhase` 不得用非终端相位覆盖 Defeat；Avatar zone 非法由 StartNode 自愈 |

## 跑图与房间流程

| ADR（状态） | 决策 | 落地代码 | 关键不变量 |
|---|---|---|---|
| [0019](../../docs/adr/0019-avatar-board-walk.md) Avatar 跳格 | 非战斗相位点击目标格走正交逐格 hop；经 IntentIntake `boardWalk` | `C/Domain/AvatarWalkPathfinder.cs`（两阶段 BFS）；`P/Flow/Presentation/AvatarWalkRunner.cs`（含 `IAvatarWalkSystem`） | `InteractionLoop` 禁走；开战前 Avatar 回格 5；途经优先全空、终点可为图标格 |
| [0020](../../docs/adr/0020-board-as-interaction-surface.md) 场地即交互面 | 非战斗选择全部拍平为场上图标/真卡/就地选项；踩图标驻留 1 秒提交；浮层退役 | `P/Flow/RoomIcons/RoomIconBoardPresenter.cs`、`RoomIconDwellSession.cs`；简要解释 `P/Flow/BoardBriefTip/` | 驻留计时在表现侧（门禁内禁壁钟）；导航图标不进玩法 Catalog；房内点选不算九宫格互动 |
| [0021](../../docs/adr/0021-run-progression-in-core.md) 跑图进度归 Core（部分被 0025/0026 修正） | 层/节点/房间编排/通关全是 Core 规则；每层 8 节点、4/7 非战斗 | `C/Models/RunModel.cs`、`C/Domain/MapNodeProgression.cs`；主循环 `P/Flow/GameFlow/GameFlowOrchestrator.RunNodeCycleAsync` | 表现壳 NodeIndex 只是投影；第 3 层节点 8 后 Victory；清关兑金条款以 0025/0026 为准 |
| [0022](../../docs/adr/0022-node-loadout-model.md) 关卡装填模型（部分条款退役） | 怪物侧=主题卡组+序列制（层难度池+层数攻血叠加）；玩家侧=每关按规则重生成；房间=开局注入 | `C/Systems/RewardSystem.cs`（`BuildNodeDeckOptions`）、`NodeDeckRule`；`CT/Catalog/ThemeDeckFloorTierMapping.cs`、`C/Content/MonsterFloorStatScaling.cs` | 每层绑一套未用过的主题卡组；注入不占容量；自动摇房排除属性房（开局上界 7）；携带卡包已退役 |
| [0025](../../docs/adr/0025-item-slots-run-persistent-hold.md) 道具卡格持续持有 | 已入手道具唯一容器=Core ItemSlots；跑图内持续持有；双容量拆开；满则拒入 | `C/Models/PlayerModel.cs`（ItemSlots/双容量）；回收 `RecycleItemSlot`（Core）+ `P/Cards/CardHandManagerSingleton` 拖放回收区 | 清关不兑道具卡格；回收每张 +10；写入一律直达（无中转容器） |
| [0026](../../docs/adr/0026-leave-trap-sole-clear-condition.md) 离开机关唯一清关 | 战斗房唯一清关=击破离开机关卡（门/魔免/离开三技能）；普通房开局编入后半段、层主房击破层主后置顶洗入 | `C/Systems/PhaseSystem.cs`（`IsLeaveTrapBroken`、清关收场）、`RewardSystem.AppendOpeningLeaveTrapCard`、`DeckSystem`（层主洗入）、`LeaveTrapDrawPileRules` | 门只吃交战中玩家出手伤害；魔免=效果管线剔除目标（不改 targeting）；清场扫描不派发 OnRemove 死亡触发；交互不得制造死局；击破当拍禁补牌 |
| [0027](../../docs/adr/0027-relic-drag-recycle-and-rmb-inspect.md) 遗物拖弃+右键详述 | 遗物图标左键拖入共用回收区丢弃（+20 金）；右键=详述 | `P/Flow/RelicManagerSingleton.cs`、`CardInspectOverlayPresenter.cs`；Core `DiscardRelic` | 道具回收与遗物丢弃是两条 Core 命令；宝箱 UseItem 满遗物栏前置拒收不消耗宝箱 |
| [0030](../../docs/adr/0030-regular-trap-opening-loadout.md) 常规机关装填 | 正式战斗开局随机注入三张常规机关（White 池、无放回、种子可复现） | `C/Content/RegularTrapPool.cs`；唯一注入点 `RewardSystem.BuildNodeDeckOptions` | 离开机关与特殊机关绝不入随机池；不借 QuickTest 通道；常规机关不算真怪物 |
| [0031](../../docs/adr/0031-attribute-room-pick-two.md) 属性房三选二（**已废止**，归 0022） | 属性房恢复进房直接开战，开局按 `OpeningInjects` 加权自动注入 2 张 | 现行为在 `RewardSystem.BuildNodeDeckOptions`；遗留 `P/Flow/AttributeBoard/` 不再触发 | 正式流程无三选二会话 |
| [0032](../../docs/adr/0032-item-cards-usable-outside-battle.md) 非战斗可用道具 | 卡级声明 `usableOutsideBattle`；RoomChoice/RewardItemChoice 放行、RoomEvent 不放行 | `C/Content/ItemUseEligibility.cs`；PhaseSystem 三处门禁改卡级裁决；非锁步冲刷 `BattleBeatFlush.PresentEventLogSlice` | 缺省=战斗限定；使用即消耗、满血浪费允许；不推进互动计数 |

## 音频、VFX 与描述之外的表现资产

| ADR（状态） | 决策 | 落地代码 | 关键不变量 |
|---|---|---|---|
| [0036](../../docs/adr/0036-audio-cue-binding-and-music-ownership.md) 声音提示与绑定 | 业务只发稳定 Audio Cue 或提交唯一期望音乐状态；绑定 JSON 单一真源；MMSoundManager 仅作 Adapter | `P/Systems/AudioSystem.cs`、`MusicSystem.cs`、`MMSoundManagerAudioPlaybackAdapter.cs`；`Assets/Resources/audio/audio_bindings.json`/`audio_music.json`；工作台在 `NineGrid.Content.Editor` | 业务禁直调 AudioKit/MMSoundManager（`AudioStructureGuardTests` 护栏）；提示接纳默认必播（可取消排期仅蓄力类）；BGM 一个当前源+至多一个淡出源；玩家音量独立 PlayerPrefs |
| [0040](../../docs/adr/0040-vfx-cue-binding-persistent-state-workbench.md) VFX 提示/绑定/持续状态 | VFX 复用音频四层组织；`vfx_bindings.json` 单一播放决策真源；Persistent Slot 幂等期望状态 | `CT/Vfx/VfxBindingCatalog.cs`；`P/Systems/VfxSystem.cs` + `P/Systems/Vfx/`（sprite-sheet / gold-flight / particle / projectile 四种播放器）；声明位 `P/Flow/Presentation/VfxCue.cs`、`BattleVfxCues.cs`；`Assets/Resources/VFX/vfx_bindings.json` | 运行时 UID/坐标不进绑定主键；独立型 Pulse 接纳后不中断；失败留可归因错误、不播回退表现、不占主线 ack；程序化播放器自治参数 |

## 存档、教学与平台

| ADR（状态） | 决策 | 落地代码 | 关键不变量 |
|---|---|---|---|
| [0041](../../docs/adr/0041-run-save-battle-start-checkpoint.md) 跑图存档 | 颗粒度=战斗开始检查点；快照含 RNG 内部状态；恢复=重建+覆盖+RNG 还原；ES3 桥装配缝 | Core DTO `C/Setup/RunSaveGame.cs`；`P/Flow/GameFlow/RunSave/RunSaveService.cs`、`RunSaveStore.cs`（`IRunSaveStore`+`RunSaveStoreHook`）；`Assets/Scripts/NineGrid.SaveBridge/Es3RunSaveStore.cs` | 检查点在 `BuildNodeDeckOptions` 消耗 RNG 之前捕获；终局清检查点+自动档；QuickTest 局不写档；新增跨战斗持久状态必须进快照并递增 version |
| [0042](../../docs/adr/0042-tutorial-level-module.md) 教学关卡模块 | 教学=独立开局模式（不进节点循环）；保序发牌+四波受控换波；完成标记独立槽 | `P/Flow/Tutorial/`（`TutorialBattleDirector`/`TutorialDeckPlan`/`TutorialProgressStore`/`TutorialRunOptions`/`TutorialWaveCommands`）；`GameFlowOrchestrator.RunTutorialAsync`；Core 缝 `NodeDeckOptions.PreserveDealOrder`（`C/Domain/Actions/BoardDeckActions.cs`）；内容 `deck.tutorial`（`trap.tutorial.*`，rarity None） | 正式流程禁引教学内容/禁置 PreserveDealOrder；教学卡禁改 White；战败不标记完成、不删玩家自动档；波次切换不绕过主线 |
| [0043](../../docs/adr/0043-steam-platform-bridge.md) Steam 平台桥 | 成就/统计/云存档经装配缝接入；Steam 实现隔离在桥程序集；平台缺位零影响 | 抽象 `P/Flow/Platform/`（`IPlatformAchievements`/`IPlatformInfo` + Hook + 门面 + `AchievementIds`/`StatIds`）；实现 `Assets/Scripts/NineGrid.SteamBridge/`（7 文件，见 [`Platform桥接/`](Platform桥接/README.md)） | 业务程序集禁 `using Steamworks`；Init 失败静默降级；云=本地后端装饰器、冲突新者胜；AppId 单点（当前 480 占位） |

## 状态速查

- **proposed**：0003（诊断关联层，方向已定、落地程度以代码为准）。
- **已废止 / superseded**：0031（归 0022）。
- **部分被取代**：0011、0013（频率与计数条款归 0038）；0017、0021、0022 的个别条款被 0025/0026 修正（表中已注明）。
- 其余均 accepted 且与当前代码核对一致。
