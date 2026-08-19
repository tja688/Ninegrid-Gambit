# Presentation/Flow 代码文档库 —— 总览

> 对应代码：`Assets/Scripts/NineGrid.Presentation/Flow/`（2026-08 快照 211 个 .cs；2026-08-13 新增 `Presentation/ScreenImpact.cs`、`Presentation/BoardImpactSignatures.cs` 后 213，同日新增 `BattleLog/` 五件后 218 —— 下方清单已含 BattleLog，合计 216）。
> 本文档库是预发布一次性权威镜像（用户明确豁免），以当下代码实际实现为准；日常维护仍以 [`docs/code-map/`](../../../../docs/code-map/) 与 [`docs/adr/`](../../../../docs/adr/) 为准。
> 兄弟文档库：[`Assets/Docs/Presentation/Cards/`](../Cards/)、[`Assets/Docs/Presentation/Systems与通信/`](../Systems与通信/)。

## Flow 在 Presentation 中的角色

Flow 是表现层的**流程编排区**：场景装配（`Setup/PresentationCompositionRoot`、`PresentationSceneBindings`，非本目录）把 View 宿主与 System 接好线后，Flow 负责「一整局游戏怎么走」——主菜单 → Run → 节点循环 → 战斗一拍一拍锁步表演 → 结算选房 → 存档/读档 → 胜负回主菜单。驱动关系一句话：

```text
场景装配（Setup/CompositionRoot）
  → Flow 编排（GameFlowOrchestrator / BattleSessionExecutor / PresentationDirector）
    → Core 领域（Command 解算，EventLog 是唯一事实输出）
      → 回传表现（EventLog 投影 → Present 通道 / Beat Handler → 卡面 Commit / HUD / 音视效）
```

三条铁律贯穿全区：**跑图进度真相在 Core `RunModel`**（ADR-0021，壳层只投影）；**Core 一次只前进一批，表演未 ack 不解算下一批**（ADR-0001 Batch-ack）；**输入经 IntentIntake 两轴门禁，主线忙一律 strict-drop**（ADR-0004）。

## 完整子目录树

```text
Flow/
├── GameFlow/                 主流程编排（Orchestrator / RunOptions / Signal / View 接口）
│   └── RunSave/              Run 存档（检查点服务 + 落盘接口）
├── Tutorial/                 教学关卡（导演 / 卡组计划 / 进度存储 / 波次命令）
├── Platform/                 平台接入（成就 ID / 成就与信息 Hook 面）
├── Transitions/              局内过场（方向袋 / 过场服务 / 参数 SO）
├── BattleSession/            战斗会话（Executor 六件 + 投影协调 + 盘面播放器 + 作弊）
├── Presentation/             导演时间线 / 意图剧本 / 锚点排期 / 脉冲与 Cue / 抖屏（82 个文件，全区最大）
├── Diagnostics/              五轨诊断日志（Battle / Flow / Perf / Registry / Console + 手动快照）
├── BattleLog/                人读战斗日志（EventLog 提炼 → 面板可看，与 Diagnostics 无关）
├── RoomIcons/                选房图标（Presenter / 落格 / 驻留 / 软占）
├── ShopBoard/                商店房场地板
├── TavernBoard/              卡店场地板（含二级候选面）
├── RewardBoard/              特殊奖励房场地板
├── AttributeBoard/           属性房三选二（玩法已废止，代码保留）
├── InRoomBoard/              房内共享基建（落格规划 / 货架动画 / 购领接手牌 / 扣金 HUD）
├── BoardBriefTip/            简要解释文字框 + 楼层提示
├── BattleInfoPreview/        战前信息预览面板
└── （根目录 43 个文件）        会话 View / 指针命中 / HUD / 遗物 / 检视 / 投影工具 / QuickTest / Hook
```

## 文档分篇

