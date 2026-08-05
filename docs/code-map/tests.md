# Presentation Tests Map

程序集：`NineGrid.Presentation.Tests`  
路径：`Assets/Scripts/NineGrid.Presentation/Tests/`

## 分层

| 目录 | 保护什么 |
|------|----------|
| `BehaviorBaseline/` | Busy / BatchAck / EventLogCommit / 场地双向索引等行为基线 |
| `Explore/` `Attack/` `Pickup/` `UseItem/` `Fusion/` `Drain/` `Shuffle/` `Zone/` | 垂直切片（意图 → Command → Director） |
| `Lifecycle/` | 卡实体生命周期 / Handoff |
| `FieldGeometry/` | Geometry / FieldBattle System（含无 Instance 护栏） |
| `FlowShell/` | 流程壳 Controller；**WalkSandboxRetirementStructuralTests**（#91：无跳格沙盒 API；`\0`=流程测试 Sequential）；**InRoomBoardWiringStructuralTests**（图标进房后须接 `PresentInRoomSessionAfterEnterAsync`，禁死代码 `PlayRoomEventAsync`；房内会话禁整段 `ChoiceOverlay`） |
| `BattleSession/` | 局内会话；**NodeSettlementReadiness**（#83 / ADR-0021：清关 RoomChoice 须唤醒主循环）；**ForceNodeVictoryStructuralTests**（跳过战斗不得再 Offer help.choice） |
| `Output/` | 描述 / 伤害等输出 |
| `Cards/` | 卡面 Commit、牌库闸、飞行排序、致死表现回归；**CardDisplayModeVisualsTests**（#104：mode 倍率相对预制体基准且可还原）；**DealFlightRemoveCancelContractTests**（同批 Deal→Remove 须取消飞牌，防 choreo 泄漏 / Pickup Buffered）；表现层配置器条目门槛（Skill 不进窗口、HelpCard/Item 保留）+ **空装配清 effectIds / 挂装配投影 / 效果分类标注（道具/遗物/怪物技能/机关技能）/ 同类多载过滤 / skillIds→assemblies 展开**（解耦装配 IA）+ **装配描述自动同步/自定义锁定 / design_text→{param} 参数化** 等；**ContentArt** 路径约定 / Resources 帧加载 / 断链校验（#66）；**JSON→Catalog 投影**（帮助卡/怪物/技能/遗物/牌组/房间）+ 表 JSON（效果模板 + 装配引用，#67–#70、ADR-0008/0009）；**词条/`{param}`/详情合成**（#71）；**右键详述 live 挂载优先**（空白板 JSON + 局内注入技能 / 忽略预设 skillIds）；**Disable Domain Reload 下 Catalog 重绑**；**装配 argsJson 缺占位实参拦截 / 选模板建议实参**；**特效库** `visual_effects.json` 扫描/粘性合并/atlas 帧加载（与 DSL 效果池区分；纯预览阶段） |
| `Flow/` | Flow 侧遗留/切片；**BoardBriefTipCopy / Session**（#89）；**BoardPlacementStructuralTests**（#104+#105 / ADR-0024：生产全集禁 Fit / 禁 parent 到 GroundAnchors / 落格路径禁写 `localScale` / 无 `slotHitBoxSize`）；**ShopBoardSlotResolver**（#92）；**TavernBoardSlotResolver**（#93）；**RewardBoardSlotResolver**（#94）；**InRoomLeaveWatchStructuralTests**（离开监视 Active 顺序 / 扣金 HUD / SoftBlockOnly；选项与真卡均预制体尺度、禁 Fit）；**InRoomItemAcquirePresentationStructuralTests**（ADR-0025：购领须接手 ItemSlots→手牌，禁只碎裂货架）；**BounceFanChoiceHitTests**（固定 AABB 命中 / 禁 OverlapPoint 回流）；**DirectionalBiasPickerTests**（同层过场四向袋洗牌） |
| `Fixtures/` | EditMode 夹具 |
| `HostContractStructuralTests.cs` | 结构护栏：禁四大旧宿主名、禁 `CombatHitSink`、禁回流 `new PresentationDirector`、System 不暴露具体 View |
| `BuildSceneMissingScriptStructuralTests.cs` | 构建护栏（#126 清理）：构建启用场景 + 非 Plugins 预制体禁引用无法解析的 m_Script（Missing Script，含 fileID-only 破坏引用）；防已删除脚本组件残留回流 |
| `DevTestSceneSerializationStructuralTests.cs` | 构建护栏（#126）：构建启用场景禁序列化 DevTest-only 组件（`#if UNITY_EDITOR||DEVELOPMENT_BUILD`，Release 即 Missing Script）；MainScene 须恰好序列化 `DevTestSceneInstaller`（始终编译的运行时安装宿主） |
| `RoomChoiceRetirementStructuralTests.cs` | 结构护栏（#90）：无 `RoomChoicePresenter`；`IGameFlowView`/`SelectorManager`/`UiPanelRouter` 无房间浮层 API；无属性三选一 Bounce 入口；BounceFan 仍在 |
| `InRoomBoardWiringStructuralTests.cs` | 结构护栏：`PlayRoomIconChoiceAsync` 进消费/特殊房后须调用 `PresentInRoomSessionAfterEnterAsync`；无独立 `PlayRoomEventAsync` 二次 Enter |
| `WalkSandboxRetirementStructuralTests.cs` | 结构护栏（#91）：无 `WalkSandbox`/`StartWalkSandboxNode`；`\0` 流程测试通道（空技能/Sequential） |
| `IntentIntakeStructuralTests.cs` | 结构护栏（#52）：输入路径须经 IntentIntake（含 RevealFace）；门禁/收口决策禁用壁钟；ADR-0004 accepted；回收/丢遗物须 `PresentEventLogSliceOnly(UpdateGold)`（ADR-0007） |
| `PointerInputStructuralTests.cs` / `PointerHitRouterTests.cs` / `GroundFieldHitSurfaceTests.cs` / `PointerHitSurfacePriorityTests.cs` / `SlotClaimRegistryTests.cs` / `SlotClaimStructuralTests.cs` / `WildPickPathStructuralTests.cs` | ADR-0006 / ADR-0023 / #101–#103+#105：禁 HitProxy `OnMouse*`；场地面解格号；停写格位 size/offset；表面优先级互异与同分装配错误；一格一认领；跨格 `RefreshPointerHover`；移格起飞卸认领/落地登记；禁 `BoardWalkSlotHitPolicy` / Avatar `int.MinValue` / 落格自建 collider 回流；禁手牌 `OverlapPointAll` / `TryPickCollider` / HUD z=0 换算 / legacy `OnMouse*`；mitigation 标记 |
| `CardFaceBeatStructuralTests.cs` | 结构护栏（#55–#62）：禁石头爱好者卡面提前同步；攻击/反击命中帧须报 Impact 且不得 Sync/SpawnDamagePopups；PresentStep 通道前 FlushUpdateFaceUp、Idle 门控、ack 前 FlushBeats；战斗 Present `flushFaceUpBeforeBegin: false`；Handler 禁 Forget PlayFlipAsync、须 Enqueue Coordinator；用道具 Present 禁 CommitAllSpawnedCards、须 Vacate 前报 Impact；探索/用道具批次投影禁写卡面数值；禁 JSON stats 盖写；首次 ApplyToManagedCard 禁 TryRead；Handler 消费 Spawn/Deal/Avatar/OfferReward；Bounce 禁 `clearCombatStats` 数值旁路；CommitPresentation 生产调用方白名单（含 `CardFaceFlipBeatHandler`）；MarkFieldDead 禁直置零；ApplyKill 取 RemainingHp；底盘数值 Setter 非公开；禁 `PresentEffectTriggersFromEventLog` / `SpawnDamagePopups` / `PresentGoldGainsFromEventLog`；组合根注册飘字/FX/金币/Avatar HUD/翻牌朝向处理器并接线 FlushUpdateFaceUp；排期器禁 SyncFromCore；ADR-0005/0007 交叉引用；排期器多 `IBattleBeatHandler` |
| `FlipPlaybackCoordinatorTests.cs` | ADR-0016：串行入队不丢翻；PresentStep FaceUp 未 Idle 前不 Begin；战斗延后 FaceUp 不前置 Flush；FlushUpdateFaceUp 只消费 FaceUp |

