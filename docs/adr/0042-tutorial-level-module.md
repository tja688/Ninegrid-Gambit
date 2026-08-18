# ADR-0042 教学关卡模块：一次性受控教学战斗

- 状态：已接受（2026-08-12）；**生产入口已断开（2026-08-18）**
- 关联：ADR-0022（关卡装填）、ADR-0026（离开机关唯一清关）、ADR-0034（盘面稳定化）、ADR-0041（跑图存档）

## 补记（2026-08-18，#226 / #228 / #229 / #230 / #231）

教程与规则书彻底拆为两颗入口：
1. **主菜单「教程」**：`TutorialRun` 按钮当且仅当步骤 1–9 完成后可见；点击发起单场教程（打完回主菜单，不进 1-2 跑图，不写跑图检查点，不误删玩家正式自动存档），每次均完整包含步骤 1–9 及独立门教学（第 10 步，不走生涯已见抑制）。彻底断开旧五阶段导演 `TutorialBattleDirector`，由 `TutorialCoach` 单一策略缝消费节拍驱动同构教学战斗。
2. **战斗壳「规则书」**：`TutorialKnowledgeBookController` 挂接场景「规则书」（原「教程按钮」），仅在战斗壳显示并从步骤 9 起放行翻阅（步骤 9 前保持隐藏/禁用），主菜单隐藏。
3. **教学档三路标记**：`TutorialProgressStore` 独立槽位 `tutorial_profile` 承载 `steps1To9Completed`（步骤 1–9 完成）、`doorTutorialSeen`（门教学已见）、`scarletUnlocked`（血色已解锁），与跑图检查点隔离。自然第一关战败亦标记 1–9 完成；整趟跑图胜利标记血色解锁。
4. **选人与难度**：选人锁定战士；旅途/冒险可选；血色在整趟跑图胜利前锁定、胜利后解开。旧独立五阶段关卡生产入口保持断开。
5. **第一关保序开局载荷（#228，2026-08-18 修订）**：`TutorialDeckPlan.BuildOpeningOptions` 提供一套保序、确定性的正式内容开局载荷（16 张正式卡）：怪物**只允许**小小莱姆 `monster.melee_3`；道具只用第一关 White 白装（恢复药水 / 飞刀 / 坚固盾）；**不塞常规机关**；离开机关 `trap.leave` 仍在抽牌堆后半段。教学目标格为玩家正右方 **Slot 6**（开局 Slot 6 小小莱姆；击杀补牌并顺时针旋转后，Slot 3 恢复药水落入 Slot 6）。仅作用于教程第一关。自然首局 / 菜单教程在 Bootstrap 后立刻 `RunModel.TryBindFloorMonsterDeck("deck.dragon")` 钉死本层莱姆卡组，因此 1-2 起 `BuildNodeDeckOptions` 走莱姆正式编组并恢复常规三张机关注入。已完成 1–9 的正式开局不钉，第 1 层仍按种子在莱姆/植物间抽。旅途与冒险难度第一关呈现相同牌面。
6. **第一关步骤 1–9 教练（#230）**：
   - 步骤 1（Avatar 就位）：顶部第 1 句（欢迎并框 Avatar Slot 5，卡主流程，点推进）；
   - 步骤 2（顺劈斧）：顶部第 2 句（讲顺劈斧范围攻击，框 Avatar Slot 5，卡主流程，点推进后放行发牌）；
   - 步骤 3（铺场 8 张）：发牌着陆后顶部第 3 句（讲铺场 8 张，卡主流程，点推进）；
   - 步骤 4（攻击邻格真怪）：顶部第 4 句（框 Slot 6 `monster.melee_3`，白名单仅允许攻击 Slot 6，错点其它格在收口层静默拒绝，不卡主流程以便攻击）；
   - 步骤 5（补牌与旋转）：击杀后补牌与顺时针旋转稳定，顶部第 5 句（讲击杀补牌与旋转，卡主流程，点推进）；
   - 步骤 6（拾取药水）：顶部第 6 句（框旋转至 Slot 6 的 `help.healing_potion`，白名单仅允许拾取 Slot 6，错点其它格拒绝，不卡主流程以便拾取）；
   - 步骤 7（行动计数）：拾取完成后顶部第 7 句（讲怪物行动计数，卡主流程，点推进）；
   - 步骤 8（右键详述）：顶部第 8 句（讲右键详述，卡主流程，点推进，右键详述自本步起放行）；
   - 步骤 9（规则书）：顶部第 9 句（讲规则书，卡主流程，点推进，规则书自本步起放行）；
   - 步骤 9 播完写入 `TutorialProgressStore.MarkSteps1To9Completed()` 并释放主流程；首关战败亦写 1–9 标记；1-2 节点及后续自然对局不再触发 1–9。