| 篇 | 覆盖 | 文件数 |
|----|------|-------|
| [01-GameFlow主编排与流程壳](01-GameFlow主编排与流程壳.md) | GameFlow/、RunSave、流程壳 View 与 Hook、壳相位、QuickTest 三件、Transitions/ | 16 |
| [02-战斗会话与批次投影](02-战斗会话与批次投影.md) | BattleSession/、会话 Controller、EventLog 投影与扫描器、卡面映射 | 20 |
| [03-表演导演时间线与编排调度](03-表演导演时间线与编排调度.md) | Director、Timeline、Step、Present 通道、稳定化/敌方行动/洗回调度 | 27 |
| [04-意图门禁与剧本工厂](04-意图门禁与剧本工厂.md) | InputIntent、合法性裁决、八个 IntentScriptFactory、Avatar 走格 | 22 |
| [05-表演锚点排期与触发脉冲](05-表演锚点排期与触发脉冲.md) | BeatScheduler 与六 Handler、TriggerPulseHub 与 Sink、抖屏与签名冲击、Cue 声明、金币表现 | 31 |
| [06-教学关卡模块（Tutorial）](06-教学关卡模块（Tutorial）.md) | Tutorial/ 五件 | 5 |
| [07-平台接入（Platform）](07-平台接入（Platform）.md) | Platform/ 三件 | 3 |
| [08-房间流程与场地板](08-房间流程与场地板.md) | RoomIcons/、四套房内板、InRoomBoard/、BoardBriefTip/、Bounce 选择、两 CoreHook | 33 |
| [09-指针命中、HUD与检视](09-指针命中、HUD与检视.md) | 指针仲裁体系、玩家 HUD、遗物栏、卡牌详述、BattleInfoPreview/ | 33 |
| [10-诊断与日志（Diagnostics）](10-诊断与日志（Diagnostics）.md) | Diagnostics/ 四轨 Trace 全家 | 23 |
| [11-人读战斗日志（BattleLog）](11-人读战斗日志（BattleLog）.md) | BattleLog/ 采集与聚合、命名与配色、面板层级 | 5 |
| 合计 | | **216** |

## 主流程状态机全景

壳相位七态（`GameFlowShellState`）：MainMenu / BattleStub / RewardChoice / RoomChoice / RoomEvent / VictoryNotice / DefeatNotice，唯一写入口 `SetGameFlowShellStateCommand`。

```mermaid
flowchart TD
    MM[主菜单 MainMenu] -->|BeginGameFlowRunCommand<br/>四模式: 正式/QuickTest/读档恢复/教学| START[Orchestrator.Start]
    START -->|教学载荷| TUT[RunTutorialAsync<br/>四波教学 → 可续正式局]
    START -->|恢复载荷| RESTORE[TryBootstrapRestoredRun<br/>Core 覆盖恢复+壳序号对齐]
    START --> LOOP{节点循环<br/>RunNodeCycleAsync}
    RESTORE --> LOOP
    TUT -->|完成| LOOP

    LOOP -->|节点 4/7| NC[PlayNonCombatNodeAsync<br/>仅 StartNode 放图标]
    LOOP -->|其余节点| BATTLE[PlayRealBattleAsync = BattleStub<br/>①存档检查点(消耗RNG前) ②战前预览硬阻塞<br/>③StartBattleNode 入场发牌]

    BATTLE --> INTER[战斗互动循环<br/>意图→IntentIntake两轴门禁→剧本工厂<br/>→主线时间线 Resolve批/Present批/ack<br/>→稳定化→敌方行动]
    INTER -->|清关 SettlementReady| REWARD[PlayRewardChoiceAsync = RewardChoice<br/>Bounce 扇形三选一 + 未用帮助卡折金]
    INTER -->|Avatar 战败| DEFEAT[DefeatNotice]

    REWARD --> ROOM[PlayRoomIconChoiceAsync = RoomChoice<br/>场地图标落格→BoardWalk驻留1s<br/>→SelectRoom+EnterRoom]
    NC --> ROOM
    ROOM -->|战斗/导航房| LOOP
    ROOM -->|消费/特殊房| INROOM[PresentInRoomSessionAfterEnterAsync = RoomEvent<br/>Shop/Tavern/Reward 场地板<br/>购买→接手牌→踩离开格离房]
    INROOM --> LOOP

    LOOP -->|Core phase == Victory| VICT[VictoryNotice]
    VICT --> SUMMARY[RunSummaryPanel 结算面板]
    DEFEAT --> SUMMARY
    SUMMARY --> MM

    MM -.->|读档 RequestLoadSlot<br/>收口→BeginRun(Restore)| START
```

战斗内「一拍」的完整生命周期（Batch-ack）与 Handler 消费详见 [03](03-表演导演时间线与编排调度.md) §1 与 [05](05-表演锚点排期与触发脉冲.md) §1-2；存档/读档链路见 [01](01-GameFlow主编排与流程壳.md) §6。

## 文件覆盖清单（211 个，一个不漏）

> 格式：文件相对路径（相对 `Assets/Scripts/NineGrid.Presentation/Flow/`）｜一句话职责｜归属篇。

