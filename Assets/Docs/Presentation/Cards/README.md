# Presentation / Cards 区权威文档

> 生成于 2026-08-12，以当日代码实际实现为准。范围：`Assets/Scripts/NineGrid.Presentation/Cards/` 全部 .cs（逐文件阅读后成文；初版 166 个，2026-08-13 新增 `Vfx/BoardCardLifeFx.cs` 后 167）。
> 兄弟区块：[`../Flow/`](../Flow/)（导演/时间线/排期器/房间流程）与 [`../Systems与通信/`](../Systems与通信/)（QF System/Controller/Command/Query）由对应文档负责；本区正文提及的 Flow/Systems 类型请到彼处查详情。

## Cards 区在 Presentation 中的角色

`Cards/`（命名空间 `NineGrid.Cards*`，约 3 万行）是表现层的**卡牌视觉与交互区**：卡牌运行时实体（底盘+卡面）、场地/手牌/卡组三大表现域、格位命中与认领、盘面运动收敛内核、交战表演 rig、卡面文案与检视、单卡特效与程序化装饰。它遵守三条总纪律：

1. **占格权威 vs 几何注册**：逻辑占格真相永在 Core `BoardModel`；本区只持几何登记（slot↔uid）与命中认领（slot→认领者），冲突显式报错、禁止 force-sync 自愈。
2. **卡面显示值只经 Commit**：一切可见数值/朝向由 Flow 排期器经 `ManagedCard.CommitPresentation` 提交（ADR-0002/0005），本区不直读 Core 刷卡面。
3. **无环依赖**：Cards 目录代码不引用 Flow/Presentation 宿主类型，跨界一律走静态 Hook 装配缝与 TraceSink 诊断旁路（08 篇）。

## 完整子目录树

```
Cards/
├─ (根目录, 74 files)   底盘/视图、场地视图与命中、手牌/卡组管理器、交战执行、发牌数据、Hook/Sink
├─ Anim/        (6)     卡面主视觉帧动画与 Mask 锚定
├─ Battle/      (11)    交战编排路由（Catalog/Profile/BindParams）、Present 执行缝、终态守卫
├─ Convergence/ (18)    L0–L4 变换塔、五次收敛曲线与驱动、租约仲裁、节拍栅栏、飞行排序通道
├─ Effects/     (22)    单卡效果 SO 体系（含 Implementations/ 6 个具体 SO）
├─ Ground/      (8)     几何占格表、认领登记、飞牌协调器、盘面运动执行器
├─ Presentation/(16)    卡面投影快照、Binder、描述投影/合成、词条与详述、翻牌
├─ Slots/       (8)     卡面装配槽表（代号/角色/节点映射/排序分区/校验）
└─ Vfx/         (4)     程序化装饰：边沿尘雾、威胁范围荧光、场地卡生命层（L1 飘动/冲击）
```

## 分篇导航

| 篇 | 文件 | 内容 |
|---|---|---|
| 01 | [01-底盘与卡面模板.md](./01-底盘与卡面模板.md) | ManagedCard/CardManager、Spawn 与五套卡面挂载、显示模式与排序、装配槽表、主视觉动画与 Mask |
| 02 | [02-卡面文案与检视.md](./02-卡面文案与检视.md) | 投影快照与 Binder 消费、描述投影缝与令牌插值、双括号语法、词条行详述、翻牌 |
| 03 | [03-场地视图与命中认领.md](./03-场地视图与命中认领.md) | 场地面单一表面、格位命中框、一格一认领、几何占格表、多选选卡模式、Avatar 朝向 |
| 04 | [04-手牌卡组与发牌飞行.md](./04-手牌卡组与发牌飞行.md) | 手牌 hover/拖拽/回收、卡组三模式与发牌、回库 in-flight、飞牌协调器、炸牌入组、骷髅合体 |
| 05 | [05-变换塔收敛与盘面运动.md](./05-变换塔收敛与盘面运动.md) | L0–L4 塔、五次 Hermite 收敛、租约与栅栏、旋转/hop/换位执行、Step 契约与合并 |
| 06 | [06-交战表现.md](./06-交战表现.md) | Encounter Catalog 路由、rig 重绑与播放、命中帧 Impact、致死 Vacate、决斗惩罚隔离、终态守卫 |
| 07 | [07-卡面特效与装饰.md](./07-卡面特效与装饰.md) | CardEffect SO 体系（闪白/冲刺/死亡 Burn/缩小退场/触发脉冲）、尘雾、威胁荧光、场地卡生命层 |
| 08 | [08-静态Hook与诊断旁路.md](./08-静态Hook与诊断旁路.md) | 15 个 Hook 装配缝、4 个 TraceSink、CardPresentationProbe 打点门面 |