### ADR-0023 / ADR-0024 结构护栏（#101–#105 已落地）

| 护栏 | 测试 | 保护什么 |
|------|------|----------|
| 落格 collider | `SlotClaimStructuralTests` | 落格对象不得自建 `Collider2D` / `EnsureCollider`；不得再注册 Router |
| 格位框 size/offset | `PointerInputStructuralTests` | 运行时不得写格位命中框 `size` / `offset`；无 `slotHitBoxSize` 覆盖 |
| 认领唯一性 | `SlotClaimRegistryTests` | 一格一认领；冲突保留先到；成对登记/注销 |
| 表面优先级 | `PointerHitSurfacePriorityTests` | `PointerHitSurfacePriorities` 互异；Router 同分 Error |
| 命中恒成功 | `SlotClaimStructuralTests` | 无 `BoardWalkSlotHitPolicy` / 软占关框 / Avatar `int.MinValue` 穿透回流 |
| 野生拾取路径 | `WildPickPathStructuralTests` | 无 `TryPickCollider` z=0、无 legacy `OnMouse*`；HUD / 主菜单走平面 Overlap |
| 落格父级 | `BoardPlacementStructuralTests` | 生产全集禁 parent 到 `GroundAnchors`；Presenter 禁 `Instantiate(prefab, parent\|anchor)` |
| 运行时缩放 | `BoardPlacementStructuralTests` + `CardDisplayModeVisualsTests` + `InRoomLeaveWatchStructuralTests` | 禁 Fit API / `slotHitBoxSize`；落格路径禁写 `localScale`；mode 倍率相对预制体基准且可还原；选项与真卡均预制体尺度 |