### 根目录（43）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `BattleSessionController.cs` | 局内会话场景 View：宿主引用绑定 + Runtime 安装回调，业务转发 Executor | 02 |
| `BattleUiDimmerOverlay.cs` | 局内半黑屏遮罩，引用计数 Acquire/Release | 09 |
| `BoardPresentationStepProjector.cs` | EventLog → 带 Commitment 标签的有序盘面表演步骤投影 | 02 |
| `BoardSlotWorldPlacement.cs` | 落格只对齐世界位置、不 parent 锚点（ADR-0024） | 08 |
| `BounceFanChoicePresenter.cs` | Bounce 扇形三选一点选表现（纯表现卡入场/推挤/掉落） | 08 |
| `CardInspectIconHover.cs` | 详述卡面图标悬停解析：内联 sprite + 卡面机制图标 → 词条栏首行（ADR-0037） | 09 |
| `CardInspectOverlayPresenter.cs` | 右键卡牌详述覆层（真卡面 + 术语表 + 半黑屏） | 09 |
| `ContentIconSlotBinder.cs` | content defId 列表刷到锚点槽 SpriteRenderer | 09 |
| `ContentIconSlotHitProxy.cs` | 遗物/技能图标槽命中代理（左拖右检由 Router 特判） | 09 |
| `CoreCardPresentationMapper.cs` | Core/Content → 卡面胖投影，CommitPresentation 唯一出口 | 02 |
| `DamageNumberManagerSingleton.cs` | 伤害飘字统一管理（血/甲拆分色、动态缩放） | 09 |
| `EffectCountdownProjection.cs` | 从卡 JSON 装配解析倒计时投影键与 period（ADR-0035） | 09 |
| `GameFlowController.cs` | 主流程场景 View：主菜单输入轮询 + Notice/Panel 投影 | 01 |
| `GameFlowShellHook.cs` | 流程壳 Controller 接线入口（PublishState 镜像已停用） | 01 |
| `GoldGainPresentationBinder.cs` | 消费金币增益事件：花费 Snap / 增益飞币 + 数字窗 | 09 |
| `GoldHudDomainHost.cs` | 金币 HUD VFX 域宿主（sink 坐标 / coin sprite / 吞币 Punch） | 09 |
| `GoldHudNumberWindow.cs` | 金币数字时间窗纯数学（首达前持旧值、插值到末达） | 09 |
| `HelpCardBoardSelectResolver.cs` | 帮助卡打出模式解析：None / 单拖 / 多选 + prompt | 02 |
| `IMultiColliderPointerHitTarget.cs` | 多 Collider/自定义命中表面接口（场地面九格） | 09 |
| `IPointerHitTarget.cs` | 轮询式指针命中目标契约（sort/type 双键仲裁） | 09 |
| `IUITestKeyConsumer.cs` | UITest 可选按键消费者契约 | 09 |
| `KeyboardUtility.cs` | KeyCode → Input System Key 映射读口 | 09 |
| `PlayerInfoHudPresenter.cs` | 玩家血/甲/金 HUD（血槽伸长、悬停切换、金币时间窗） | 09 |
| `PointerHitRegistry.cs` | 命中目标静态登记表（去重 + 清假 null） | 09 |
| `PointerHitRouter.cs` | 每帧指针轮询仲裁与 Enter/Exit/Down 合成 | 09 |
| `PointerHitSurfacePriorities.cs` | 表面类型优先级常量（Field 28 > Hand 20 > Overlay 10） | 09 |
| `PointerHitSurfacePriorityValidator.cs` | 装配期命中优先级同分冲突扫描 | 09 |
| `QuickTestDeckCatalog.cs` | `\0`–`\9` 十条 QuickTest 通道预设真源 + 菜单文本 + 卡面短描述 | 01 |
| `QuickTestRunOptions.cs` | QuickTest 载荷：节点序 / 钉死首关牌组 / skillIds / trapIds | 01 |
| `QuickTestRunPlanner.cs` | QuickTest 整局内容节点队列规划（Sequential/Shuffled） | 01 |
| `RelicCountdownProjection.cs` | 遗物倒计时投影薄封装 | 09 |
| `RelicHudHook.cs` | 遗物栏静态 Hook 面（同步/丢弃/拖拽/检视/倒计时 Commit） | 09 |
| `RelicIconSlotView.cs` | 遗物栏单槽视图（锚点命中 + 子树显示模板） | 09 |
| `RelicManagerSingleton.cs` | 遗物栏表现单例（刷图标、倒计时、拖拽回收） | 09 |
| `RewardChoiceCoreHook.cs` | SelectReward / SkipHelpChoice / RefreshShop 写 Core 门面 | 08 |
| `RoomChoiceCoreHook.cs` | SelectRoom / EnterRoom 写 Core 门面 | 08 |
| `SelectorManagerSingleton.cs` | Bounce 扇形选择统一入场退场门面 | 08 |
| `ShuffleIntoDeckPresentationScanner.cs` | EventLog 洗入卡组事件扫描（四类前缀识别） | 02 |
| `SkeletonFusionPresentationScanner.cs` | 骷髅合体批次扫描与参与者索引 | 02 |
| `UiOverlayHitProxy.cs` | UI 叠层命中代理（Swallow/关详述/半黑屏背景/关菜单） | 09 |
| `UiPanelRouter.cs` | 主菜单/局内壳/叠层面板互斥路由 | 09 |
| `UITestBootstrap.cs` | 临时 UI 测试引导（仅 UITestScene） | 09 |
| `WorldPointerUtility.cs` | 世界空间指针唯一读口（New Input System） | 09 |

