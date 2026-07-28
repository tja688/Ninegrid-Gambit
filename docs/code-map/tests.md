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
| `FlowShell/` | 流程壳 Controller |
| `BattleSession/` | 局内会话 |
| `Output/` | 描述 / 伤害等输出 |
| `Cards/` | 卡面 Commit、牌库闸、飞行排序、致死表现回归；表现层配置器条目门槛（Skill 不进窗口、HelpCard/Item 保留）+ **空装配清 effectIds / 挂装配投影**（解耦装配 IA）等；**ContentArt** 路径约定 / Resources 帧加载 / 断链校验（#66）；**JSON→Catalog 投影**（帮助卡/怪物/技能/遗物/牌组/房间）+ 表 JSON（效果模板 + 装配引用，#67–#70、ADR-0008/0009）；**词条/`{param}`/详情合成**（#71） |
| `Flow/` | Flow 侧遗留/切片 |
| `Fixtures/` | EditMode 夹具 |
| `HostContractStructuralTests.cs` | 结构护栏：禁四大旧宿主名、禁 `CombatHitSink`、禁回流 `new PresentationDirector`、System 不暴露具体 View |
| `IntentIntakeStructuralTests.cs` | 结构护栏（#52）：输入路径须经 IntentIntake；门禁/收口决策禁用壁钟；ADR-0004 accepted |
| `PointerInputStructuralTests.cs` / `PointerHitRouterTests.cs` | ADR-0006：禁 HitProxy `OnMouse*`；指针缝 / HitRouter 行为；mitigation 标记 |
| `CardFaceBeatStructuralTests.cs` | 结构护栏（#55–#62）：禁石头爱好者卡面提前同步；攻击/反击命中帧须报 Impact 且不得 Sync/SpawnDamagePopups；PresentStep 在 ack 前 FlushBeats；用道具 Present 禁 CommitAllSpawnedCards、须 Vacate 前报 Impact；探索/用道具批次投影禁写卡面数值；禁 JSON stats 盖写；首次 ApplyToManagedCard 禁 TryRead；Handler 消费 Spawn/Deal/Avatar/OfferReward；Bounce 禁 `clearCombatStats` 数值旁路；CommitPresentation 生产调用方白名单；MarkFieldDead 禁直置零；ApplyKill 取 RemainingHp；底盘数值 Setter 非公开；禁 `PresentEffectTriggersFromEventLog` / `SpawnDamagePopups` / `PresentGoldGainsFromEventLog`；组合根注册飘字/FX/金币/Avatar HUD 处理器；排期器禁 SyncFromCore；ADR-0005/0007 交叉引用；排期器多 `IBattleBeatHandler` |

### Core 契约护栏（`NineGrid.Core.Tests`）

| 测试 | 保护什么 |
|------|----------|
| `PresentationEventMapBeatExhaustivenessTests` | #54/#57/#60/#61/#62：每个 `CoreEventType` 须有表演映射与显式 `PresentationBeat`；`None` 必须带理由；观察型 `BaseStatModified` 不得落在 `Impact`；`CardDealt`/`AvatarAppeared` 为 Settled；`DamageDealt`/`EffectTriggered` 为 Impact；`GoldModified`/`RewardOffered` 为 Settled |
| `BaseStatModifiedResultValueTests` | #54：`BaseStatModified` 携带结算后 `ResultValue`，且保留 `Amount=StatId` / `Delta` 增量约定 |
| `CardSpawnedFaceAbsoluteTests` | #57：`CardSpawned` 携带造卡时攻/甲/血绝对值 |
| `CardKilledFaceAbsoluteTests` | #58：`CardKilled` 携带 `RemainingHp=0` |
| `RewardOfferFaceProjectionContractTests` | #62：`OfferRewardChoice` 把 CreateDraft 攻/甲/血写入 `RewardEntry` 与 `RewardOffered` Message |
| `RewardPoolQueryContractTests` / `RewardPoolDiversityContractTests` | #71：奖池查询规则展开、role 均衡、稀有度分层抽取 |
| `ContentCatalogValidationTests` | 小型夹具 `ValidateCatalog` 绿；生产 Bootstrap（模板表 + 装配引用解析 + schema≥2 JSON 投影 + 奖池查询展开）校验绿；跨容器共享模板不同实参（#70 / ADR-0009） |
| `EffectTemplateAssemblyContractTests` | #70：取消 typeTag/verb 门禁；`requires` 解析；装配实参替换与跨容器共享模板 |
| `EffectSelfDeclarationContractTests` | #72 / ADR-0010：requires 校验（未知 token / mount 错配 / 缺声明）；拒上下文开关旧形；生产卡挂载显式场景声明审计归零；`ValidateCatalog` 绿 |
| `CardOwnedTriggerScopeGateRegressionTests` / `CardZoneDeckGateRegressionTests` | #73 / ADR-0010 Phase C：外部门禁拆除后自陈等价（无 `EffectOwnerScopeGate` / 区域门禁回流）；帮助卡互不误触、亡语互不误触 |
| `EffectNonTriggerProbeTests` | #73 / ADR-0010 Phase D：未触发探查区分 requires vs conditions；与正向诊断层并存 |

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
