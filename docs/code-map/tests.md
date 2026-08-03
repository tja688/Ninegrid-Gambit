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
| `FlowShell/` | 流程壳 Controller；**WalkSandboxRetirementStructuralTests**（#91：无跳格沙盒 API；`\0`=流程测试 Sequential）；**InRoomBoardWiringStructuralTests**（图标进房后须接 `PresentInRoomSessionAfterEnterAsync`，禁死代码 `PlayRoomEventAsync`） |
| `BattleSession/` | 局内会话；**NodeSettlementReadiness**（#83 / ADR-0021：清关 RoomChoice 须唤醒主循环）；**ForceNodeVictoryStructuralTests**（跳过战斗不得再 Offer help.choice） |
| `Output/` | 描述 / 伤害等输出 |
| `Cards/` | 卡面 Commit、牌库闸、飞行排序、致死表现回归；**DealFlightRemoveCancelContractTests**（同批 Deal→Remove 须取消飞牌，防 choreo 泄漏 / Pickup Buffered）；表现层配置器条目门槛（Skill 不进窗口、HelpCard/Item 保留）+ **空装配清 effectIds / 挂装配投影 / 效果分类标注（道具/遗物/怪物技能/机关技能）/ 同类多载过滤 / skillIds→assemblies 展开**（解耦装配 IA）+ **装配描述自动同步/自定义锁定 / design_text→{param} 参数化** 等；**ContentArt** 路径约定 / Resources 帧加载 / 断链校验（#66）；**JSON→Catalog 投影**（帮助卡/怪物/技能/遗物/牌组/房间）+ 表 JSON（效果模板 + 装配引用，#67–#70、ADR-0008/0009）；**词条/`{param}`/详情合成**（#71）；**右键详述 live 挂载优先**（空白板 JSON + 局内注入技能 / 忽略预设 skillIds）；**Disable Domain Reload 下 Catalog 重绑**；**装配 argsJson 缺占位实参拦截 / 选模板建议实参**；**特效库** `visual_effects.json` 扫描/粘性合并/atlas 帧加载（与 DSL 效果池区分；纯预览阶段） |
| `Flow/` | Flow 侧遗留/切片；**BoardBriefTipCopy / Session**（#89）；**RoomIconVisualFit**（场地图标落格压缩放）；**ShopBoardSlotResolver**（#92）；**TavernBoardSlotResolver**（#93）；**RewardBoardSlotResolver**（#94）；**InRoomLeaveWatchStructuralTests**（离开监视 Active 顺序 / 扣金 HUD / 选项 Fit） |
| `Fixtures/` | EditMode 夹具 |
| `HostContractStructuralTests.cs` | 结构护栏：禁四大旧宿主名、禁 `CombatHitSink`、禁回流 `new PresentationDirector`、System 不暴露具体 View |
| `RoomChoiceRetirementStructuralTests.cs` | 结构护栏（#90）：无 `RoomChoicePresenter`；`IGameFlowView`/`SelectorManager`/`UiPanelRouter` 无房间浮层 API；无属性三选一 Bounce 入口；BounceFan 仍在 |
| `InRoomBoardWiringStructuralTests.cs` | 结构护栏：`PlayRoomIconChoiceAsync` 进消费/特殊房后须调用 `PresentInRoomSessionAfterEnterAsync`；无独立 `PlayRoomEventAsync` 二次 Enter |
| `WalkSandboxRetirementStructuralTests.cs` | 结构护栏（#91）：无 `WalkSandbox`/`StartWalkSandboxNode`；`\0` 流程测试通道（空技能/Sequential） |
| `IntentIntakeStructuralTests.cs` | 结构护栏（#52）：输入路径须经 IntentIntake（含 RevealFace）；门禁/收口决策禁用壁钟；ADR-0004 accepted |
| `PointerInputStructuralTests.cs` / `PointerHitRouterTests.cs` | ADR-0006：禁 HitProxy `OnMouse*`；指针缝 / HitRouter 行为；mitigation 标记 |
| `CardFaceBeatStructuralTests.cs` | 结构护栏（#55–#62）：禁石头爱好者卡面提前同步；攻击/反击命中帧须报 Impact 且不得 Sync/SpawnDamagePopups；PresentStep 通道前 FlushUpdateFaceUp、Idle 门控、ack 前 FlushBeats；战斗 Present `flushFaceUpBeforeBegin: false`；Handler 禁 Forget PlayFlipAsync、须 Enqueue Coordinator；用道具 Present 禁 CommitAllSpawnedCards、须 Vacate 前报 Impact；探索/用道具批次投影禁写卡面数值；禁 JSON stats 盖写；首次 ApplyToManagedCard 禁 TryRead；Handler 消费 Spawn/Deal/Avatar/OfferReward；Bounce 禁 `clearCombatStats` 数值旁路；CommitPresentation 生产调用方白名单（含 `CardFaceFlipBeatHandler`）；MarkFieldDead 禁直置零；ApplyKill 取 RemainingHp；底盘数值 Setter 非公开；禁 `PresentEffectTriggersFromEventLog` / `SpawnDamagePopups` / `PresentGoldGainsFromEventLog`；组合根注册飘字/FX/金币/Avatar HUD/翻牌朝向处理器并接线 FlushUpdateFaceUp；排期器禁 SyncFromCore；ADR-0005/0007 交叉引用；排期器多 `IBattleBeatHandler` |
| `FlipPlaybackCoordinatorTests.cs` | ADR-0016：串行入队不丢翻；PresentStep FaceUp 未 Idle 前不 Begin；战斗延后 FaceUp 不前置 Flush；FlushUpdateFaceUp 只消费 FaceUp |