### GameFlow/（6）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `GameFlow/GameFlowOrchestrator.cs` | 主流程编排深模块：四模式开局 → 节点循环 → 胜负收口 | 01 |
| `GameFlow/GameFlowRunOptions.cs` | 开局选项（载荷即模式：QuickTest/Restore/Tutorial） | 01 |
| `GameFlow/GameFlowSignal.cs` | 编排信号 struct（SettlementReady / BattleEnded） | 01 |
| `GameFlow/IGameFlowView.cs` | 流程场景视图接口（Notice/Panel/时序参数/QuitGame） | 01 |
| `GameFlow/RunSave/RunSaveService.cs` | 存档服务：检查点捕获、手动槽写入、读档收口重开 | 01 |
| `GameFlow/RunSave/RunSaveStore.cs` | 落盘后端接口 + 装配缝（ES3 桥注册点） | 01 |

### Tutorial/（5）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `Tutorial/TutorialBattleDirector.cs` | 教学战斗导演：轮询 Core 状态推进四波内容与事件 | 06 |
| `Tutorial/TutorialDeckPlan.cs` | 教学卡组计划：阅读序 → 抽牌堆序翻译 + 开局选项 | 06 |
| `Tutorial/TutorialProgressStore.cs` | 教学完成状态持久化（独立存档槽） | 06 |
| `Tutorial/TutorialRunOptions.cs` | 教学开局选项（完成后是否续正式局） | 06 |
| `Tutorial/TutorialWaveCommands.cs` | 教学波次 Core 命令（清场 + 注入下一波） | 06 |

### Platform/（3）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `Platform/AchievementIds.cs` | 成就与统计 ID 常量表（与 Steam 后台对齐） | 07 |
| `Platform/PlatformAchievements.cs` | 平台成就接口 + Hook + 静态门面（Unlock/Stat/Flush） | 07 |
| `Platform/PlatformInfo.cs` | 平台信息接口 + Hook + 门面（玩家名/语言/Rich Presence） | 07 |

### Transitions/（3）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `Transitions/DirectionalBiasPicker.cs` | 过场方向四向袋洗牌抽取（抽完再洗防连续同向） | 01 |
| `Transitions/RunSceneTransitionService.cs` | 局内过场服务：同层 Directional / 跨层 Round + Cover 挂起 | 01 |
| `Transitions/RunSceneTransitionSettingsSO.cs` | 过场参数 SO（时长/曲线/素材/FaderId 常量） | 01 |

### BattleSession/（14）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `BattleSession/BattleSessionCheat.cs` | Dev/QuickTest 作弊静态工具（改血攻/挂技能/强制胜利/跨层） | 02 |
| `BattleSession/BattleSessionEndedEvent.cs` | 战斗结束事件载荷（Victory） | 02 |
| `BattleSession/BattleSessionExecutor.cs` | 会话执行体主体：Bind/通道装配/结算唤醒/战斗结束 | 02 |
| `BattleSession/BattleSessionExecutor.Opening.cs` | 开局序列：StartNode → 表演计划捕获 → 发牌动画 | 02 |
| `BattleSession/BattleSessionExecutor.Choice.cs` | 奖励 Bounce 选择 + 未用帮助卡折金表演 | 02 |
| `BattleSession/BattleSessionExecutor.BoardSelect.cs` | 帮助卡棋盘多选模式（校验/提交/中止回手） | 02 |
| `BattleSession/BattleSessionExecutor.UseItem.cs` | 用牌 Present 主体（洗回前缀/飘字/盘面 delta） | 02 |
| `BattleSession/BattleSessionExecutor.OrphanReward.cs` | 孤儿奖励 UI 恢复（Pending=Reward 无浮层时补开） | 02 |
| `BattleSession/BattleSessionSettlementReadyEvent.cs` | 节点结算就绪标记事件（空 struct） | 02 |
| `BattleSession/BoardPresentationPlayer.cs` | 盘面表演播放器：步骤流 Drain / 洗回 / 融合 / 卡组对账 | 02 |
| `BattleSession/CoreBatchProjectionCoordinator.cs` | 解算批投影协调：EventLog → 表演摘要 + 副作用路由 | 02 |
| `BattleSession/IBattleSessionView.cs` | 会话视图注入面（七 Manager 宿主 + Runtime 回调） | 02 |
| `BattleSession/NodeSettlementReadiness.cs` | ADR-0021 结算就绪判定 | 02 |
| `BattleSession/PresentationOutputProjector.cs` | 单向输出投影薄封装（Pickup 旁路/坐标解析/视觉对账） | 02 |