### Core 契约护栏（`NineGrid.Core.Tests`）

| 测试 | 保护什么 |
|------|----------|
| `PresentationEventMapBeatExhaustivenessTests` | #54/#57/#60/#61/#62：每个 `CoreEventType` 须有表演映射与显式 `PresentationBeat`；`None` 必须带理由；观察型 `BaseStatModified` 不得落在 `Impact`；`CardDealt`/`AvatarAppeared`/`CardFaceChanged` 为 Settled；`DamageDealt`/`EffectTriggered` 为 Impact；`GoldModified`/`RewardOffered` 为 Settled |
| `CardFaceOrientationTests` | ADR-0016：Flip/Reveal 改 FaceUp 并发 `CardFaceChanged`；攻击拒背面；背面怪不 −1/不开火；`DealDamage`/爆弹类不中背面；`FaceDownTickCounters` 注册/Tick/查询；OnFlip/Flip/IsFaceUp 原子与 `ActiveWhileFaceDown` token |
| `TrapKindDualBucketRegressionTests` / `Batch2HardSkillsRegressionTests`（复活石） / `TrapBatch2RegressionTests` / `LeaveTrapDoorLeaveContractTests` / `LeaveTrapInsertContractTests` / `LeaveTrapClearContractTests` | ADR-0017：`CardKind.Trap` 双桶；爆弹/`SelectedCards` 指向伤（飞刀等）可伤 Trap；绑架 `trueMonsterOnly` 仍仅真怪；清关/赏金排除；静默 `CounterAttackBanned`；复活石 Kind/deck/container 迁正；批2 滚石/捕熊/烈焰语义与 Help 迁徙；**#111 / ADR-0026**：离开机关 `trap.leave` + 门/魔免/离开（交战可伤、非交战/直接移除无效、魔免挡外来效果含传送、击破置清关标志、无赏金）；**#112 / ADR-0026**：默认 ⌈N/2⌉ 后 `ShuffleIntoDrawPile(trap.leave)`（N 不含机关、奇数上取整、幂等）；层主房改击破开局层主；经补牌上场；**#113 / ADR-0026**：`IsNodeCleared`=`IsLeaveTrapBroken`；真怪清零不清关；击破离开机关清关；清场不兑金；道具卡格保留；QuickTest 置标志跳关；**清关后 `ResolvePostKillFill` 禁止补牌**（防 Deal 飞行卡死主线） |
| `BaseStatModifiedResultValueTests` | #54：`BaseStatModified` 携带结算后 `ResultValue`，且保留 `Amount=StatId` / `Delta` 增量约定 |
| `PermanentAttackFaceCommitTests` | Permanent 有效攻卡面旁路：Apply 发 BaseStatModified；Temporary 不上屏；Swap 条件失效回基值 |
| `CardSpawnedFaceAbsoluteTests` | #57：`CardSpawned` 携带造卡时攻/甲/血绝对值 |
| `CardKilledFaceAbsoluteTests` | #58：`CardKilled` 携带 `RemainingHp=0` |
| `RewardOfferFaceProjectionContractTests` | #62：`OfferRewardChoice` 把 CreateDraft 攻/甲/血写入 `RewardEntry` 与 `RewardOffered` Message |
| `RewardPoolQueryContractTests` / `RewardPoolDiversityContractTests` | #71：奖池查询规则展开、role 均衡、稀有度分层抽取 |
| `RelicArchiveDeckContractTests` | #115：`deck.relic_archive` 九件错位旧卡不进 common_chest / blood_conversion 展开；Profession/开局不授予归档遗物 |
| `RelicR1NineProfessionContractTests` | #116：九件新建遗物在役卡组+装配；`relic.rotten_cleave_axe` 为 Red 且不进 W/B/G 宝箱池；Profession 授予顺劈斧且基础护甲 0；样本效果（铁盾伤害减免、超越维度战斗旋转） |
| `RelicR1AssemblableContractTests` | #117：13 件可拼新建遗物在役卡组+装配；按稀有度进入 common_chest / blood_conversion；样本效果（废物剑击杀回血、血液暴力半血攻、复合盔甲关初换甲） |
| `RelicR1KeeperAuditContractTests` | #118：20 件名称对齐留用遗物在役+非空描述+装配；渴望 MaxHp+10；幸运硬币仅层主击杀；废物三件 `OnAnyHelpCardUsed` 与利用机回血样本 |
| `RelicR2CumulativeContractTests` | #119：OnCumulative 扩展 `monsterRemoved` / `helpCardUsed` / `damageTaken`（含甲吸收）；六件累计遗物在役+奖池+装配；恐怖面罩只移普通等级怪；血魔承伤涨上限 |
| `RelicR3GrowthContractTests` | #120：`RelicRunContribution` run 内成长；锻造器具关初扣甲成长；金剑战斗衰减/击杀成长/下限 0；新 run 清空；丢弃清贡献 |
| `RelicR3FoamContractTests` | #121：泡沫盔甲 +1 基础甲；本关首次甲归零后武装下一击 Once 免疫（归零击不吃盾）；同关不重武装；多段只免第一段；未消耗盾关初清除 |
| `ThemeMonsterDeckContractTests` | #86 / ADR-0022：每层主题卡组不重复绑定、节点按序列 1–5 抽、Reserve 不参与、层主击杀固定 1 金箱+2 金币 |
| `ThemeDeckStableMappingTests` | #127 / ADR-0029：七套 × sequence 1–5 稳定槽位契约（`ThemeDeckStableMapping`）与生产 JSON 逐槽比对；重复 sequence / 缺槽 / 槽位错位 / 越界成员 / 错误 Boss / 错误 Reserve 由 `ThemeDeckMappingVerifier` / `ThemeDeckFormalReadiness` 明确报出（含坏夹具用例）；映射正确（绿）与正式可达（当前红）分层 |
| `ContentDisplayNameAssertGuardrailTests` | #127 / ADR-0029：结构护栏——测试源码禁断言生产内容 displayName（`string.IsNullOrEmpty` 非空检查豁免；白名单仅限自建夹具与代码常量文件） |
| `MonsterLoadoutPresentationValidatorTests` | 过渡卡组 staging 校验绿；交付就绪在怪物仍挂 `deck.transition` 时必须失败 |
| `MapNodeProgressionContractTests` | #84 / ADR-0021 / #107 / #113：8 节点编排全表（节点 7=层主房图标战前缓冲）、非战斗不进 InteractionLoop、进层主房图标推进至节点 8、清关跳过 help.choice（经离开机关标志）、道具卡格清关不兑不清+清场残留、困难房门槛、第 3 层节点 8 通关 |
| `ShopBuyGoldContractTests` / `ShopSessionContractTests` / `ShopItemSlotsUpgradeContractTests` | #92 / #108 / #109：商店四货架 + 未满级升级项、购买留店扣金直写道具卡格、格升级 50 金且不改 `ItemDeckCapacity`、满 5 隐藏、刷新翻倍、离开、余额/格满拒买 |
| `TavernSessionContractTests` | #93：卡店三项服务、扩容/强化扣费留店、刷新翻倍、离开、道具卡固定二级选择确认/取消、余额不足拒买 |
| `SpecialRewardSessionContractTests` | #94 / #108：宝箱奖励 4 卡 / 道具奖励 5 卡、免费拿直写道具卡格留房、满格拒领、离开放弃不加 skip 金 |
| `RoomOpeningInjectContractTests` | #95 / ADR-0022：五种战斗房开局注入（固定/权重可重复/不可重复）、困难房怪物侧序列 3+4、注入顺序（固定→房间）、RandomBattle 开局分房 |
| `ItemSlotsDirectGrantContractTests` | #108 / ADR-0025：商店/特殊房直写道具卡格、无携带开局注入、满格拒拾、默认容量 3 |
| `ItemSlotsRecycleContractTests` | #110 / ADR-0025：道具卡格回收 +10 金并移除；商店相位合法；Apply 跳过表演锁门禁；**非战斗相位（RoomChoice/RoomEvent/RewardItemChoice）禁 UseItem、仍可回收** |
| `RelicInventoryEconomyContractTests` | #98：遗物栏上限 12、满栏 SelectReward 拒收保留 Pending、DiscardRelic +20、宝箱 SkipRelicChoiceGold +20 |
| `CarryPackPreserveBootstrapContractTests`（Presentation `BattleSession/`） | #107 / #108：`BootstrapRun(preserveRunInventory)` 道具卡格与容量存活；无 preserve 则清空 |
| `NodeStartRelicCardGrantTests` | 关卡开始遗物加卡：飞刀进抽牌堆、交换按钮进道具卡格；满格静默丢弃不兑金 |
| `ContentCatalogValidationTests` | 小型夹具 `ValidateCatalog` 绿；生产 Bootstrap（模板表 + 装配引用解析 + schema≥2 JSON 投影 + 奖池查询展开 + 节点序列规则）校验绿；跨容器共享模板不同实参（#70 / ADR-0009） |
| `EffectTemplateAssemblyContractTests` | #70：取消 typeTag/verb 门禁；`requires` 解析；装配实参替换与跨容器共享模板 |
| `EffectSelfDeclarationContractTests` | #72 / ADR-0010：requires 校验（未知 token / mount 错配 / 缺声明）；拒上下文开关旧形；生产卡挂载显式场景声明审计归零；`ValidateCatalog` 绿 |
| `CardOwnedTriggerScopeGateRegressionTests` / `CardZoneDeckGateRegressionTests` | #73 / ADR-0010 Phase C：外部门禁拆除后自陈等价（无 `EffectOwnerScopeGate` / 区域门禁回流）；帮助卡互不误触、亡语互不误触 |
| `EffectNonTriggerProbeTests` | #73 / ADR-0010 Phase D：未触发探查区分 requires vs conditions；与正向诊断层并存 |
| `InteractionCountDecoupleTests` | #75：未击杀交战/拾卡/点空格推进 interactionCount；道具使用不计；计数/补牌/旋转可分步 |
| `ActionCountdownSemanticsTests` | #76 / ADR-0013：OnSelfMove / OnCumulative 倒计时拍序；OnInteract.every；外部加减不永久错相；`AttackPatternPrefix` 与 `effect.` 隔离 |
| `SlotIdDiagonalAdjacencyTests` | #77 / ADR-0011：对角相邻谓词；正交 `IsAdjacentTo` 语义不变 |
| `AttackPatternDataPlaneTests` | #77/#82/#86 / ADR-0011：五取值与频率；缺省报错；显式「无」通过；进场倒计时初始化；生产 Catalog 按主题序列回填后的攻击模式赋模 |
| `OnBattleEngagementScopeTests` | #78 / ADR-0012：OnBattle 仅交战作用域；非交战 DealDamage 不触发、不洗 UntilBattleEnds；「每战斗」只数交战 |
| `DamageFormulaRegressionTests` | ADR-0028：伤害减免在乘区/平板之后减去；无视护甲跳过甲与金甲；减免+无视甲组合；金甲仅抵甲伤段；FlatDelta→减免顺序 |
| `EnemyActionPhaseTests` | #79/#80 / ADR-0011–0012：敌方行动报名/逐条/收尾；四开火模式位置×频率；窗口错过重置；Avatar 偏心真对角；盘面冻结；玩家死亡终止；名单冻结；反伤致死伤害仍成立；「无」不开火 |
| `EnemyActionVolleyIntentTests` / `CardPresentationCommitTests`（ActionCount / Action_Icon） | #81：倒计时事件→指令 Commit；Action_Icon 模板兜底；导演单向打击走 Counter 分拍 |