## 卡牌从数据到上屏的完整链路

以一张怪物卡为例（括号内为负责篇目）：

1. **内容定义**：一卡一文件 JSON（`NineGrid.Content`，卡面区外部）定义 defId/stats/sprites/effectAssemblies/description。
2. **Core 造卡**：`ContentSystem.CreateDraft` → `CardSpawned` 事件（携带出生攻甲血绝对值）。
3. **视图 Spawn**（01）：Flow 映射层调 `CardManagerSingleton.SpawnView(uid, defId, kind)`——实例化底盘 → 建 L0–L4 塔 → 按 Kind 显式表挂五套卡面之一入 FacePivot → 挂 Binder/EffectManager/FlipPresenter/HitProxy。
4. **卡面装配**（02）：Flow 排期器（生成引导/Settled/Impact）构造 `CardPresentationSnapshot` → `CommitPresentation` → `StandardCardView.ApplyPresentation` → `CardFacePresentationBinder` 路由图标/名字/数值/描述/节奏图标/朝向；描述经唯一投影缝填装配实参后恒静态；主视觉动画按 contentId 播 idle。
5. **发牌上场**（04→05）：`CardDeckManagerSingleton.DealCardByUid*` 占格（`RequestPlaceCard`）→ `DealFlightCoordinator` 启动 L2 收敛飞牌（预算制、旋转期间 Redirect）→ 落地 `SnapHome` + 尘雾 + 登记认领。
6. **交互**（03）：指针经 Flow `PointerHitRouter` 命中场地面 → 解析格号 → 查认领者 → 悬停（Hover 视觉+威胁荧光+简要解释）或点击（`Activate` 分流：交战/拾取/多选；空格分流跳格/探索）；一切意图经 Hook 提交 Flow Controller → IntentIntake 两轴裁决。
7. **交战**（06）：导演 Present 步回调 → Catalog 路由绑参 → rig 重绑播放 → 命中帧报 Impact（卡面数值/飘字/FX 在此消费）→ 同批盘面 delta Drain（Vacate 前）→ 致死走 Vacate/StageCorpse/FinalizeLethal。
8. **盘面运动**（05）：旋转/换位/hop 先占格事务后动画（BeatGrid 同拍栅栏、Sync 租约、起飞卸认领落地登记）。
9. **离场**：击杀 → Burn 精灵表脱卡 FX（07）；拾取 → 入手牌（04）；使用 → 缩小退场；回收 → 碎裂（04）；回库 → 垂直上飞入组（04）。

## 阅读中发现的可疑问题（非阻塞）