### Presentation/（80）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `Presentation/PresentationDirector.cs` | 双时间线唯一所有者：strict-drop 门禁 / 外部租约 / 旁路 | 03 |
| `Presentation/BattleTimeline.cs` | 串行 Step 队列（Enqueue/Tick/Clear + 换步诊断） | 03 |
| `Presentation/ITimelineStep.cs` | 原子 Step 接口（Tick 返回状态） | 03 |
| `Presentation/TimelineStepStatus.cs` | Step 状态与开批结果枚举 | 03 |
| `Presentation/ITimelineDiagnosticSink.cs` | 换步诊断接口 | 03 |
| `Presentation/BatchLockstepSteps.cs` | 批次锁步四件套（门/解算步/通道接口/表演步） | 03 |
| `Presentation/PresentationSyncBatchGate.cs` | Core OpenBatch/FinishBatch 缝接成批次门 | 03 |
| `Presentation/DelayStep.cs` | 显式延迟 Step（注入 deltaTime 累计） | 03 |
| `Presentation/ParallelForkStep.cs` | 单 Step 内并行子流 | 03 |
| `Presentation/TimelineBranchStep.cs` | 条件分支（Tick 时求值追加后续步骤） | 03 |
| `Presentation/AttackPostHitBranchStep.cs` | 命中批后击杀/反击分支 | 03 |
| `Presentation/ExternalMainlineHoldStep.cs` | 主线外部租约步（持忙至显式释放） | 03 |
| `Presentation/QueuedBoardPresentChannel.cs` | 通用盘面 Present 通道（异步 drain） | 03 |
| `Presentation/CombatAttackPresentChannel.cs` | 攻击命中 Present 通道（lunge/受击） | 03 |
| `Presentation/CombatCounterPresentChannel.cs` | 反击/敌方开火 Present 通道 | 03 |
| `Presentation/UseItemPresentChannel.cs` | 用牌 Present 通道 | 03 |
| `Presentation/ShufflePrefixedPresentChannel.cs` | 洗回前缀装饰通道（EditMode 假 tick） | 03 |
| `Presentation/BoardStabilizationScheduler.cs` | 盘面稳定化逐轮锁步 pacing（ADR-0034） | 03 |
| `Presentation/EnemyActionPhaseScheduler.cs` | 敌方行动阶段分拍（报名/逐条打击/收尾） | 03 |
| `Presentation/ShuffleIntoDeckScheduler.cs` | 洗回卡组表演排期（EventLog 扫描入 sink） | 03 |
| `Presentation/ShuffleIntoDeckPresentSink.cs` | 待播洗回 FIFO 队列（禁 Forget 旁路泵） | 03 |
| `Presentation/ShuffleBurstGrouper.cs` | 洗回爆发组/散单拆分 | 03 |
| `Presentation/ShuffleBurstOriginResolver.cs` | 爆发炸开原点解析（禁离屏尸体位） | 03 |
| `Presentation/FlipPlaybackCoordinator.cs` | 全局串行翻牌队列（ADR-0016 门控） | 03 |
| `Presentation/CardFaceGenerationBootstrap.cs` | 非锁步生成指令重放引导 | 03 |
| `Presentation/OccupancyForceSyncGuard.cs` | 占格强制对账退场门（永远拒绝并断言） | 03 |
| `Presentation/IntentBatchProjection.cs` | 解算批 → 盘面摘要共享投影 | 03 |
| `Presentation/InputIntent.cs` | 意图载体 struct + 清除原因 + 工厂/预览接口 | 04 |
| `Presentation/InputIntentKinds.cs` | 意图 kind 常量与分类（棋盘动作/模式模态） | 04 |
| `Presentation/IntentDisposition.cs` | 意图处置四型枚举（Buffer 已退役） | 04 |
| `Presentation/IPresentationIntentRuntime.cs` | 意图运行时窄接口（提交/忙态/租约） | 04 |
| `Presentation/IAccelerationSink.cs` | 冲动轻点脉冲缝（预留加速，当前 no-op） | 04 |
| `Presentation/IBufferedIntentLegality.cs` | Flush 前复校接口（缓冲退役后仅 legacy） | 04 |
| `Presentation/BoardIntentLegality.cs` | 棋盘意图 Core 内容合法性七 kind 裁决 + 相位矩阵 | 04 |
| `Presentation/BoardBufferedIntentLegality.cs` | 按 kind 委托复校（未知 kind 放行） | 04 |
| `Presentation/RoutingIntentScriptFactory.cs` | 工厂组合器（遍历全部子工厂） | 04 |
| `Presentation/AttackIntentScriptFactory.cs` | 攻击垂直切片剧本（命中/分支/稳定化/敌方行动） | 04 |
| `Presentation/ExploreIntentScriptFactory.cs` | 空格探索剧本 | 04 |
| `Presentation/RevealFaceIntentScriptFactory.cs` | 主动翻开剧本 | 04 |
| `Presentation/UseItemIntentScriptFactory.cs` | 战斗相位用牌剧本 | 04 |
| `Presentation/NonCombatUseItemIntentScriptFactory.cs` | 非战斗相位用牌剧本（ADR-0032） | 04 |
| `Presentation/PickupIntentScriptFactory.cs` | 拾取剧本 + Hand 动画 Hook | 04 |
| `Presentation/RecycleItemIntentScriptFactory.cs` | 回收剧本 + 离手动画 Hook | 04 |
| `Presentation/BoardWalkIntentScriptFactory.cs` | 走格剧本（仅 SetDestination 一步） | 04 |
| `Presentation/AvatarWalkRunner.cs` | Avatar 连跳执行器 + IAvatarWalkSystem | 04 |
| `Presentation/AttackIntentRejectedEvent.cs` | 攻击被拒广播 struct | 04 |
| `Presentation/ExploreIntentRejectedEvent.cs` | 探索被拒广播 struct | 04 |
| ~~`Presentation/PickupItemRejectedEvent.cs`~~（#234 已删：拾取改导演锁步） | — | 04 |
| `Presentation/UseItemIntentRejectedEvent.cs` | 用牌被拒广播 struct | 04 |
| `Presentation/BattleBeatScheduler.cs` | 结算指令唯一分发点（pending/报点/隔离区） | 05 |
| `Presentation/BattleBeatHook.cs` | 报点静态桥（编排 → Scheduler） | 05 |
| `Presentation/BattleBeatFlush.cs` | 冲刷门面（FlushBeats / 只冲翻牌 / 切片 Present） | 05 |
| `Presentation/IBattleBeatHandler.cs` | 锚点处理器契约（TryApply 认领语义） | 05 |
| `Presentation/PlayerInfoHudBeatHandler.cs` | 链① Avatar 血甲 HUD 旁路刷新 | 05 |
| `Presentation/DamageFloaterBeatHandler.cs` | 链② 飘字/受击音/VFX 装饰 | 05 |
| `Presentation/CardFaceStatHandler.cs` | 链③ 卡面数值唯一 Commit 出口 | 05 |
| `Presentation/CardFaceFlipBeatHandler.cs` | 链④ UpdateFaceUp 消费与串行翻牌 | 05 |
| `Presentation/EffectTriggerPulseBeatHandler.cs` | 链⑤ TriggerEffect 脉冲装饰 | 05 |
| `Presentation/GoldGainBeatHandler.cs` | 链⑥ UpdateGold 飞币/HUD 广播 | 05 |
| `Presentation/TriggerPulseHub.cs` | FX/音效/VFX 三通道脉冲唯一出口 | 05 |
| `Presentation/ScreenImpact.cs` | 抖屏门面 + Runner（整像素量化、不旋转相机，ADR-0051） | 05 |
| `Presentation/BoardImpactSignatures.cs` | 「触发即爆点」效果的抖屏/冲击波档位登记表（ADR-0051） | 05 |
| `Presentation/AudioTriggerPulseSink.cs` | 音频脉冲落地 IAudioSystem | 05 |
| `Presentation/CardEffectTriggerPulseSink.cs` | 旧 FX 通道（fx.card.{uid} → 卡面脉冲） | 05 |
| `Presentation/DebouncingTriggerPulseSink.cs` | 同 cue 去抖装饰 sink | 05 |
| `Presentation/GatedTriggerPulseSink.cs` | 谓词门控装饰 sink | 05 |
| `Presentation/NullTriggerPulseSink.cs` | 空 sink（降级实现） | 05 |
| `Presentation/VfxTriggerPulseSink.cs` | 类型化 VFX 落地 IVfxSystem | 05 |
| `Presentation/TriggerStep.cs` | 时间线一次性脉冲 Step + ITriggerPulseSink 接口 | 05 |
| `Presentation/AudioCue.cs` | 全项目音频 cue ID 注册表与发射点标注 | 05 |
| `Presentation/VfxCue.cs` | VFX cue 元数据声明基础设施 | 05 |
| `Presentation/BattleVfxCues.cs` | 战斗/生命周期/结果/技能/胜负 VFX cue 声明 | 05 |
| `Presentation/ProjectileVfxCues.cs` | 弹道 VFX（源→靶，返回命中延迟计划） | 05 |
| `Presentation/GoldGainVfxCues.cs` | 金币飞入唯一 VFX cue（economy.gold_flight） | 05 |
| `Presentation/GoldGainPresentationScheduler.cs` | GoldModified 扫描广播器 | 05 |
| `Presentation/GoldGainPresentationRequested.cs` | 金币增减单向表现请求 struct | 05 |
| `Presentation/DamageNumberRequested.cs` | 世界坐标飘字请求 struct | 05 |
| `Presentation/DescriptionOutputEvents.cs` | 描述显示/清除请求三 struct | 05 |
| `Presentation/RelicHudSyncRequestedEvent.cs` | 遗物栏同步请求 struct | 05 |
| `Presentation/TeardownPresentationDirectorRequested.cs` | 导演 Teardown 请求 struct | 05 |
| `Presentation/GameFlowShellState.cs` | 流程壳相位七态枚举 | 01 |
| `Presentation/GameFlowShellStateChangedEvent.cs` | 壳相位变更广播 struct | 01 |