7. **独立门教学（#229）**：`TutorialCoach` 独立于步骤 1–9 线性状态机。自然对局里，玩家生涯首次离开机关（`trap.leave`）就位（发牌或补牌落地表演就位后）时卡住主流程、`TutorialPromptBoxPresenter` 框住门、`InfoNoticePresenter` 打出第 10 句（「可以打破门来离开，清理掉所有怪物再离开会自动变卖场上的道具」；点了才走保持）。玩家点击推进后立即写入 `TutorialProgressStore.MarkDoorTutorialSeen()`，不要求打破门。若见到门前战败，标记保持 `false`，后续自然对局首次见门仍会提示。主菜单「教程」不走生涯抑制，每次门就位均完整触发第 10 步。门落点不强制正右方。
8. **框选几何与半黑屏挖洞（2026-08-18）**：教学提示框包围盒优先卡框节点（`卡框` / `CardFaceSlotCodes.CardFrame`），不合并描述格与 HUD 图标；框选目标以 Core `BoardModel` 占格 UID 为准（旋转后表现占格短暂不一致时不得跟飞走的旧卡）。框选出现时 `TutorialSpotlightPresenter` 复制场景 `半黑屏BG` 颜色/sorting，用四条无 collider 遮罩条围出亮区并随框呼吸；**禁止** `BattleUiDimmerOverlay.TryAcquire` 与 SpriteMask 挖洞（半黑屏自定义材质不参与 mask）。无框步骤不铺黑。全部教学句均为点了才走，无定时自动过。

## 背景

游戏缺一个独立的新手教学。要求：一次性（存档判定首次游玩自动进入，非首次自动跳过）、可从主菜单独立重进、完全不影响正式关卡流程；讲解载体是**特制教学机关卡的卡面描述互相拼接**，开局仍走常规卡组入场与发牌表演。

## 决策

### 1. 教学是独立开局模式，不进节点循环

`GameFlowRunOptions.CreateTutorial(continueToFormalRun)` 增加教学载荷（与 QuickTest / Restore 同款「载荷即模式」范式）。`GameFlowOrchestrator.Start` 见教学载荷走独立的 `RunTutorialAsync`：BootstrapRun → 受控开局发牌 → 教学导演推进 → 等待结算就绪；**不进** `RunNodeCycleAsync`，不捕获存档检查点，不展示战前信息预览，不叠 QuickTest 作弊。

### 2. 首次判定走既有存档后端；完成标记独立槽位

`TutorialProgressStore` 经 `RunSaveStoreHook`（ES3 桥）读写独立槽位 `tutorial_profile`，与跑图检查点（auto / manual_N）互不干扰。主菜单「开始游戏」（`GameFlowController.BeginFormalRun`）在无完成标记时改派教学开局（`continueToFormalRun=true`，通关后自动转正式开局）；有标记直接正式开局。主菜单独立「教学」按钮（`TutorialRun`）任何时候可重进，完成后回主菜单。**教学战败不标记完成**，且不触发 `RunSaveService.HandleRunEnded`（不得误删玩家早前正式局自动存档）。**补记（2026-08-13）**：教学通关转正式开局走跨层洞形转场（`RunSceneTransitionService.BeginCoverAsync(crossFloor:true)`）——回主菜单收场与 `BeginRun(CreateFormal)` 的硬切全部在遮罩下完成，不再闪一帧主菜单。

### 3. 受控发牌：保序抽牌堆 + 波次脚本挂主线

