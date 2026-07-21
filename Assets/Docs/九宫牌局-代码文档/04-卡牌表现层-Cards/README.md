# 04 — 卡牌表现层（Cards）

> 权威来源：`Assets/Scripts/Cards/**` 源码。不含 Docs/Notes/规划推断。  
> 程序集：`NineGrid.Cards`（运行时）· `NineGrid.Cards.Editor` · `NineGrid.Cards.Tests`（仅清单）。

---

## 1. 层职责

Cards 是**纯表现层**：管理卡牌视图生命周期、三区布局（牌组 / 手牌 / 九宫场地）、发牌飞行动画、场地交战演出、单卡反馈特效。

| 职责域 | 权威入口 | 说明 |
|--------|----------|------|
| 视图注册表 | `CardManagerSingleton` | 以 Uid 键管理 `ManagedCard` → `StandardCardView` |
| 牌组布局 / 发牌 | `CardDeckManagerSingleton` | Standby / Entry / InGame；向 Ground 或 Hand 发牌 |
| 手牌交互 | `CardHandManagerSingleton` | 最多 5 槽；hover / 拖拽 / 释放到 ApplyZone |
| 场地占用与运动 | `GroundFieldManagerSingleton` | 9 格几何占用；外圈旋转；L2 收敛位移 |
| 场地交战演出 | `FieldBattleManagerSingleton` | Intent → Catalog → Adapter Rig；命中经 Sink |
| 发牌飞行 | `SlotDealFlightService` + DealFlight* | Explore / Drain；换格 = Redirect |
| 单卡反馈 | `CardEffectManager` + Effects SO | Attack/Hit/Death/Use/HitFlash |
| 变换塔 / 收敛 | `Convergence/` | L0–L4 塔层；租约；节拍；交接 |

**不做的事（边界）：**

- 不拥有逻辑占格真相（注释写明逻辑在 Core `BoardModel`，合法性由 Flow idle 裁决）。
- 运行时 asmdef **不引用** `NineGrid.Core` / `NineGrid.Flow`；跨层经 Sink 委托与 Uid 契约。
- 不直接改场景 `.unity`（装配点见各 Singleton 的 SerializeField）。

---

## 2. 与 Flow / Core 的边界

```
Core（逻辑）          Flow（编排/桥）           Cards（表现）
─────────────        ──────────────          ──────────────
CardInstance.Uid ──► 映射写入 ManagedCard ──► CardManager 注册表
BoardModel 占格  ──► idle 裁决 / Sync      ──► GroundField 几何占用表
战斗结算事件     ──► CombatHitSink 填充    ──► FieldBattle / Drain
区归属查询       ──► CardZoneOwnershipSink ──► Deck/Hand 门禁
描述/飘字/日志   ──► *TraceSink / HoverSink ──► Probe / UI 侧消费
```

**Uid 约定（源码）：**

- 正整数：与 Core `CardInstance.Uid` 共用。
- `0`：无效（`CardManagerSingleton.InvalidUid`）。
- 负整数：纯表现卡（BounceFan 等），由 CardManager 递减分配，永不与 Core 正 Uid 撞号。

**关键桥接面：**

| 桥 | 方向 | 作用 |
|----|------|------|
| `CombatHitSink` | Cards → Flow 委托 | 命中结算、击杀后盘面、拾取/用道具、输入门禁 |
| `CardZoneOwnershipSink` | Flow → Cards 查询 | Core ItemSlots / DrawPile，避免 asmdef 环 |
| `ChoreoTraceSink` / `PerfTraceSink` / `FlowFieldTraceSink` / `RegistryTraceSink` | Cards → 外部 | 编排/性能/场地/注册表诊断 |
| `DescriptionHoverSink` | Cards → UI | 卡牌描述悬停展示 |
| `ManagedCard.CoreKind` | Flow 写入 | `CardPresentationKind`，数值对齐 Core `CardKind` |

---

## 3. asmdef

### `NineGrid.Cards`（`Assets/Scripts/Cards/NineGrid.Cards.asmdef`）

| 项 | 值 |
|----|-----|
| rootNamespace | `NineGrid.Cards` |
| references | `UniTask` |
| precompiled | `DOTween.dll`, `Sirenix.OdinInspector.Attributes.dll` |
| overrideReferences | `true` |
| autoReferenced | `true` |

**未引用** Core / Flow / Content — 故意隔离。

### `NineGrid.Cards.Editor`

| 项 | 值 |
|----|-----|
| references | `NineGrid.Cards` |
| platforms | Editor only |