### Diagnostics/（23）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `Diagnostics/BattleTraceRecorder.cs` | Battle 轨记录器（结算门 op + 四轨导出串联） | 10 |
| `Diagnostics/BattleTraceModels.cs` | Battle 轨数据模型 | 10 |
| `Diagnostics/BattleTraceJson.cs` | Battle 轨手写 JSON 序列化 | 10 |
| `Diagnostics/FlowTraceRecorder.cs` | Flow（CoreLog）轨记录器 | 10 |
| `Diagnostics/FlowTraceModels.cs` | Flow 轨模型（Category/事件名常量） | 10 |
| `Diagnostics/FlowTraceJson.cs` | Flow 轨手写 JSON 序列化 | 10 |
| `Diagnostics/PerfTraceRecorder.cs` | Perf 轨记录器（Beat/BoardSnap/实时异常检测） | 10 |
| `Diagnostics/PerfTraceModels.cs` | Perf 轨模型（Kind/站点/异常码） | 10 |
| `Diagnostics/PerfTraceJson.cs` | Perf 轨手写 JSON 序列化 | 10 |
| `Diagnostics/RegistryTraceRecorder.cs` | Registry 轨记录器（镜像/IdleWatch/FieldVisualGap） | 10 |
| `Diagnostics/RegistryTraceModels.cs` | Registry 轨模型 | 10 |
| `Diagnostics/RegistryTraceJson.cs` | Registry 轨手写 JSON 序列化 | 10 |
| `Diagnostics/DiagTraceShared.cs` | 四轨共享会话身份与落盘基础设施 | 10 |
| `Diagnostics/DiagTraceExportPreferences.cs` | 自动落盘/内存记录偏好（EditorPrefs） | 10 |
| `Diagnostics/DiagTraceManualSnapshot.cs` | 手动 Bug 快照（四轨写盘 + AI 必读说明） | 10 |
| `Diagnostics/DiagBeatClock.cs` | 共用 beatId 单调时钟 | 10 |
| `Diagnostics/DiagBoardSnapCapture.cs` | 共享 BoardSnap 采集 | 10 |
| `Diagnostics/DiagFieldVisualCapture.cs` | 场地可见性审计（Core 占格 vs 可见卡） | 10 |
| `Diagnostics/DirectorTrace.cs` | 导演剧本层打点薄封装（→ PerfLog） | 10 |
| `Diagnostics/ChoreoTraceContext.cs` | 场地编排关联键（choreoSeqId）+ busy payload | 10 |
| `Diagnostics/FieldTraceHelper.cs` | FlowTrace V2 占格/表现打点门面（Sink 注册） | 10 |
| `Diagnostics/BoardIntentGateDiagnostics.cs` | 门禁拒绝快照采集 | 10 |
| `Diagnostics/CombatHitTraceContext.cs` | 交战命中 reason 跨层单向传递 | 10 |