1. **`CardDeckTween.cs` 注释乱码**：类注释与 `LaunchFieldExitThenDeckInsert` 的 XML 注释是中文被错误编码的乱码（如「鐗岀粍鐩稿叧 DOTween 缂撳姩宸ュ叿」）；代码行为正常，建议按 UTF-8 修复注释。
2. **`GroundMotionExecutor.IsBusy` 死代码**：getter 内 `var battle = FieldBattlePresentationHook.BattleOrNull();` 取值后未使用。
3. **`CardEffectManager.TryConsumeHoverSuppression` 名不副实**：名为 Consume 但只读返回不清位（清位在播放 finally），语义上是 `IsHoverSuppressed`。
4. **`SkeletonDeckPresentationManager.PresentFusionAsync` XML 注释格式错误**：summary 段出现两个 `</summary>`。
5. **`HandCardHitProxy` 仍写自带 collider 尺寸**：`ApplyColliderSize` 写 `collider.size = handHitBoxSize`——手牌带主路径已是槽位带 AABB 数学，此 collider 仅作回退；与 ADR-0023「落格对象无自带命中区」并不冲突（手牌不是落格对象），但属于可收敛的双轨。
6. **`GroundFieldView`/`CardHandManagerSingleton`/`CardDeckManagerSingleton` 的 `GameObject.Find("Anchors")` 兜底**：生产装配应依赖场景序列化字段；Find 路径仅为未绑定兜底，注意 MainScene 装配卫生（#141）语境下勿新增依赖。
7. **`CardManagerSingleton.BootstrapPrefabs` 仅 Editor 兜底**：Player 下底盘/机关卡面完全依赖场景序列化；若场景配置丢失将 Spawn 失败（有 Error 日志，无自动恢复）。

---

## 文件覆盖清单（166 / 166）

路径相对 `Assets/Scripts/NineGrid.Presentation/Cards/`。「篇」为归属文档编号。

