# 07 · 测试体系（代码事实地图）

> 仅罗列仓库中已存在的测试程序集与用例文件；语义以测试类名与断言代码为准，详见各 `*Tests.cs`。

---

## 程序集一览

| 程序集 | 路径 | 约计用例文件 |
|--------|------|-------------|
| NineGrid.Core.Tests | `Assets/Scripts/NineGrid.Foundation/NineGrid.Core.Tests/` | 25 |
| NineGrid.Flow.Tests | `Assets/Scripts/Flow/Tests/Editor/` | 12 |
| NineGrid.Cards.Tests | `Assets/Scripts/Cards/Tests/` | 26 |
| NineGrid.DevTest.Tests | `Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest.Tests/` | 1 |
| NineGrid.LivingUI.Tests | `Assets/Scripts/UI/LivingUI/Tests/Editor/` | 2 |

---

## Core.Tests — 契约 / 回归

| 文件 | 从文件名推断的关注点 |
|------|----------------------|
| `CoreCommandShellTests.cs` | 命令壳 |
| `CoreOperationContractTests.cs` | 核心操作契约 |
| `ContentCatalogValidationTests.cs` | 内容目录校验 |
| `MidBattleRewardChoiceContractTests.cs` | 战中奖励选择契约 |
| `RewardPoolDiversityContractTests.cs` | 奖励池多样性 |
| `ShopBuyGoldContractTests.cs` | 商店购金 |
| `HelpCardRewardPersistenceTests.cs` | 帮助卡奖励持久 |
| `NodeStartRelicCardGrantTests.cs` | 节点开始遗物发牌 |
| `RecombineDedupRegressionTests.cs` | 重组合并去重 |
| `TauntRedirectRegressionTests.cs` | 嘲讽改道 |
| `CombatHitDeathRegressionTests.cs` | 命中死亡 |
| `EconomyKillGoldRegressionTests.cs` | 击杀金币 |
| `ArmorModelRegressionTests.cs` | 护甲模型 |
| `GoldArmorRegressionTests.cs` | 金/甲 |
| `MaxHpRegressionTests.cs` | 最大生命 |
| `SharpStoneArmorBreakRegressionTests.cs` | 尖石破甲 |
| `StoneShelterDamageFlatRegressionTests.cs` | 石盾伤害平坦 |
| `SurvivalWisdomKillRegressionTests.cs` | 生存智慧击杀 |
| `HardReflectBootstrapRegressionTests.cs` | 硬反弹引导 |
| `FallApartOnRemoveRegressionTests.cs` | 移除时崩解 |
| `FallingRocksDeckGateRegressionTests.cs` | 落石牌库门闩 |
| `CardZoneDeckGateRegressionTests.cs` | 卡区牌库门闩 |
| `CardOwnedTriggerScopeGateRegressionTests.cs` | 卡属触发作用域门闩 |
| `BattleHardenedScopeRegressionTests.cs` | 战锻作用域 |
| `OnBattleFilterRegressionTests.cs` | 开战过滤 |

**模式事实**：大量 `*RegressionTests` + 若干 `*ContractTests`；测试引用 `NineGrid.Content` + `NineGrid.Core` + QFramework（见文件 using）。

---

## Flow.Tests — 垂直切片 / 表现

| 文件 | 从文件名推断 |
|------|----------------|
| `AttackVerticalSliceTests.cs` | 攻击切片 |
| `DrainVerticalSliceTests.cs` | 抽取/消耗切片 |
| `ExploreVerticalSliceTests.cs` | 探索切片 |
| `FusionVerticalSliceTests.cs` | 融合切片 |
| `ShuffleVerticalSliceTests.cs` | 洗入牌库切片 |
| `UseItemVerticalSliceTests.cs` | 用物切片 |
| `BoardIntentLegalityTests.cs` | 棋盘意图合法性 |
| `PresentationDirectorTests.cs` | 表现导演 |
| `ShuffleBurstGrouperTests.cs` | 洗牌爆发分组 |
| `OccupancyForceSyncGuardTests.cs` | 占位强制同步守卫 |
| `TriggerPulseAndDiagnosticsTests.cs` | 触发脉冲与诊断 |
| `TauntRedirectPresentTargetingTests.cs` | 嘲讽改道表现选目标 |

**模式事实**：以 `*VerticalSliceTests` 覆盖 Flow.Presentation 主路径。

---

## Cards.Tests — 表现数学 / 汇合 / 所有权

| 文件 | 从文件名推断 |
|------|----------------|
| `DealFlightMathTests.cs` | 发牌飞行数学 |
| `DealNullViewTests.cs` | 空视图发牌 |
| `ConvergenceCurveTests.cs` | 汇合曲线 |
| `EffectFrameConvergenceTests.cs` / `SlotFrameConvergenceTests.cs` | 帧汇合 |
| `PresentationClockTests.cs` | 表现时钟 |
| `LeaseArbiterTests.cs` / `HandoffProtocolTests.cs` | 租约/交接协议 |
| `HandDeckOwnershipTests.cs` | 手牌牌库所有权 |
| `DeckInsertGateTests.cs` | 牌库插入门闩 |
| `StaleOccupancyTests.cs` | 陈旧占位 |
| `BoardPresentationMergeTests.cs` / `BoardPresentationStepProjectorTests.cs` | 棋盘表现合并/投影 |
| `BattlePresentationRouterTests.cs` | 战斗表现路由 |
| `BeatGridBarrierTests.cs` | 节拍格屏障 |
| `BurstScatterPointSamplerTests.cs` | 爆发散射采样 |
| `CardAttackBasicDirectionRigTests.cs` | 普攻方向绑架 |
| `CardManagerPresentationUidTests.cs` | 表现 UID |
| `CardTransformTowerTests.cs` | Transform 塔 |
| `CombatHitSinkOpeningGateTests.cs` | 命中下沉开门闩 |
| `FlightSortingChannelTests.cs` | 飞行排序通道 |
| `HelpCardBoardSelectResolverTests.cs` | 帮助卡选位（测试位于 Cards.Tests，实现可能在 Flow） |
| `LethalPresentationRegressionTests.cs` | 致死表现回归 |
| `ShuffleIntoDeckPresentationScannerTests.cs` | 洗入扫描 |
| `SkeletonFusionPresentationScannerTests.cs` | 骨架融合扫描 |
| `TauntRedirectMotionTests.cs` | 嘲讽改道运动 |

---

## 其他

| 文件 | 说明 |
|------|------|
| `DevTest.Tests/TestKeyManagerTests.cs` | 测试键管理器 |
| `LivingUI.Tests/LivingUiContentPolicyTests.cs` | LivingUI 内容策略 |
| `LivingUI.Tests/LivingUiTransitionPlannerTests.cs` | 过渡规划 |

---

## 测试分层观察（事实）

1. **规则正确性**主要压在 Core.Tests（可无引擎）。  
2. **意图→表现切片**压在 Flow.Tests。  
3. **视图/飞行/汇合数学**压在 Cards.Tests。  
4. 命名大量使用 `Regression` / `Contract` / `VerticalSlice`，表明测试在锁定既有行为而非探索新 API。

详细断言与夹具构造见源码；本页只做路由索引。