### `NineGrid.Cards.Tests`

| 项 | 值 |
|----|-----|
| references | Cards, Content, Core, Flow, QFramework, UniTask, TestRunner |
| platforms | Editor |
| autoReferenced | `false` |
| precompiled | nunit, DOTween |

Tests 可引用 Core/Flow；生产 Cards 不可。

---

## 4. 核心关系：Manager ↔ View ↔ DealFlight

```
                    CardManagerSingleton
                    （Uid → ManagedCard → StandardCardView）
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
 CardDeckManager      CardHandManager      GroundFieldManager
 （牌组槽+发牌）       （手牌槽+拖拽）      （9格占用+运动）
          │                   │                   │
          │ DealCard*         │ PullFromGround    │ SlotDealFlightService
          └───────────────────┴──────────────────►│ Explore / Drain 飞行
                                                  │
                                                  ▼
                                         FieldBattleManager
                                         （交战 Rig + CombatHitSink）
```

1. **CardManager** 只负责 Spawn/Release/DisplayMode；不持有槽位。
2. **Deck** 从自身槽取出卡 → `GroundField.RequestPlaceCard` → 场地启动 Drain/Explore 飞牌。
3. **Hand** 通过 `IHandoffEndpoint` 与 Deck/Ground 交换卡；拖拽 Apply 经 `DragApplyValidator`（待 Core 注入）。
4. **GroundField** 持有 `_uidBySlot` 几何表 + `SlotDealFlightService`；格位位移走 L2 五次收敛，不写 world position 做动画。
5. **FieldBattle** 依赖 GroundField 查槽/忙闲，播 `CardAttackBasicAdapter`，命中帧回调 `CombatHitSink`。
6. **StandardCardView** 是预制体上的像素数字/护甲/图标视图；挂在 ManagedCard 上。
7. **DealFlight\***：预算/句柄/数学；实际协程在 `Convergence/SlotDealFlightService`。

---

## 5. 目录路由表（每个 .cs 一条）

### 根目录 `Assets/Scripts/Cards/`

| 文件 | 一句话 |
|------|--------|
| `ArmorBlockDisplay.cs` | 护甲板块显示（内部，供 StandardCardView） |
| `BoardCardSelectModeController.cs` | 场地多选模式静态控制器 |
| `BoardMotionStepScheduler.cs` | 消费 Rotate/Swap/Move Step，调 GroundField |
| `BoardPresentationMerge.cs` | 合并致死命中与击杀后盘面结果 |
| `BoardSelectParkedCardHitProxy.cs` | 棋盘选择时停靠卡点击代理 |
| `BurstScatterPointSampler.cs` | 爆炸散开圆周采样 |
| `CardAttackBasicAdapter.cs` | 基础交战方向 Rig 适配器 |
| `CardAttackBasicDirectionRig.cs` | 单方向攻击 Timeline/Tween Rig |
| `CardBurstScatterIntoDeckPresenter.cs` | 场地卡炸散后入组表现 |
| `CardDeckLayoutSettings.cs` | 牌组布局/动效序列化参数 |
| `CardDeckManagerSingleton.cs` | 牌组单例：三模式 + 发牌 |
| `CardDeckMode.cs` | Standby/Entry/InGame 枚举 |
| `CardDeckSlotContainer.cs` | 牌组稠密槽容器 |
| `CardDeckTween.cs` | 牌组 DOTween 移动 / 离场入组 |
| `CardHandAnchorUtility.cs` | HandAnchors 排序与索引解析 |
| `CardHandLayoutSettings.cs` | 手牌布局/hover/拖拽参数 |
| `CardHandManagerSingleton.cs` | 手牌单例 |
| `CardHandSlotContainer.cs` | 手牌定长槽容器 |
| `CardManagerSingleton.cs` | 卡牌视图注册表单例 + ManagedCard |
| `CardOpacityUtility.cs` | 卡牌 alpha 批量工具 |
| `CardPerformanceTimelinePlayer.cs` | DOTweenTimeline 播放包装 |
| `CardPresentationKind.cs` | 表现层卡种（对齐 Core CardKind） |
| `CardPresentationProbe.cs` | 表现诊断探针（运动/注册/租约等） |
| `CardSlotAnchorUtility.cs` | GroundAnchors 槽解析与开局环 |
| `CardViewTween.cs` | 视图 Punch/Appear/Disappear 协程 |
| `CardVisualDriver.cs` | 显示模式驱动（缩放/sorting/反馈） |
| `CardZoneOwnershipSink.cs` | Core 区归属查询桥 |
| `ChoreoTraceSink.cs` | 编排追踪 Sink |
| `CombatHitSink.cs` | 战斗/盘面表现结果桥 + 输入门禁 |
| `DealFlightContext.cs` | 飞牌预算上下文 |
| `DealFlightHandle.cs` | 单飞句柄（WaitSettle） |
| `DealFlightKind.cs` | Explore / Drain |
| `DealFlightLayoutSettings.cs` | 飞牌曲线/预算参数 |
| `DealFlightMath.cs` | 贝塞尔与进度数学 |
| `DealSettleBudget.cs` | 就位时间预算 |
| `DealVisualTargetResolver.cs` | 起飞视觉瞄准格预解 |
| `DescriptionHoverSink.cs` | 描述悬停桥 |
| `DirectorAttackPresentTargeting.cs` | 导演攻击表现目标决策 |
| `FieldBattleManagerSingleton.cs` | 场地交战单例 |
| `FlowFieldTraceSink.cs` | 场地 Flow 追踪桥 |
| `GroundCardHitProxy.cs` | 场地卡碰撞代理 |
| `GroundFieldLayoutSettings.cs` | 场地布局 + dealFlight 参数 |
| `GroundFieldManagerSingleton.cs` | 场地单例 |
| `GroundFieldSnapshot.cs` | 场地占用快照 |
| `GroundSlotHitProxy.cs` | 空槽点击代理 |
| `GroundSlotRelation.cs` | 格位关系枚举 |
| `GroundSlotTopology.cs` | 九宫拓扑静态工具 |
| `HandCardHitProxy.cs` | 手牌碰撞代理 |
| `PerfTraceSink.cs` | 性能追踪桥 |
| `PixelCardPackSpriteLibrary.cs` | 像素数字/护甲 Sprite 库 SO |
| `PixelDigitDisplay.cs` | 像素数字显示（内部） |
| `RegistryTraceSink.cs` | 注册表追踪桥 |
| `SkeletonDeckLayoutSettings.cs` | 骷髅合体时序参数 |
| `SkeletonDeckPresentationManager.cs` | 骷髅军团合体表现 |
| `SkeletonFusionPresentationRequest.cs` | 合体请求 DTO |
| `StandardCardView.cs` | 标准卡面视图 |