### 根目录（74）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 1 | `ArmorBlockDisplay.cs` | 底盘旧数值通路的护甲格显示器（逐块缩放出现/消失） | 01 |
| 2 | `AttackInputHook.cs` | 交战视图装配 + 攻击提交静态缝 | 08 |
| 3 | `AvatarBoardFacing.cs` | Avatar 水平朝向枚举（Right/Left） | 03 |
| 4 | `AvatarBoardFacingHook.cs` | 场地就绪后装配 Avatar 朝向 Controller 的静态缝 | 08 |
| 5 | `AvatarBoardFacingState.cs` | 盘面级 Avatar 朝向静态真相与 MirrorX 解析 | 03 |
| 6 | `BoardCardSelectModeController.cs` | 道具卡盘面多选模式静态会话（Begin/Toggle/Commit/Abort） | 03 |
| 7 | `BoardMotionStepScheduler.cs` | 位移类 BoardPresentationStep 执行入口（System 优先、View 回退） | 05 |
| 8 | `BoardPresentationMerge.cs` | 致死命中 delta 与 PostKill 步骤合并、剥离主目标 Remove | 05 |
| 9 | `BoardSelectParkedCardHitProxy.cs` | 多选期间驻留效果卡的点击反悔代理（Router 注册） | 03 |
| 10 | `BoardWalkInputHook.cs` | 跳格提交 + 门禁开关静态缝 | 08 |
| 11 | `BurstScatterPointSampler.cs` | 炸牌散点圆周等角采样（随机相位） | 04 |
| 12 | `CardAttackBasicAdapter.cs` | 交战 rig 执行器：方向解析、参战者就位、绑参播放、攻方抬序 | 06 |
| 13 | `CardAttackBasicDirectionRig.cs` | 单向 Timeline rig：反射重绑 target/endValue、相对几何重算、回调重挂 | 06 |
| 14 | `CardBurstScatterIntoDeckPresenter.cs` | 炸牌入组表演：散点→停稳→集体上飞入组 | 04 |
| 15 | `CardChassisPaths.cs` | 底盘/卡面/图标权威资产路径 + Editor/Player 双轨加载 | 01 |
| 16 | `CardDeckLayoutSettings.cs` | 卡组布局/发牌/ripple 参数与入场时长估算 | 04 |
| 17 | `CardDeckManagerSingleton.cs` | 卡组管理器：三模式、发牌、回库 in-flight、视觉序对账 | 04 |
| 18 | `CardDeckMode.cs` | 卡组运行模式枚举（Standby/Entry/InGame） | 04 |
| 19 | `CardDeckSlotContainer.cs` | 卡组槽容器：致密列表、TryReorderToUids、SortingLayer 切换 | 04 |
| 20 | `CardDeckTween.cs` | 卡牌 DOTween 工具（Move/Ripple/ScaleAppear/Hop 脉冲/场离入组） | 04 |
| 21 | `CardEntityLifecycleHook.cs` | 三 Manager 绑定与跨域解析静态缝 | 08 |
| 22 | `CardHandAnchorUtility.cs` | 手牌锚点名解析排序 | 04 |
| 23 | `CardHandLayoutSettings.cs` | 手牌布局/hover/拖拽参数 | 04 |
| 24 | `CardHandManagerSingleton.cs` | 手牌管理器：hover 槽位带、拖拽、回收、拾取表演承接 | 04 |
| 25 | `CardHandSlotContainer.cs` | 手牌槽容器：最多 5 张致密数组 + ripple 计划 | 04 |
| 26 | `CardManagerSingleton.cs` | 卡牌管理器：Spawn（底盘+Kind 挂面）/Release/DisplayMode/注册表审计 | 01 |
| 27 | `CardOpacityUtility.cs` | 卡下全 SpriteRenderer alpha 统一调节（按 uid 缓存原色） | 01 |
| 28 | `CardPerformanceTimelinePlayer.cs` | DOTween Timeline 反射播放器（rig 播放入口） | 01 |
| 29 | `CardPresentationKind.cs` | 表现层卡牌种类枚举（与 Core CardKind 数值对齐） | 01 |
| 30 | `CardPresentationKindResolver.cs` | defId 前缀 → Kind 兜底解析 | 01 |
| 31 | `CardPresentationProbe.cs` | 表现层打点门面（Motion/Registry/Lease/Dust 等标准 kind） | 08 |
| 32 | `CardSlotAnchorUtility.cs` | slotN 锚点解析、格号换算、开局环序、格 5 禁发牌 | 03 |
| 33 | `CardViewTween.cs` | 协程缓动工具（PunchScale/ScaleAppear/ScaleDisappear） | 01 |
| 34 | `CardVisualDriver.cs` | 卡牌视觉目标驱动（Base/Hover/Selected）+ 预制体基准缩放 + 模式查表 | 01 |
| 35 | `CardZoneOwnershipHook.cs` | Core 区域归属（ItemSlots/DrawPile）只读桥 | 08 |
| 36 | `ChoreoTraceSink.cs` | 场地编排诊断旁路（choreo 开合/Explore 轨迹/异常） | 08 |
| 37 | `CombatPresentationContracts.cs` | 盘面表现契约结构族（Step/PostKill/Pickup/UseItem Result 等） | 05 |
| 38 | `DamageNumberHook.cs` | 伤害飘字入口（总伤/治疗/血伤/甲伤四口） | 08 |
| 39 | `DealFlightContext.cs` | 飞牌预算上下文（busy/并发/旋转步/视觉目标格） | 04 |
| 40 | `DealFlightHandle.cs` | 单张飞牌句柄（TrackedSlot + WaitSettleAsync） | 04 |
| 41 | `DealFlightKind.cs` | 飞牌种类枚举（Explore/Drain） | 04 |
| 42 | `DealFlightLayoutSettings.cs` | 贝塞尔飞牌与预算参数 | 04 |
| 43 | `DealFlightMath.cs` | 二阶贝塞尔纯数学（EditMode 可测） | 04 |
| 44 | `DealSettleBudget.cs` | 可消费就位时间预算（逐帧扣减、跳变罚时、软着陆） | 04 |
| 45 | `DealVisualTargetResolver.cs` | 发牌视觉目标预解（跳过同批 Deal 收集后续 Rotate） | 04 |
| 46 | `DescriptionDisplayHook.cs` | 已退役的动态描述 no-op 壳（禁复活） | 08 |
| 47 | `DiagnosticOutputHook.cs` | 诊断 Recorder Attach/Detach 接线入口 | 08 |
| 48 | `DirectorAttackPresentTargeting.cs` | Present 目标裁决（Resolve 批捕获 combatUid、嘲讽重定向判定） | 06 |
| 49 | `ExploreInputHook.cs` | 空格探索提交静态缝 | 08 |
| 50 | `FieldBattlePresentationHook.cs` | 交战视图宿主桥（BattleOrNull） | 08 |
| 51 | `FieldBattleView.cs` | 交战场景视图薄壳（Adapter+Catalog，余者转发 System） | 06 |
| 52 | `FlowFieldTraceSink.cs` | 占格/发牌/手牌/租约/栅栏诊断旁路 | 08 |
| 53 | `GroundCardHitProxy.cs` | 场地卡命中代理：认领登记、Hover/点击分流、输入门禁诊断 | 03 |
| 54 | `GroundFieldGeometryHook.cs` | 场地视图宿主桥（FieldOrNull） | 08 |
| 55 | `GroundFieldHitSurface.cs` | 场地面单一表面：世界点→格号→认领者派发 | 03 |
| 56 | `GroundFieldLayoutSettings.cs` | 场地布局与动效参数 | 03 |
| 57 | `GroundFieldSnapshot.cs` | 九格占格只读快照结构 | 03 |
| 58 | `GroundFieldView.cs` | 场地场景视图适配（锚点/命中框/Fusion + Geometry 薄转发） | 03 |
| 59 | `GroundSlotHitProxy.cs` | 遗留空槽 Hit 壳（不注册 Router、装配时被销毁） | 03 |
| 60 | `GroundSlotRelation.cs` | 格位结构关系 Flags | 03 |
| 61 | `GroundSlotTopology.cs` | 九宫格方位静态查表（正交/对角掩码、外圈环） | 03 |
| 62 | `HandCardHitProxy.cs` | 手牌命中代理（Hand 表面；collider 回退路径） | 04 |
| 63 | `PerfTraceSink.cs` | PerfTrace 旁路（Record/BoardSnap/Beat 开合） | 08 |
| 64 | `PickupInputHook.cs` | 拾取写 Core 静态缝（返回表现摘要） | 08 |
| 65 | `PixelCardPackSpriteLibrary.cs` | 数字/护甲块 Sprite 库 SO | 01 |
| 66 | `PixelDigitDisplay.cs` | 像素数字排版器（底盘旧数值通路） | 01 |
| 67 | `RecycleItemInputHook.cs` | 回收提交静态缝 | 08 |
| 68 | `RegistryTraceSink.cs` | RegistryTrace 旁路（交互戳点/可疑释放/拾取审计） | 08 |
| 69 | `SkeletonDeckLayoutSettings.cs` | 骷髅合体动效参数 | 04 |
| 70 | `SkeletonDeckPresentationManager.cs` | 骷髅合体表演（撞点→闪白→结果卡→入组） | 04 |
| 71 | `SkeletonFusionPresentationRequest.cs` | 合体表现请求 DTO | 04 |
| 72 | `StandardCardView.cs` | 卡牌底盘宿主：SortingGroup、Binder 挂载、投影分发 | 01 |
| 73 | `TriggerPulseOutputHook.cs` | 触发脉冲 Hub 生产装配/重置入口 | 08 |
| 74 | `UseItemInputHook.cs` | 用牌提交静态缝 | 08 |