### BattleLog/（5）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `BattleLog/BattleLogRecorder.cs` | 采集端：EventLog 游标增量扫描 → 同因去重 + 按 SourceDefId 语义聚合 | 11 |
| `BattleLog/BattleLogStore.cs` | 成品行唯一存放处（按房间分段 + Changed 通知面板） | 11 |
| `BattleLog/BattleLogEntry.cs` | 行与房间段模型（序号 / 类别 / 缩进深度 / 富文本） | 11 |
| `BattleLog/BattleLogNaming.cs` | 唯一取名口：表现层 displayName → Catalog 定义名 → defId | 11 |
| `BattleLog/BattleLogPalette.cs` | 富文本配色真源（色相跟飘字，明度按浅底压暗） | 11 |

### RoomIcons/（5）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `RoomIcons/RoomIconBoardPresenter.cs` | 选房主链路（图标/登记/驻留提交/进房硬切） | 08 |
| `RoomIcons/RoomIconBoardSlotResolver.cs` | 图标落格解析（JSON boardSlot + 撞格回退） | 08 |
| `RoomIcons/RoomIconDwellSession.cs` | 驻留会话（武装/取消/一次性提交） | 08 |
| `RoomIcons/RoomIconOccupancy.cs` | 表现侧软占登记（WalkDestination/SoftBlockOnly） | 08 |
| `RoomIcons/RoomIconOccupancySlotHits.cs` | 软占变更后刷新格位命中框 | 08 |