### Core 契约护栏（`NineGrid.Core.Tests`）

| 测试 | 保护什么 |
|------|----------|
| `PresentationEventMapBeatExhaustivenessTests` | #54/#57/#60/#61/#62：每个 `CoreEventType` 须有表演映射与显式 `PresentationBeat`；`None` 必须带理由；观察型 `BaseStatModified` 不得落在 `Impact`；`CardDealt`/`AvatarAppeared`/`CardFaceChanged` 为 Settled；`DamageDealt`/`EffectTriggered` 为 Impact；`GoldModified`/`RewardOffered` 为 Settled |
| `CardFaceOrientationTests` | ADR-0016：Flip/Reveal 改 FaceUp 并发 `CardFaceChanged`；攻击拒背面；背面怪不 −1/不开火；`DealDamage`/爆弹类不中背面；`FaceDownTickCounters` 注册/Tick/查询；OnFlip/Flip/IsFaceUp 原子与 `ActiveWhileFaceDown` token |
| `TrapKindDualBucketRegressionTests` / `Batch2HardSkillsRegressionTests`（复活石） / `TrapBatch2RegressionTests` | ADR-0017：`CardKind.Trap` 双桶；爆弹/`SelectedCards` 指向伤（飞刀等）可伤 Trap；绑架 `trueMonsterOnly` 仍仅真怪；清关/赏金排除；静默 `CounterAttackBanned`；复活石 Kind/deck/container 迁正；批2 滚石/捕熊/烈焰语义与 Help 迁徙 |
| `BaseStatModifiedResultValueTests` | #54：`BaseStatModified` 携带结算后 `ResultValue`，且保留 `Amount=StatId` / `Delta` 增量约定 |
| `PermanentAttackFaceCommitTests` | Permanent 有效攻卡面旁路：Apply 发 BaseStatModified；Temporary 不上屏；Swap 条件失效回基值 |
| `CardSpawnedFaceAbsoluteTests` | #57：`CardSpawned` 携带造卡时攻/甲/血绝对值 |
| `CardKilledFaceAbsoluteTests` | #58：`CardKilled` 携带 `RemainingHp=0` |
| `RewardOfferFaceProjectionContractTests` | #62：`OfferRewardChoice` 把 CreateDraft 攻/甲/血写入 `RewardEntry` 与 `RewardOffered` Message |
| `RewardPoolQueryContractTests` / `RewardPoolDiversityContractTests` | #71：奖池查询规则展开、role 均衡、稀有度分层抽取 |
| `ThemeMonsterDeckContractTests` | #86 / ADR-0022：每层主题卡组不重复绑定、节点按序列 1–5 抽、Reserve 不参与、层主击杀固定 1 金箱+2 金币 |
| `MapNodeProgressionContractTests` | #84 / ADR-0021：8 节点编排全表、非战斗不进 InteractionLoop、清关跳过 help.choice、道具卡格结算+清 Trap、困难房门槛、第 3 层节点 8 通关 |
| `ShopBuyGoldContractTests` / `ShopSessionContractTests` | #92：商店四货架、购买留店扣金、刷新翻倍（本次进店）、离开、余额不足拒买 |
| `TavernSessionContractTests` | #93：卡店三项服务、扩容/强化扣费留店、刷新翻倍、离开、道具卡固定二级选择确认/取消、余额不足拒买 |
| `SpecialRewardSessionContractTests` | #94：宝箱奖励 4 卡 / 道具奖励 5 卡、免费拿走进携带卡包留房、离开放弃不加 skip 金 |
| `RoomOpeningInjectContractTests` | #95 / ADR-0022：五种战斗房开局注入（固定/权重可重复/不可重复）、困难房怪物侧序列 3+4、注入顺序（固定→房间→携带）、RandomBattle 开局分房 |
| `CarryPackClosedLoopContractTests` | #96 / ADR-0022：商店买 3 张→下战斗张数=容量+房间注入+3、特殊房进包、注入未用清关 +10/张、与固定卡分容器 |
| `RelicInventoryEconomyContractTests` | #98：遗物栏上限 12、满栏 SelectReward 拒收保留 Pending、DiscardRelic +20、宝箱 SkipRelicChoiceGold +20 |
| `CarryPackPreserveBootstrapContractTests`（Presentation `BattleSession/`） | #96：`BootstrapRun(preserveRunInventory)` 携带卡包存活；无 preserve 则清空 |
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

## 跑测（Unity CLI）

```bash
unity command recompile --project-path "<repo>"
unity command run_tests --mode editor --filter <NameOrNamespace> --project-path "<repo>" --format json
```