### Anim/（6）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 75 | `Anim/CardAnimFrameSource.cs` | folder/atlas 帧序列加载与帧名数字序排序 | 01 |
| 76 | `Anim/CardMainVisualAnchorMode.cs` | 主视图 Mask 锚定模式枚举（BottomCenter 等） | 01 |
| 77 | `Anim/CardMainVisualMaskAnchor.cs` | 主视图 Mask 锚点：局部空间锚定、Mask range 同步、Layer Propagate | 01 |
| 78 | `Anim/CardMainVisualPlacement.cs` | 主视图 scale/参考帧/锚定+偏移一次性写入（拒 NaN） | 01 |
| 79 | `Anim/CardSpriteAnimPlayer.cs` | 卡面主视图帧动画播放器（切槽定位一次、播帧只换 sprite） | 01 |
| 80 | `Anim/SpriteSheetLoopPlayer.cs` | 通用精灵表循环播放器（Burn 退场/预览共用） | 01 |

### Battle/（11）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 81 | `Battle/BattleBindParams.cs` | Rig 绑参 DTO 与 SafeFallback 构造 | 06 |
| 82 | `Battle/BattleEncounterCatalogSO.cs` | (intent, 玩家, 怪物) → Profile 路由表 SO | 06 |
| 83 | `Battle/BattleEncounterProfileSO.cs` | 战斗编排预设（击退锁区间、回调时机、Teardown 开关） | 06 |
| 84 | `Battle/BattleFinalStateGuard.cs` | 交战终态守卫（快照/回锚/软补间/硬 Snap） | 06 |
| 85 | `Battle/BattleHitFlashTimingPolicy.cs` | 回调时机策略枚举（Heuristic/Explicit） | 06 |
| 86 | `Battle/BattleIntent.cs` | 交战意图四态与 counter/lethal 判定 | 06 |
| 87 | `Battle/BattleParticipantIds.cs` | 参战双方 contentId 对（通配匹配） | 06 |
| 88 | `Battle/BattlePresentationRouter.cs` | 纯逻辑路由（精确>通配>SafeFallback） | 06 |
| 89 | `Battle/FieldBattlePresentationExecutor.cs` | 交战 Present 执行缝（命中/反击/致死/决斗隔离/对账触发） | 06 |
| 90 | `Battle/IFieldBattleView.cs` | 交战视图窄接口 | 06 |
| 91 | `Battle/IFieldBattleViewBinding.cs` | 交战 System Bind 面 | 06 |