### 行为基线（节选）

| 测试 | 保护什么 |
|------|----------|
| `CardFaceBeatCommitBaselineTests` | #55–#62：攻击/反击 Present 命中后护甲已变、观察型加攻仍旧，收尾后才变；用道具解算后卡面仍旧、Present 收尾才提交；四条剧本均报 Settled；handler 不读内核；SpawnCard Settled 后卡面等于指令绝对值而非 JSON 出生值；KillCard 取 RemainingHp；Settled 漏接 Impact 值不动；Avatar HpChanged 在 Impact 后按指令刷新 PlayerInfo HUD；金币 Settled 才飞币；非锁步 PresentEventLogSlice 消费金币；多处理器认领 / 无人认领 Settled 诊断；飘字/FX 装饰处理器在 Impact 消费指令；OfferReward Settled 后 Bounce 负 uid 卡面等于指令投影 |

## 关闭门槛（普通实施票）

1. 受影响 EditMode 绿  
2. `unity command recompile` 后 Console 无新增 Error / Exception / Assert  

全量真实场景 PlayMode 终验是 Spec 级门禁（历史上由 #42 承接），不属于每张实施票的默认门槛。

### Map #122（主流程正式接线）验证边界

- 商店 / 卡店 / 特殊奖励房主循环与节点编排已接线，本 Map 内的验证边界为 **EditMode**：契约测试（`ShopSessionContractTests` / `TavernSessionContractTests` / `SpecialRewardSessionContractTests` / `MapNodeProgressionContractTests` / `InRoomBoardWiringStructuralTests` / 各 `*BoardSlotResolverTests`）覆盖 Core 会话、清关后放图标与房内场地板接线。
- 正式 / QuickTest 隔离（#125）：`GameFlowRunModeContractTests` 契约正式入口无 QuickTest 标志/作弊/动态装配（`GameFlowRunOptions.CreateFormal` 无载荷、正式 BeginRun 无 RunTag/无技能机关列表），`\0` 仍具镜像与作弊标志（Sequential 节点序 + HP99/ATK5 + QuickTest RunTag）；结构性断言 `TestMode` 布尔与 `(bool,bool)` 组合构造已删除。
- 完整一局（主菜单开始 → 3 层 × 8 节点 → 胜利/失败）的**真实运行 PlayMode 终验**属本 Map 最终门禁（`#142` 自动烟雾 + `#143` 人工终验），当前未运行前**不得宣称已通过**；tests.md 不写入尚未运行的 PlayMode 结果。

## 跑测（Unity CLI）

```bash
unity command recompile --project-path "<repo>"
unity command run_tests --mode editor --filter <NameOrNamespace> --project-path "<repo>" --format json
```
