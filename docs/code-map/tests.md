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
| `FlowShell/` | 流程壳 Controller；**WalkSandboxRetirementStructuralTests**（#91：无跳格沙盒 API；`\0`=流程测试 Sequential）；**InRoomBoardWiringStructuralTests**（图标进房后须接 `PresentInRoomSessionAfterEnterAsync`，禁死代码 `PlayRoomEventAsync`；房内会话禁整段 `ChoiceOverlay`；#137 属性房池路由 `AttributeBoardPresenter`） |
| `BattleSession/` | 局内会话；**NodeSettlementReadiness**（#83 / ADR-0021：清关 RoomChoice 须唤醒主循环）；**ForceNodeVictoryStructuralTests**（跳过战斗不得再 Offer help.choice） |
| `Cheat/` | F12 作弊工具面板契约：**CheatToolCardSearchIndexTests**（候选池仅怪物/机关/道具三类、排除归档卡组、可按卡名/卡组名/技能名/技能描述搜索、选项文案=卡组·卡名）；**CheatToolPanelBootstrapTests**（对齐场景结构：一级五按钮须 `BoxCollider2D`+`CheatToolPanelButton` 而非 uGUI Button；二级 WorldSpace Canvas 打开后面板内须补 `GraphicRaycaster`，且初始关闭；关闭二级清空输入；`TryToggle` 能找到失活的「作弊工具BG」；空物体时仍可最小自举两层；缺 EventSystem 时创建） |
| `Output/` | 描述 / 伤害等输出 |
| `Cards/` | 卡面 Commit、牌库闸、飞行排序、致死表现回归；**CardDisplayModeVisualsTests**（#104：mode 倍率相对预制体基准且可还原）；**DealFlightRemoveCancelContractTests**（同批 Deal→Remove 须取消飞牌，防 choreo 泄漏 / Pickup Buffered）；表现层配置器条目门槛（Skill 不进窗口、HelpCard/Item 保留）+ **空装配清 effectIds / 挂装配投影 / 效果分类标注（道具/遗物/怪物技能/机关技能）/ 同类多载过滤 / skillIds→assemblies 展开**（解耦装配 IA）+ **装配描述自动同步/自定义锁定 / design_text→{param} 参数化** 等；**ContentArt** 路径约定 / Resources 帧加载 / 断链校验（#66）；**JSON→Catalog 投影**（帮助卡/怪物/技能/遗物/牌组/房间）+ 表 JSON（效果模板 + 装配引用，#67–#70、ADR-0008/0009）；**词条/`{param}`/详情合成**（#71）；**右键详述 live 挂载优先**（空白板 JSON + 局内注入技能 / 忽略预设 skillIds）；**Disable Domain Reload 下 Catalog 重绑**；**装配 argsJson 缺占位实参拦截 / 选模板建议实参**；**特效库** `visual_effects.json` 扫描/粘性合并/atlas 帧加载（与 DSL 效果池区分；纯预览阶段） |
| `Flow/` | Flow 侧遗留/切片；**BoardBriefTipCopy / Session**（#89）；**BoardPlacementStructuralTests**（#104+#105 / ADR-0024：生产全集禁 Fit / 禁 parent 到 GroundAnchors / 落格路径禁写 `localScale` / 无 `slotHitBoxSize`）；**ShopBoardSlotResolver**（#92）；**TavernBoardSlotResolver**（#93）；**RewardBoardSlotResolver**（#94）；**AttributeBoardSlotResolver / AttributePickIndexResolver / AttributeBoardPresenterStructuralTests**（#137：候选格位、视觉候选→当前 Pending 索引、点击须经 RewardChoiceCoreHook / 禁直改 Model / 首选驻留等二次 / Despawn 释放清理）；**InRoomLeaveWatchStructuralTests**（离开监视 Active 顺序 / 扣金 HUD / SoftBlockOnly；选项与真卡均预制体尺度、禁 Fit）；**InRoomItemAcquirePresentationStructuralTests**（ADR-0025：购领须接手 ItemSlots→手牌，禁只碎裂货架）；**BounceFanChoiceHitTests**（固定 AABB 命中 / 禁 OverlapPoint 回流）；**DirectionalBiasPickerTests**（同层过场四向袋洗牌） |
| `Fixtures/` | EditMode 夹具 |
| `HostContractStructuralTests.cs` | 结构护栏：禁四大旧宿主名、禁 `CombatHitSink`、禁回流 `new PresentationDirector`、System 不暴露具体 View |
| `BuildSceneMissingScriptStructuralTests.cs` | 构建护栏（#126 清理）：构建启用场景 + 非 Plugins 预制体禁引用无法解析的 m_Script（Missing Script，含 fileID-only 破坏引用）；防已删除脚本组件残留回流 |
| `DevTestSceneSerializationStructuralTests.cs` | 构建护栏（#126）：构建启用场景禁序列化 DevTest-only 组件（`#if UNITY_EDITOR||DEVELOPMENT_BUILD`，Release 即 Missing Script）；MainScene 须恰好序列化 `DevTestSceneInstaller`（始终编译的运行时安装宿主） |
| `RoomChoiceRetirementStructuralTests.cs` | 结构护栏（#90）：无 `RoomChoicePresenter`；`IGameFlowView`/`SelectorManager`/`UiPanelRouter` 无房间浮层 API；无属性三选一 Bounce 入口；BounceFan 仍在 |
| `InRoomBoardWiringStructuralTests.cs` | 结构护栏：`PlayRoomIconChoiceAsync` 进消费/特殊房后须调用 `PresentInRoomSessionAfterEnterAsync`；无独立 `PlayRoomEventAsync` 二次 Enter；`Present*BoardAsync` 禁整段 `SetChoiceOverlay(true)`；**#137**：`attribute.pick` 池须路由到 `AttributeBoardPresenter` 场地板 |
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
| `FormalRunSmokeContractTests` | **#142 正式流程自动烟雾**：正式入口（`InitialGameFactory`，无 QuickTest 载荷）走完整 24 节点骨架（3 层 × 8 节点）——构建节点牌组 → StartNode → 结算信号（离开机关击破清关）→ SelectRoom+EnterRoom 推进；固定种子确定性、不断言随机顺序/动画帧。断言：三层主题不重复绑定（层内 `FloorMonsterDeckId` 一致、跨层互异）；节点 3/6 房间选项分属 ConsumerRooms/SpecialRooms；节点 4/7 非战斗推进不进 InteractionLoop；节点 8 `RequireElite`+含该层 seq5 层主、第 3 层节点 8 → Victory；正式 Avatar 数值非 HP99/ATK5 |
| `DiagTraceMinFieldsContractTests`（Presentation `FlowShell/`） | **#142 日志门禁**：FlowTraceEvent / BattleTraceOp 自动带 `floor`+`nodeIndex`（Run-Floor-Node-Battle 关联），JSON 序列化含关联字段；会话级 seed/sessionId/runTag；`\0` 与正式的 runTag 隔离 |
| `PresentationEventMapBeatExhaustivenessTests` | #54/#57/#60/#61/#62：每个 `CoreEventType` 须有表演映射与显式 `PresentationBeat`；`None` 必须带理由；观察型 `BaseStatModified` 不得落在 `Impact`；`CardDealt`/`AvatarAppeared`/`CardFaceChanged` 为 Settled；`DamageDealt`/`EffectTriggered` 为 Impact；`GoldModified`/`RewardOffered` 为 Settled |
| `CardFaceOrientationTests` | ADR-0016：Flip/Reveal 改 FaceUp 并发 `CardFaceChanged`；攻击拒背面；背面怪不 −1/不开火；`DealDamage`/爆弹类不中背面；`FaceDownTickCounters` 注册/Tick/查询；OnFlip/Flip/IsFaceUp 原子与 `ActiveWhileFaceDown` token |
| `TrapKindDualBucketRegressionTests` / `Batch2HardSkillsRegressionTests`（复活石） / `TrapBatch2RegressionTests` / `LeaveTrapDoorLeaveContractTests` / `LeaveTrapInsertContractTests` / `LeaveTrapClearContractTests` | ADR-0017：`CardKind.Trap` 双桶；爆弹/`SelectedCards` 指向伤（飞刀等）可伤 Trap；绑架 `trueMonsterOnly` 仍仅真怪；清关/赏金排除；静默 `CounterAttackBanned`；复活石 Kind/deck/container 迁正；批2 滚石/捕熊/烈焰语义与 Help 迁徙；**#111 / ADR-0026**：离开机关 `trap.leave` + 门/魔免/离开（交战可伤、非交战/直接移除无效、魔免挡外来效果含传送、击破置清关标志、无赏金）；**#112 / ADR-0026**：默认 ⌈N/2⌉ 后 `ShuffleIntoDrawPile(trap.leave)`（N 不含机关、奇数上取整、幂等）；层主房改击破开局层主；经补牌上场；**#113 / ADR-0026**：`IsNodeCleared`=`IsLeaveTrapBroken`；真怪清零不清关；击破离开机关清关；清场不兑金；道具卡格保留；QuickTest 置标志跳关；**清关后 `ResolvePostKillFill` 禁止补牌**（防 Deal 飞行卡死主线） |
| `RegularTrapBattleLoadoutContractTests` | **#135 / ADR-0030**：`RegularTrapPool` 池过滤（Kind=Trap+稀有度 White，恰六张常规，离开机关/特殊机关排除）；正式战斗装填恰三张且无放回；同种子三张与顺序可复现；池不足按池量（2→2、1→1、0→0）；N 分母不含常规机关；杀机关不计阈值/无赏金；层主房仍三张常规机关且按击破开局层主洗入离开机关 |
| `BaseStatModifiedResultValueTests` | #54：`BaseStatModified` 携带结算后 `ResultValue`，且保留 `Amount=StatId` / `Delta` 增量约定 |
| `PermanentAttackFaceCommitTests` | Permanent 有效攻卡面旁路：Apply 发 BaseStatModified；Temporary 不上屏；Swap 条件失效回基值 |
| `CardSpawnedFaceAbsoluteTests` | #57：`CardSpawned` 携带造卡时攻/甲/血绝对值 |
| `CardKilledFaceAbsoluteTests` | #58：`CardKilled` 携带 `RemainingHp=0` |
| `RewardOfferFaceProjectionContractTests` | #62：`OfferRewardChoice` 把 CreateDraft 攻/甲/血写入 `RewardEntry` 与 `RewardOffered` Message |
| `RewardPoolQueryContractTests` / `RewardPoolDiversityContractTests` | #71：奖池查询规则展开、role 均衡、稀有度分层抽取 |
| `RelicArchiveDeckContractTests` | #115：`deck.relic_archive` 九件错位旧卡不进 common_chest / blood_conversion 展开；Profession/开局不授予归档遗物 |
| `FormalContentReachabilityContractTests` | #139 / ADR-0033：正式内容可达性终审——策划现行 51 件遗物（白 27/蓝 15/金 8/独特 1）全在役、有效果装配、非独特件至少进一个宝箱池、独特件仅职业授予；9 件归档遗物 0 可达；19 种现行道具卡全在役且每张至少一个正式来源（房间注入/商店固定货架/职业道具来源池/精英池/层主注入），`help.impact_tutorial` 按设计归 `deck.player`；**来源池恰为 12 张常规（White）道具卡，7 张特殊（蓝/金/红）不进随机来源池（稀有度按策划常规/特殊表对齐：食品卡/绑票/撞击教程=White）**；7 张非现行道具卡（破击锤/血液转换/倍增塔/盾击教程/属性提升/庇佑/瞭望塔）归 `deck.help_archive` 且不进奖池/道具来源池；奖池无悬空 ID、无空内容；层主击杀固定洗入 1 金宝箱卡 + 2 金币卡（行为断言） |
| `RelicR1NineProfessionContractTests` | #116：九件新建遗物在役卡组+装配；`relic.rotten_cleave_axe` 为 Red 且不进 W/B/G 宝箱池；Profession 授予顺劈斧且基础护甲 0；样本效果（铁盾伤害减免、超越维度战斗旋转） |
| `RelicR1AssemblableContractTests` | #117：13 件可拼新建遗物在役卡组+装配；按稀有度进入 common_chest / blood_conversion；样本效果（废物剑击杀回血、血液暴力半血攻、复合盔甲关初换甲） |
| `RelicR1KeeperAuditContractTests` | #118：20 件名称对齐留用遗物在役+非空描述+装配；渴望 MaxHp+10；幸运硬币仅层主击杀；废物三件 `OnAnyHelpCardUsed` 与利用机回血样本 |
| `RelicR2CumulativeContractTests` | #119：OnCumulative 扩展 `monsterRemoved` / `helpCardUsed` / `damageTaken`（含甲吸收）；六件累计遗物在役+奖池+装配；恐怖面罩只移普通等级怪；血魔承伤涨上限 |
| `RelicR3GrowthContractTests` | #120：`RelicRunContribution` run 内成长；锻造器具关初扣甲成长；金剑战斗衰减/击杀成长/下限 0；新 run 清空；丢弃清贡献 |
| `RelicR3FoamContractTests` | #121：泡沫盔甲 +1 基础甲；本关首次甲归零后武装下一击 Once 免疫（归零击不吃盾）；同关不重武装；多段只免第一段；未消耗盾关初清除 |
| `ThemeMonsterDeckContractTests` | #86 / ADR-0022：每层主题卡组不重复绑定、节点按序列 1–5 抽、Reserve 不参与、层主击杀固定 1 金箱+2 金币 |
| `ThemeDeckStableMappingTests` | #127 / ADR-0029：七套 × sequence 1–5 稳定槽位契约（`ThemeDeckStableMapping`）与生产 JSON 逐槽比对；重复 sequence / 缺槽 / 槽位错位 / 越界成员 / 错误 Boss / 错误 Reserve 由 `ThemeDeckMappingVerifier` / `ThemeDeckFormalReadiness` 明确报出（含坏夹具用例）；**#134 起映射正确（绿）与正式可达（绿）分层，且断言正式池恰为七套、`deck.transition` 归档为 Reserve** |
| `ThemeDeckNarrowContractTests` | #128：基础/链接两套窄内容契约（`ThemeDeckNarrowContract`）——槽位映射正确 + Boss + 槽位卡非 Reserve + 牌组登记 + 两套卡面 JSON 与 monster_decks.json 在 Arts/Streaming 双侧字节一致；#134 起全局正式轮换开放（`FormalReadiness` 绿） |
| `RotationDeckSkillIdsContractTests` | #129：旋转套（deck.orc_legion）怪物技能权威链路——四张带技能卡 `effectAssemblies` 为空、经 `skillIds` 引用 `skill_*.json`（技能文件持有等价效果模板装配）；`big_stone` 无技能、仅靠 attackPattern 斜角近战（不误判为漏技能）；技能效果 id 与迁移前一致；`ValidateCatalog` 绿；五张卡面 JSON Arts/Streaming 双侧字节一致 |
| `InsectDeckSkillIdsContractTests` | #130：召唤套（deck.insect）怪物技能权威链路——四张带技能卡 `effectAssemblies` 为空、经 `skillIds` 引用 `skill_*.json`（技能文件持有等价效果模板装配：死亡召唤/远程武器+提速/吟唱/死亡之主）；`big_skeleton` 无技能、仅靠 attackPattern 全向近战（不误判为漏技能）；技能效果 id 与迁移前一致；`ValidateCatalog` 绿；五张卡面 JSON Arts/Streaming 双侧字节一致 |
| `FlipDeckSkillIdsContractTests` | #131：翻面套（deck.skeleton_legion）怪物技能权威链路——五张带技能卡（伏击近战+跳杀 / 远程武器+起来 / 伏击近战+休养 / 伏击近战+盗取 / 刺客领袖+起来）`effectAssemblies` 为空、经 `skillIds` 引用 `skill_*.json`；技能效果 id 与迁移前一致；**背面惰性触发契约**——常规/被动效果须带 `IsFaceUp` 条件或触发点天然不可在背面发生，策划显式 `OnFlip`（跳杀/休养/盗取）与 `OnDeal`（刺客领袖）翻面边沿触发保留；`ValidateCatalog` 绿；五张卡面 JSON Arts/Streaming 双侧字节一致 |
| `StoneLegionSkillIdsContractTests` | #132：烈焰套（deck.stone_legion）怪物技能权威链路——五张带技能卡（献火 / 远程武器+烈焰沸腾 / 呼唤 / 剧烈燃烧 / 吞云吐雾+生生不息）`effectAssemblies` 为空、经 `skillIds` 引用 `skill_*.json`；技能效果 id 与迁移前一致；`ThemeDeckMappingVerifier.VerifyDeck` 槽位 1–5 与层主标志（shelter_stone 序列 5 / isBoss / FloorBoss）绿；`ValidateCatalog` 绿；五张卡面 JSON Arts/Streaming 双侧字节一致 |
| `VoidDeckSkillIdsContractTests` | #133：决斗套（deck.void）怪物技能权威链路——五张带技能卡（神圣决斗 / 神圣决斗+闪避 / 神圣决斗+战斗硬化 / 神圣决斗+嘲讽 / 神圣决斗+损耗）`effectAssemblies` 为空、经 `skillIds` 引用 `skill_*.json`；**attackPattern=无 显式合法**（ADR-0011），五卡无主动攻击但仍经 `skillIds` 挂技能（不误判为漏技能）；技能效果 id 与迁移前一致；`ThemeDeckMappingVerifier.VerifyDeck` 槽位 1–5 与层主标志（fire_bather 序列 5 / isBoss / FloorBoss）绿；`ValidateCatalog` 绿；五张卡面 JSON Arts/Streaming 双侧字节一致 |
| `ContentDisplayNameAssertGuardrailTests` | #127 / ADR-0029：结构护栏——测试源码禁断言生产内容 displayName（`string.IsNullOrEmpty` 非空检查豁免；白名单仅限自建夹具与代码常量文件） |
| `MonsterLoadoutPresentationValidatorTests` | 过渡期 staging 校验在 `deck.transition` 归档后必须失败（transition_reserve）；交付就绪为正向门禁——七套启用 + 过渡组归档后必须通过 |
| `MapNodeProgressionContractTests` | #84 / ADR-0021 / #107 / #113：8 节点编排全表（节点 7=层主房图标战前缓冲）、非战斗不进 InteractionLoop、进层主房图标推进至节点 8、清关跳过 help.choice（经离开机关标志）、道具卡格清关不兑不清+清场残留、困难房门槛、第 3 层节点 8 通关 |
| `ShopBuyGoldContractTests` / `ShopSessionContractTests` / `ShopItemSlotsUpgradeContractTests` | #92 / #108 / #109：商店四货架 + 未满级升级项、购买留店扣金直写道具卡格、格升级 50 金且不改 `ItemDeckCapacity`、满 5 隐藏、刷新翻倍、离开、余额/格满拒买 |
| `TavernSessionContractTests` | #93：卡店三项服务、扩容/强化扣费留店、刷新翻倍、离开、道具卡固定二级选择确认/取消、余额不足拒买 |
| `SpecialRewardSessionContractTests` | #94 / #108：宝箱奖励 4 卡 / 道具奖励 5 卡、免费拿直写道具卡格留房、满格拒领、离开放弃不加 skip 金 |
| `RoomOpeningInjectContractTests` | #95 / ADR-0022：五种战斗房开局注入（固定/权重可重复/不可重复）、困难房怪物侧序列 3+4、注入顺序（固定→房间）、RandomBattle 开局分房；**#136**：属性房无三选二选择结果时不自动注入 |
| `ItemSlotsDirectGrantContractTests` | #108 / ADR-0025：商店/特殊房直写道具卡格、无携带开局注入、满格拒拾、默认容量 3 |
| `AttributePickSessionContractTests` | #136 / ADR-0031：属性房三选二——进房 3 加权候选（40/40/20 可重复）、首选不提前结束/移除实例、二选提交 RunModel+推进节点只结算一次、越界/重复选择拒绝、满格不阻断、离开放弃、选择结果注入本关卡组后清空、新 Run 重置 |
| `ItemSlotsRecycleContractTests` | #110 / ADR-0025 / ADR-0032：道具卡格回收 +10 金并移除；商店相位合法；Apply 跳过表演锁门禁；**非战斗相位（RoomChoice/RoomEvent/RewardItemChoice）禁 UseItem，仅 usableOutsideBattle=true 的卡在 RoomChoice/RewardItemChoice 合法**；仍可回收 |
| `RelicInventoryEconomyContractTests` | #98：遗物栏上限 12、满栏 SelectReward 拒收保留 Pending、DiscardRelic +20、宝箱 SkipRelicChoiceGold +20 |
| `CarryPackPreserveBootstrapContractTests`（Presentation `BattleSession/`） | #107 / #108：`BootstrapRun(preserveRunInventory)` 道具卡格与容量存活；无 preserve 则清空 |
| `NodeStartRelicCardGrantTests` | 关卡开始遗物加卡：飞刀进抽牌堆、交换按钮进道具卡格；满格静默丢弃不兑金 |
| `ContentCatalogValidationTests` | 小型夹具 `ValidateCatalog` 绿；生产 Bootstrap（模板表 + 装配引用解析 + schema≥2 JSON 投影 + 奖池查询展开 + 节点序列规则）校验绿；跨容器共享模板不同实参（#70 / ADR-0009） |
| `ContentHygieneContractTests` | **#140**：内容卫生终验——`_index.json` 条目与磁盘生产内容双向一致（无缺项/重复，两侧索引字节一致）；Authoring/Streaming 文件集合与内容完全一致；正式内容无悬空 skillId / 装配模板 / 模板 body defId；无空壳技能（Skill 无装配无 effectIds）；归档内容（relic/help_archive）不进入房间注入 / 模板 defId / 奖池展开 / 职业道具来源池等正式 grant 路径；奖池展开与房间注入无悬空 grant ID |
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