### Convergence/（18）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 92 | `Convergence/BeatGrid.cs` | 节拍栅格：同拍共享 sourceTime + 就位栅栏 | 05 |
| 93 | `Convergence/CardTransformTower.cs` | 每卡固定 L0–L4 变换塔 + FacePivot | 05 |
| 94 | `Convergence/CommitmentKind.cs` | 表演承诺标签（Sync/Async） | 05 |
| 95 | `Convergence/ConvergenceCurve.cs` | 三维收敛曲线（Redirect C1 连续） | 05 |
| 96 | `Convergence/ConvergenceCurve1D.cs` | 单维五次 Hermite 闭式解 | 05 |
| 97 | `Convergence/ConvergenceDiagProbe.cs` | 收敛范式诊断门面（双写 Perf/CoreLog） | 05 |
| 98 | `Convergence/EffectFrameConvergence.cs` | L3 特效位移门面（末态回零、ParkRoot、冲刺往返） | 05 |
| 99 | `Convergence/FlightSortingChannel.cs` | 飞行排序通道（起飞抬层、L2 完成掉回域权威序） | 05 |
| 100 | `Convergence/HandoffState.cs` | 域边界交接快照（位姿+速度） | 05 |
| 101 | `Convergence/ICornerReshapePolicy.cs` | 墙角策略注入点（现役纯五次无 reshape） | 05 |
| 102 | `Convergence/IDependencyDag.cs` | 升级门预留接口 + Null 实现 | 05 |
| 103 | `Convergence/IHandoffEndpoint.cs` | 全域一致交接口（Evict/Admit） | 05 |
| 104 | `Convergence/LayerConvergenceDriver.cs` | 单层 localPosition 收敛驱动器（L2/L3 各一） | 05 |
| 105 | `Convergence/LeaseArbiter.cs` | 租约仲裁（Sync 独占、征用带速度、纪律 B 报警） | 05 |
| 106 | `Convergence/LeaseTypes.cs` | 租约键/请求/结果数据类型 | 05 |
| 107 | `Convergence/PresentationClock.cs` | 表现层权威调度墙钟 | 05 |
| 108 | `Convergence/SlotFrameConvergence.cs` | L2 格位收敛门面（SnapHome/BeginDeal/Sanitize/视觉位读取） | 05 |
| 109 | `Convergence/TowerLayer.cs` | 塔层枚举 L0–L4 | 05 |

