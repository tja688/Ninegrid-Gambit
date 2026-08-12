# ADR-0042 教学关卡模块：一次性受控教学战斗

- 状态：已接受（2026-08-12）
- 关联：ADR-0022（关卡装填）、ADR-0026（离开机关唯一清关）、ADR-0034（盘面稳定化）、ADR-0041（跑图存档）

## 背景

游戏缺一个独立的新手教学。要求：一次性（存档判定首次游玩自动进入，非首次自动跳过）、可从主菜单独立重进、完全不影响正式关卡流程；讲解载体是**特制教学机关卡的卡面描述互相拼接**，开局仍走常规卡组入场与发牌表演。

## 决策

### 1. 教学是独立开局模式，不进节点循环

`GameFlowRunOptions.CreateTutorial(continueToFormalRun)` 增加教学载荷（与 QuickTest / Restore 同款「载荷即模式」范式）。`GameFlowOrchestrator.Start` 见教学载荷走独立的 `RunTutorialAsync`：BootstrapRun → 受控开局发牌 → 教学导演推进 → 等待结算就绪；**不进** `RunNodeCycleAsync`，不捕获存档检查点，不展示战前信息预览，不叠 QuickTest 作弊。

### 2. 首次判定走既有存档后端；完成标记独立槽位

`TutorialProgressStore` 经 `RunSaveStoreHook`（ES3 桥）读写独立槽位 `tutorial_profile`，与跑图检查点（auto / manual_N）互不干扰。主菜单「开始游戏」（`GameFlowController.BeginFormalRun`）在无完成标记时改派教学开局（`continueToFormalRun=true`，通关后自动转正式开局）；有标记直接正式开局。主菜单独立「教学」按钮（`TutorialRun`）任何时候可重进，完成后回主菜单。**教学战败不标记完成**，且不触发 `RunSaveService.HandleRunEnded`（不得误删玩家早前正式局自动存档）。

### 3. 受控发牌：保序抽牌堆 + 波次脚本挂主线

- Core 缝：`NodeDeckOptions.PreserveDealOrder`（仅教学置 true）——`OpeningDealAction` 跳过洗牌与离开机关落点重排，抽牌堆保持装填顺序；配合 `FillEmptySlotsAction` 固定补格序（1,2,3,6,9,8,7,4）实现确定性铺场。正式流程该开关恒为 false，行为不变。
- 阅读序换算：卡面描述按玩家阅读习惯（格 1,2,3 / 4,6 / 7,8,9 逐行）编排，`TutorialDeckPlan` 把阅读序重排为抽牌堆序。
- 波次切换：`TutorialBattleDirector` 逐帧巡检 Core 状态，把「清场批（`RemoveCardAction`，reason=clearResidualBoard，不派发 OnRemove 连锁）→ 补发批（`ShuffleIntoDrawPileAction` 逆序顶插）→ 盘面稳定化」作为真时间线脚本经 `MutateMainline` 挂主线——复用既有 Resolve/Present/ack 锁步与发牌表演。非锁步小补给（机关无限补位、飞刀练习靶）走作弊面板同款「直跑管线 + 事件切片冲刷」。
- 批次投影通道：导演自持私有 `QueuedBoardPresentChannel`，换波各批（清场 / 补发 / 稳定化）的 EventLog 切片投影**必须投递该私有通道**（PresentStep 消费的就是它）；投进生产 Explore 通道无人消费，会导致换波在 Core 落地但零表演（旧卡影子留场、新卡「虚空发牌」）。
- 换波期输入管控：换波条件按 Core 真相先行判定（击杀批一解算即为真），满足即复用 **Opening 输入门**（`PresentationInputGates.SetOpening(true)`）锁玩家输入，直到新波发牌表演完成、`ScanWave` 登记完毕才解锁；相位切换（清关 / 战败）与导演 `Stop()` 兜底释放，不粘门。这堵住「击杀表演收尾 → 换波脚本入队」之间主线短暂空闲的输入窗口。

### 4. 四波剧本（行为约定）

1. **基础互动**：8 张教学机关卡铺满场（各 6 血、无攻、机关不反击）；击破 1 张 → 自然补位（补位提示卡）+ 旋转；累计击破 2 张 → 换波（上波残余机关集体移除）。
2. **怪物机制**：7 机关 + 1 教学怪（复用 `monster.melee_3`：普通近战、行动计数 5）；讲行动计数 / 开火 / 右键详情 / 击破机关调位；击破怪 → 换波；机关无限补位。
3. **道具使用**：5 机关 + 飞刀 + 恢复药水 + 练习靶怪；讲拾取 / 拖用 / 选目标；飞刀未用而全场无活怪时自动补练习靶（防卡死）；两件道具都用掉 → 换波。
4. **清关与变卖**：7 机关 + 离开机关（复用 `trap.leave`）；讲击破门离开 / 杀光怪才自动变卖 / 提前跑不结算 / 层主房规则；击破离开机关走 **Core 正常清关链**（ADR-0026），结算就绪即教学完成。

### 5. 教学内容全部进 `deck.tutorial`，不污染正式投放

29 张教学机关卡（`trap.tutorial.*`）统一 `deckId=deck.tutorial`、稀有度 `None`（**不进** `RegularTrapPool` White 池）、无效果装配（描述契约天然通过）；卡面描述每张 ≤16 字。怪物 / 道具 / 离开机关复用既有内容，不新建。教学卡组不登记 `monster_decks.json`（不是遭遇牌组）。

## 禁止

- 正式流程（`BuildNodeDeckOptions` / 节点循环）引用 `deck.tutorial` 内容或置 `PreserveDealOrder`
- 教学机关卡改成 White 稀有度（会流入常规机关池）
- 教学战败/中途退出写完成标记；教学局删除玩家自动存档
- 教学波次逻辑绕过主线（直接改盘面视图或在锁步批打开时直跑管线）
- 教学批次投影投递生产会话通道（`OnExploreBatchProjected` 等）——必须进导演私有通道，否则换波零表演