### 子目录（详见专章）

| 目录 | 文档 |
|------|------|
| `Battle/` | [Battle/战斗表现.md](./Battle/战斗表现.md) |
| `Convergence/` | [Convergence/汇合与编排.md](./Convergence/汇合与编排.md) |
| `Effects/` | [Effects/卡牌效果表现.md](./Effects/卡牌效果表现.md) |
| `Editor/` | [Editor/Cards编辑器.md](./Editor/Cards编辑器.md) |
| 根目录详解 | [Managers/管理器与视图.md](./Managers/管理器与视图.md) |

### Tests 清单（仅列文件，不写权威语义）

`Assets/Scripts/Cards/Tests/`：

- `BattlePresentationRouterTests.cs`
- `BeatGridBarrierTests.cs`
- `BoardPresentationMergeTests.cs`
- `BoardPresentationStepProjectorTests.cs`
- `BurstScatterPointSamplerTests.cs`
- `CardAttackBasicDirectionRigTests.cs`
- `CardManagerPresentationUidTests.cs`
- `CardTransformTowerTests.cs`
- `CombatHitSinkOpeningGateTests.cs`
- `ConvergenceCurveTests.cs`
- `DealFlightMathTests.cs`
- `DealNullViewTests.cs`
- `DeckInsertGateTests.cs`
- `EffectFrameConvergenceTests.cs`
- `FlightSortingChannelTests.cs`
- `HandDeckOwnershipTests.cs`
- `HandoffProtocolTests.cs`
- `HelpCardBoardSelectResolverTests.cs`
- `LeaseArbiterTests.cs`
- `LethalPresentationRegressionTests.cs`
- `PresentationClockTests.cs`
- `ShuffleIntoDeckPresentationScannerTests.cs`
- `SkeletonFusionPresentationScannerTests.cs`
- `SlotFrameConvergenceTests.cs`
- `StaleOccupancyTests.cs`
- `TauntRedirectMotionTests.cs`

---

## 6. 命名空间

| 命名空间 | 范围 |
|----------|------|
| `NineGrid.Cards` | 根、Battle、Effects（含 Implementations） |
| `NineGrid.Cards.Convergence` | Convergence/ |
| `NineGrid.Cards.Editor` | Editor/ |
| `NineGrid.Cards.Tests` | Tests/ |