### Effects/（22）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 110 | `Effects/CardAttackPresentationKind.cs` | 攻击表现组合头种类枚举 | 07 |
| 111 | `Effects/CardBoardDirection.cs` | 八向方向枚举 | 07 |
| 112 | `Effects/CardBoardDirectionUtility.cs` | 八向工具（格位推方向/取反/偏移向量） | 07 |
| 113 | `Effects/CardDOTweenDirectionUtility.cs` | Tween 片段左右朝向 X 取反 | 07 |
| 114 | `Effects/CardEffectBinding.cs` | 效果装配槽（默认 SO + 八向覆盖） | 07 |
| 115 | `Effects/CardEffectCallbackAction.cs` | 统一回调动作枚举与整型/名称/中文别名解析 | 07 |
| 116 | `Effects/CardEffectInvokeContext.cs` | 效果调用上下文（Kind/方向/格位/编排标记） | 07 |
| 117 | `Effects/CardEffectKind.cs` | 反馈种类枚举（Attack..EffectTrigger） | 07 |
| 118 | `Effects/CardEffectManager.cs` | 单卡反馈管理器（主通道串行 + 闪白/脉冲旁路 + Callback 入口） | 07 |
| 119 | `Effects/CardEffectManagerExtensions.cs` | ManagedCard 效果便捷扩展 | 07 |
| 120 | `Effects/CardEffectOrchestrationUtility.cs` | 普攻组合延迟 schema 读取辅助 | 07 |
| 121 | `Effects/CardEffectPlayContext.cs` | 效果播放上下文（Self/Other 世界位等） | 07 |
| 122 | `Effects/CardEffectSO.cs` | 效果 SO 基类（元数据 + PlayAsync/Stop） | 07 |
| 123 | `Effects/CardSpriteHitFlashExecutor.cs` | 闪白运行时执行器（HitFlash 材质实例驱动） | 07 |
| 124 | `Effects/CardTweenClip.cs` | 内化 Tween 片段数据 | 07 |
| 125 | `Effects/CardTweenClipType.cs` | 片段类型枚举（现仅 LocalMove） | 07 |
| 126 | `Effects/Implementations/CardDOTweenSequenceEffectSO.cs` | 并行片段序列效果 SO（数据化、结束还原） | 07 |
| 127 | `Effects/Implementations/CardSpriteHitFlashEffectSO.cs` | 受击闪白效果 SO | 07 |
| 128 | `Effects/Implementations/CardSpriteSheetBurnExitEffectSO.cs` | 默认死亡退场：Burning 精灵表脱卡 FX | 07 |
| 129 | `Effects/Implementations/CardTweenDirectionalLungeEffectSO.cs` | 方向冲刺往返效果 SO（L3 优先、无塔回退） | 07 |
| 130 | `Effects/Implementations/CardTweenDirectionalPunchEffectSO.cs` | 方向加权 PunchScale 受击效果 SO | 07 |
| 131 | `Effects/Implementations/CardTweenScaleOutEffectSO.cs` | 缩小消失效果 SO（Use 默认退场） | 07 |

### Ground/（8）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 132 | `Ground/DealFlightCoordinator.cs` | 飞牌协调器（Explore/Drain 探针、环移 Redirect、取消回滚） | 04 |
| 133 | `Ground/GroundMotionExecutor.cs` | 场地运动执行器（旋转/hop/换位/移除/Avatar 入场/空格分流） | 05 |
| 134 | `Ground/GroundOccupancyIndex.cs` | 几何占格双向表 + 认领登记宿主（拒静默挤占、批量置换事务） | 03 |
| 135 | `Ground/IDealFlightHost.cs` | 飞牌宿主窄接口 | 04 |
| 136 | `Ground/IGroundFieldView.cs` | 场地视图窄接口 | 03 |
| 137 | `Ground/IGroundFieldViewBinding.cs` | 场地 System Bind 面 | 03 |
| 138 | `Ground/SlotClaimant.cs` | 认领者数据（Owner+文案+Activate/Hover 回调） | 03 |
| 139 | `Ground/SlotClaimRegistry.cs` | 格位→认领者登记（一格一认领、冲突保留先到者） | 03 |