- 商店 / 卡店 / 特殊奖励房主循环与节点编排已接线，本 Map 内的验证边界为 **EditMode**：契约测试（`ShopSessionContractTests` / `TavernSessionContractTests` / `SpecialRewardSessionContractTests` / `AttributePickSessionContractTests` / `MapNodeProgressionContractTests` / `InRoomBoardWiringStructuralTests` / 各 `*BoardSlotResolverTests` / `AttributeBoardPresenterStructuralTests` / `AttributePickIndexResolverTests`）覆盖 Core 会话、清关后放图标与房内场地板接线。
- 正式 / QuickTest 隔离（#125 / #135 / #142）：`GameFlowRunModeContractTests` 契约正式入口无 QuickTest 标志/作弊/动态装配（`GameFlowRunOptions.CreateFormal` 无载荷、正式 BeginRun 无 RunTag/无技能机关列表），`\0` 仍具镜像与作弊标志（Sequential 节点序 + HP99/ATK5 + QuickTest RunTag）；结构性断言 `TestMode` 布尔与 `(bool,bool)` 组合构造已删除；**#135 隔离**：`\0` 无定向机关注入（与正式镜像一致），`\1–\9` 各恰一张定向机关且不得注入离开机关（正式三张常规机关由 Core 装填路径统一注入）。**#142 镜像骨架**：`Channel0_SequentialQueue_MirrorsFormal24NodeSkeleton` 断言 `\0` Sequential 队列 = 3 层 × 每层战斗节点（与正式 24 节点骨架的战斗段一致）；`FormalEntry_HasNoQuickTestSkillOrTrapPayload` / `QuickTestEntry_FormalMirrorHasNoSkillOrTrap_Channel0` 强化正式↔`\0` 载荷隔离。
- **#142 正式流程自动烟雾**：`FormalRunSmokeContractTests`（Core）走完整 24 节点骨架（固定种子、确定性），覆盖三层主题不重复绑定、节点 3/6 房间选项、节点 4/7 非战斗推进、节点 8 层主与最终 Victory、正式无 HP99/ATK5；`RewardSystem.BuildNodeDeckOptions` 对全局节点索引（shell 1..24）归一化为层内展示节点，跨层不再落入无怪物 fallback。
- **#142 日志门禁**：`DiagTraceMinFieldsContractTests` + `DiagTraceShared.ResolveFloor/ResolveNodeIndex`——FlowTrace/BattleTrace 会话共享 Run 关联（seed/sessionId/runTag），事件/op 自动带 `floor`+`nodeIndex`；日志分析入口见 `.cursor/skills/table-nine-battlelog-analysis/`（扫描 Error/Exception/Assert 与 Run-Floor-Node-Battle 关联）。
- 完整一局（主菜单开始 → 3 层 × 8 节点 → 胜利/失败）的**真实运行 PlayMode 终验**属本 Map 最终门禁（`#142` 自动烟雾 + `#143` 人工终验），当前未运行前**不得宣称已通过**；tests.md 不写入尚未运行的 PlayMode 结果。

## 跑测（Unity CLI）

```bash
unity command recompile --project-path "<repo>"
unity command run_tests --mode editor --filter <NameOrNamespace> --project-path "<repo>" --format json
```