- Core 缝：`NodeDeckOptions.PreserveDealOrder`（仅教学置 true）——`OpeningDealAction` 跳过洗牌与离开机关落点重排，抽牌堆保持装填顺序；配合 `FillEmptySlotsAction` 固定补格序（1,2,3,6,9,8,7,4）实现确定性铺场。正式流程该开关恒为 false，行为不变。
- 阅读序换算：卡面描述按玩家阅读习惯（格 1,2,3 / 4,6 / 7,8,9 逐行）编排，`TutorialDeckPlan` 把阅读序重排为抽牌堆序。
- 波次切换：`TutorialBattleDirector` 逐帧巡检 Core 状态，把「清场批（`RemoveCardAction`，reason=clearResidualBoard，不派发 OnRemove 连锁）→ 指定格直摆（`SpawnCardAction`）→ 盘面 Present」作为真时间线脚本经 `MutateMainline` 挂主线。**空池开局仍须把卡组切到 `CardDeckMode.InGame`**（`BeginEntry` 无待入场牌时直接切；`EnsureInGameIfStandby`），否则 `DealCardByUid` 拒绝发牌，Core 占格 / 表现空格会占格对账死循环。
- 批次投影通道：导演自持私有 `QueuedBoardPresentChannel`，换阶段各批的 EventLog 切片投影**必须投递该私有通道**（PresentStep 消费的就是它）；投进生产 Explore 通道无人消费，会导致换阶段在 Core 落地但零表演。
- 换波期输入管控：换波条件按 Core 真相先行判定（击杀批一解算即为真），满足即复用 **Opening 输入门**（`PresentationInputGates.SetOpening(true)`）锁玩家输入，直到新波发牌表演完成、`ScanWave` 登记完毕才解锁；相位切换（清关 / 战败）与导演 `Stop()` 兜底释放，不粘门。这堵住「击杀表演收尾 → 换波脚本入队」之间主线短暂空闲的输入窗口。

### 4. 五阶段剧本（行为约定，2026-08-17 修订）

同一场战斗内切阶段；关键卡未登记前（UID=0）不得当成已击杀/已离场，否则会整阶段跳过。`deck.tutorial` 仅保留 11 张「-」教学机关 + 教学怪物；旧「教学·*」机关归档至 `deck.tutorial_archive`。

1. **进攻 + 查看详情**：两张提示卡 + 范围内教学假人（6 血）；阶段1点击白名单与提示卡攻击弹窗；击败假人 → 阶段2。
2. **移动 + 补牌**：提示卡 + 范围外假人 + 牌堆库存假人；常显攻击范围；首次击杀把牌堆那张补到死亡格并导演强转一次（不另造第三张）；再击杀 → 阶段3。
3. **行动/移动计数**：两张提示卡 + 行动假人 / 移动假人（机关卡模板，节奏归零自毁，可硬砸）；双离场 → 阶段4。节奏自毁挂 `tpl.tutorial.dummy.rhythm_death`，须声明 `HasOwnerEntity` + `CardZoneTriggerable`（ADR-0010）。倒计时图标按 `rhythmSource` 切换：机关模板默认移动计数，行动假人显式用行动计数图标。
4. **手牌 + 道具**：提示卡 + 飞刀 + 恢复药水（邻格可拾）；双拾取 → 阶段5（道具可带入阶段5）。
5. **综合实战**：提示卡 + 教学钢铁莱姆（`monster.tutorial.steel_slime`，40 血）+ 牌堆 5 飞刀 + 5 药水；击杀莱姆 → 教学通关。

**阶段死亡重开**：仅在 Avatar 真正战败（Defeat 相位 / 0 血）时抑制整场收口并重发当前阶段；普通盘面 Drain（发牌/直摆）不得触发重开。阶段4/5重开时回滚本关道具手牌。

**通关条件**：阶段5击杀 `monster.tutorial.steel_slime`（不再使用 `trap.leave`）。

### 5. 教学内容：`deck.tutorial` + `deck.tutorial_archive`

11 张 live 教学机关（`trap.tutorial.*`，displayName 含「-」）+ 教学怪物（`monster.tutorial.*`）在 `deck.tutorial`；旧「教学·*」机关统一 `deckId=deck.tutorial_archive` 归档。怪物 / 道具复用或新建教学专用 id；教学卡组不登记 `monster_decks.json`。

## 禁止

- 正式流程（`BuildNodeDeckOptions` / 节点循环）引用 `deck.tutorial` 内容或置 `PreserveDealOrder`
- 教学机关卡改成 White 稀有度（会流入常规机关池）
- 教学战败/中途退出写完成标记；教学局删除玩家自动存档
- 教学波次逻辑绕过主线（直接改盘面视图或在锁步批打开时直跑管线）
- 教学批次投影投递生产会话通道（`OnExploreBatchProjected` 等）——必须进导演私有通道，否则换波零表演