### Presentation/（16）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 140 | `Presentation/CardDetailDescriptionComposer.cs` | 旧详情合成退役空壳 | 02 |
| 141 | `Presentation/CardFaceAttackPatternIconResolver.cs` | 攻击模式槽图标解析（正交/全向/斜角三档，临时接线） | 02 |
| 142 | `Presentation/CardFaceDescriptionComposer.cs` | 双括号语法 → TMP 富文本合成 | 02 |
| 143 | `Presentation/CardFaceDescriptionIconCatalogSO.cs` | 项目级词条表 SO（code/名字/介绍/颜色/分区） | 02 |
| 144 | `Presentation/CardFaceDescriptionInlineIconStyleSO.cs` | 内联图标 em 布局项目级配置 SO | 02 |
| 145 | `Presentation/CardFaceDescriptionParamFiller.cs` | `{装配id.键}` 令牌插值（限定式/歧义保留） | 02 |
| 146 | `Presentation/CardFaceDescriptionProjector.cs` | ADR-0035 唯一描述投影缝（Inspect/Instance 同文） | 02 |
| 147 | `Presentation/CardFaceDescriptionSpriteAssetBuilder.cs` | 内联图标运行时 TMP_SpriteAsset 打包器 | 02 |
| 148 | `Presentation/CardFaceFlipPresenter.cs` | 翻牌视觉（FacePivot 采样 Flip.anim、中点切面） | 02 |
| 149 | `Presentation/CardFacePresentationBinder.cs` | L4 卡面 Kind Binder（图标/数值/描述/节奏矩阵/朝向消费） | 02 |
| 150 | `Presentation/CardGlossaryTerms.cs` | 检查描述 `[[词条]]` 抽取 seam | 02 |
| 151 | `Presentation/CardInspectDetailComposer.cs` | 详述面板 faceIntro/牌组介绍合成 | 02 |
| 152 | `Presentation/CardInspectGlossaryListView.cs` | 详述词条行列表视图（动态装行 + hover 槽） | 02 |
| 153 | `Presentation/CardInspectGlossaryRowView.cs` | 单条词条行视图 | 02 |
| 154 | `Presentation/CardPresentationSnapshot.cs` | 面向卡面消费的胖投影快照 | 02 |
| 155 | `Presentation/ICardFaceBinder.cs` | Kind 卡面消费端接口 | 02 |

### Slots/（8）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 156 | `Slots/CardFaceSlotCodes.cs` | 装配槽稳定代号常量 | 01 |
| 157 | `Slots/CardFaceSlotDefinition.cs` | 槽表单条契约（代号/中文注释/角色） | 01 |
| 158 | `Slots/CardFaceSlotNodeMap.cs` | 槽代号 → 模板节点名候选映射（Renderer/TMP/配对图标查找） | 01 |
| 159 | `Slots/CardFaceSlotRegistrySO.cs` | 项目级装配槽注册表 SO + 默认槽表 | 01 |
| 160 | `Slots/CardFaceSlotResolver.cs` | 槽解析纯函数（内容覆盖→模板兜底；数值缺省 0） | 01 |
| 161 | `Slots/CardFaceSlotRole.cs` | 槽角色 Flags | 01 |
| 162 | `Slots/CardFaceSortingLayers.cs` | 卡面通层 sortingOrder 分区常量（0–99）与 Mask 偏移 | 01 |
| 163 | `Slots/CardFaceSortingOrderValidator.cs` | 通层重复 order 编辑器校验 | 01 |

### Vfx/（4）

| # | 文件 | 一句话说明 | 篇 |
|---|---|---|---|
| 164 | `Vfx/BoardRangeGlowFx.cs` | 悬停范围荧光：怪物威胁（暖橙）/ 机关邻接影响（冷青）/ Avatar 可攻击范围（暖橙）；程序化 9-slice 光环 + 加色材质 + 自检淡出 | 07 |
| 165 | `Vfx/CardEdgeDustFx.cs` | 边沿尘雾（Place 喷发 / Trail 双尾迹，池化与预算保护） | 07 |
| 166 | `Vfx/CardEdgeDustFxSettingsSO.cs` | 尘雾参数 SO（全局开关默认关） | 07 |
| 167 | `Vfx/BoardCardLifeFx.cs` | 场地卡生命层（ADR-0051）：L1 唯一写者，悬浮飘动 + 冲击弹簧 + 落位沉降，权威运动接手即硬归零 | 07 |