### ShopBoard/（3）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `ShopBoard/ShopBoardPresenter.cs` | 商店房场地板（货架/刷新/离开/金币不足 Notice） | 08 |
| `ShopBoard/ShopBoardHitProxy.cs` | 商店点击代理（BuyShelf/Refresh） | 08 |
| `ShopBoard/ShopBoardSlotResolver.cs` | 商店占格常量 | 08 |

### TavernBoard/（3）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `TavernBoard/TavernBoardPresenter.cs` | 卡店场地板（三服务 + 二级候选面） | 08 |
| `TavernBoard/TavernBoardHitProxy.cs` | 卡店点击代理（服务/刷新/候选三 kind） | 08 |
| `TavernBoard/TavernBoardSlotResolver.cs` | 卡店占格（粘性家格 + 候选池） | 08 |

### RewardBoard/（3）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `RewardBoard/RewardBoardPresenter.cs` | 特殊奖励房场地板（点击拿走/踩离开放弃） | 08 |
| `RewardBoard/RewardBoardHitProxy.cs` | 奖励房点击代理 | 08 |
| `RewardBoard/RewardBoardSlotResolver.cs` | 奖励房占格常量 | 08 |

### AttributeBoard/（4，玩法已废止）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `AttributeBoard/AttributeBoardPresenter.cs` | 属性房三选二场地板（遗留废止，ADR-0031） | 08 |
| `AttributeBoard/AttributeBoardHitProxy.cs` | 属性房候选点击代理（遗留） | 08 |
| `AttributeBoard/AttributeBoardSlotResolver.cs` | 属性房占格常量（遗留） | 08 |
| `AttributeBoard/AttributePickIndexResolver.cs` | 视觉序 → Pending 索引映射（遗留） | 08 |

### InRoomBoard/（5）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `InRoomBoard/InRoomOfferSlotPlanner.cs` | 货架动态落格规划（保 PreferEmpty 离开通路） | 08 |
| `InRoomBoard/InRoomShelfAnimation.cs` | 货架补位/消耗表演（跳格/落下/碎裂） | 08 |
| `InRoomBoard/InRoomItemAcquirePresentation.cs` | 购领接手牌（ADR-0025） | 08 |
| `InRoomBoard/InRoomGoldPresentation.cs` | 房内扣金/加金直推 HUD | 08 |
| `InRoomBoard/RoomOptionFaceVisuals.cs` | 就地选项卡面视觉应用 | 08 |

### BoardBriefTip/（5）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `BoardBriefTip/BoardBriefTipPresenter.cs` | 场景「简要解释文字框」驱动（悬停 + Notice） | 08 |
| `BoardBriefTip/BoardBriefTipSession.cs` | 两路文案会话状态（代数防脏写） | 08 |
| `BoardBriefTip/BoardBriefTipCopy.cs` | 文案库（离开/房间/货架/楼层） | 08 |
| `BoardBriefTip/BoardBriefTipHitProxy.cs` | 非战斗场地对象认领 + BoardWalk 提交 | 08 |
| `BoardBriefTip/FloorHintPresenter.cs` | 场景「楼层提示」驱动（轮询 RunModel） | 08 |

### BattleInfoPreview/（6）

| 文件 | 一句话职责 | 篇 |
|------|-----------|----|
| `BattleInfoPreview/BattleInfoPreviewPresenter.cs` | 战前信息预览主控（ShowAndWaitAsync 硬阻塞） | 09 |
| `BattleInfoPreview/BattleInfoPreviewSlotView.cs` | 单预览槽视图（__Art 图标层 + 高亮） | 09 |
| `BattleInfoPreview/BattleInfoPreviewIconPlayer.cs` | 预览槽图标播放（静态/Idle 多帧） | 09 |
| `BattleInfoPreview/BattleInfoPreviewHighlight.cs` | 悬停九宫四角框高亮 | 09 |
| `BattleInfoPreview/BattleInfoSlotArtFit.cs` | 槽图标摆放数学（复用卡面 mainVisual 参数） | 09 |
| `BattleInfoPreview/BattleInfoPreviewCopySO.cs` | 房间信息文案模板 SO（四级通配回退） | 09 |

> 分节小计：根 43 + GameFlow 6 + Tutorial 5 + Platform 3 + Transitions 3 + BattleSession 14 + Presentation 80 + Diagnostics 23 + BattleLog 5 + RoomIcons 5 + ShopBoard 3 + TavernBoard 3 + RewardBoard 3 + AttributeBoard 4 + InRoomBoard 5 + BoardBriefTip 5 + BattleInfoPreview 6 = **216**。
