下面是探查报告。我已经把设计文档、Core 契约、表现层全部脊柱/适配器/FSM 代码、以及 MainScene 的实际挂载逐一交叉核对过了（并刻意以代码为准，因为文档之间本身存在时间错位）。

---

# 表现层接线探查报告 · 最小可玩全流程

## 一句话结论

**内核是"成品级"的，表现层的零件也基本造齐了，但"总装"卡在一个被所有文档都漏掉的地基上——没有统一的"演员工厂/生命周期"系统。** 现在按 F5 能跑通绝大半个流程（发牌→攻击→击杀旋转补位→通关→三选一/选房间→进房），但画面会"缺人、缺脸、动作打折"：化身没有演员、所有卡长一个样、开局发牌动画被抄近路。要开"胶水计划"，**第一坎也是唯一的硬坎，就是先把演员工厂做出来**，其余都是顺着接线和打磨。

---

## 二、先校准"货不对版"：文档 vs 代码的真实错位

你担心的"设计与落地错位"是真实存在的，我先把它钉死，后面分析才站得住：

| 来源 | 说法 | 代码现实 |
|---|---|---|
| `表现层全面盘点(06-23)` | OverlayAdaptor "从未创建"、选择流程"完全断裂"、EffectAdaptor"未入场景"、悬停信息"从零开始" | **全部已闭合**。`TableNineOverlayAdaptor`、`SelectionOverlayFsm`、`ContentInfoPresenter` 三者代码齐全且 **GUID 都在 `Assets/Scenes/MainScene.unity` 中命中**（我逐个 GUID 验证过） |
| `蓝图对照表(06-24)` | 6/6 适配器接通、Overlay 已落地 | 与代码一致，这份是准的 |

**结论**：以 06-23 那份盘点为基准会严重低估你的进度。**真实进度比那份文档先进一代**。下面所有判断以代码为准。

---

## 三、内核侧（Core）：可以放心，它不是瓶颈

`九宫牌局权威顶层架构设计.md` 描述的契约在代码里是**完整兑现**的，且这是表现层可以依赖的稳固地基：

- **三窗口齐全**：`CoreCommandDispatcher`（唯一入口）→ `PresentationBatch`（指令流）→ `CoreViewSnapshot`（权威终态）+ `PresentationFinishedCommand` 握手回执。
- **39 种 `CoreEventType` 全映射**进 `PresentationEventMap`，`LocksInput` / `RequiresPlayback` 元信息齐备。
- **录像回放模型**已实现（单帧结算、整批回放、批末解锁），`P7PresentationContractTests` / `P8HeadlessSimulationTests` 在 `NineGrid.Core.Tests` 里给它兜底。

> 唯一对表现层有影响的内核侧"小账"：`CoreViewSnapshot.BoardSlot` 给的是 **base 攻/血/甲**，不是经条件光环/RuleModifier 后的有效值；快照里也没有 `DrawPile` 字段（发牌适配器目前靠读 `DeckModel.DrawPileUids` 兜）。这些不阻塞 MVP，但做"有效属性显示"时要记得补 Query。

**判断：内核不欠表现层任何东西。瓶颈 100% 在表现层的"总装层"。**

---

## 四、表现层散落零件清单（造好了什么）

把脑子里那堆散件归位，按四盒子方法论分类，它们其实大部分到位了：

**脊柱（基础设施）—— 齐**
- `PresentationBatchPlayer`：中心播放器，路由 6 域适配器 + 批末快照对齐 + 发回执 ✅
- `TableNineViewRegistry`：CardUid→演员、SlotId→锚点、环形旋转路径、手牌列表 ✅
- `InputLockGate` / `BoardItemInteractionCoordinator` / `ContentCatalogRuntimeBootstrap`（Boot 期已加载内核表+视觉表）✅

**泳道 A（流程壳）—— 齐**
- `InGameFlowShellFsm` + `GamePhaseFlowShellProjection`：Boot→RunSession→NodePlaying，并把内核 phase 弱同步投影成屏幕态、启停三个子 FSM ✅

**泳道 B（交互输入）—— 大体齐，两处半成品**
- `BoardInteractionFsm`：Hover 本地反馈 + Confirm 发 Attack/Pickup/ClickEmpty ✅
- `SelectionOverlayFsm` + `SelectionOverlayController`：奖励/房间/进房 N 选 1 + stat_boost 选项覆盖层，确认后回传 `SelectRewardCommand`/`SelectRoomCommand`/`EnterRoomCommand`/`SkipHelpChoiceCommand` ✅
- `ItemCardInteractionFsm`：⚠️ 只有 hover/drag/回手，**不发 `UseItemCommand`**（注释明说 MVP 场地主导）

**泳道 C（回放管线）—— 6 域适配器全接通**
- Board / Deck / Item / Status / Effect / Overlay 适配器全部存在且在 `PlayBatchCoroutine` 路由链中 ✅
- 20+ 个 Performance 黑盒（攻击/反击/击杀/旋转/发牌/补位/飘字/选择层入场悬停确认退场…）已落地 ✅

---

## 五、核心诊断：被所有文档漏掉的"地基坑"——没有演员工厂

这是整份报告最重要的一节，也是你"散、配合不起来"的真正根因。

### 现象

方法论文档第二节白纸黑字写了第②个盒子是 **"演员 Actor：从对象池取、绑 CardUid、记当前锚点"**。但代码里**根本没有这个盒子**。我把整个 `NineGrid.Presentation` 里所有 `RegisterActor` 调用都查了一遍，演员的"出生"只发生在两个地方：

```371:400:Assets/Scripts/NineGrid.Presentation/Adaptors/TableNineCardDeckAdaptor.cs
        private Transform EnsureDeckActor(int cardUid)
        {
            ...
            if (cardActorPrefab != null)
            {
                var instance = Instantiate(cardActorPrefab, parent);   // 一个通用预制体，不认 defId
                ...
            }
            else
            {
                actor = SelectionOptionVisual.CreatePreviewCard(...);  // 纯色占位卡
            }
            viewRegistry.RegisterActor(cardUid, actor, ViewActorZone.Deck);
            return actor;
        }
```

- **发牌适配器** 在 Deal/Spawn 时造演员（牌堆/场地）
- **道具适配器** 在 PickItem 时造手牌演员

### 三个由此派生的真实断点

1. **化身（玩家）没有演员。** `InitialGameFactory.Create` 在 **Boot 期**直接 `board.SetAvatar(avatar, Board(5))`——**不走流水线、不产生任何 `CardSpawned`/`CardDealt` 事件**：

```65:72:Assets/Scripts/NineGrid.Core/Setup/InitialGameFactory.cs
            var avatar = registry.Create(options.AvatarDefId, CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, options.AvatarMaxHp);
            ...
            board.SetAvatar(avatar, SlotId.Board(5));
```

   既然没有事件，就没有任何适配器会为化身调 `EnsureDeckActor`。于是 `viewRegistry.TryGetActor(AvatarUid)` **必然失败**。后果是连锁的：
   - `TableNineBoardAdaptor.TryResolveCombatPair` 拿不到化身 Transform → **化身作为攻击方时不会有冲刺动作**，只剩受击方抖一下 + 飘字（走 anchor 兜底）。
   - 化身受击/加血的卡面状态视图也无处更新（`ShouldAnimateCardStatus` 对 AvatarUid 直接返回 false，靠 HUD 面板兜）。

2. **所有卡长一个样。** `EnsureDeckActor` 只 `Instantiate` 同一个 `cardActorPrefab`，**从不根据 defId 去 `ContentVisualCatalog` 取卡面精灵**。`ContentInfoPresenter`（悬停信息）已经会用 `VisualCatalog` 了，但**演员本体没接**。所以场上 8 张怪物视觉无法区分，只有数字（攻/血/甲）不同。

   —— 注意：你**当前未提交的工作**（`ContentVisual*` 编辑器 + `ContentVisualSpriteKeyCodec` + xlsx IO）正是在建 defId→精灵 的视觉表通道。**这条线就是为了填这个坑**，方向完全正确，只是还没接到演员工厂上。

3. **没有对象池、没有统一销毁。** 击杀走 `SetActive(false)` + `UnregisterActor`，补位再 `Instantiate` 新的。没有复用、没有跨批次生命周期管理。当前规模能跑，但这是"散"的物理来源。

### 为什么这是"重量级系统"

因为方法论里"位置是数据不是父子关系""补位=重跑布局函数""快照兜底 snap 终态"这套优雅心智，**全部以"每个 CardUid 都有一个可解析的演员"为前提**。演员工厂缺位，等于地基缺了一块，上面所有适配器都在各自打补丁（deck 造一种、item 造一种、avatar 没人造），这就是你体感"散"的根源——**不是组件太多，是缺了那个把它们统一起来的"演员注册中心 + 工厂"**。

---

## 六、"最小可玩全流程 demo" 现在到底能跑到哪

按你描述的全流程逐阶段判定（F5 启动后）：

| 阶段 | 内核 | 表现 | 实际效果 |
|---|---|---|---|
| Boot + 内容加载 | ✅ | ✅ | 正常，catalog 已在 Boot 加载 |
| 开局发牌入场 | ✅ | ⚠️ | 卡会出现并落到 8 格，但**开局走的是"飞进牌堆+批末 snap 到格"**（`IsOpeningDeal` 分支绕过了漂亮的 deal-to-slot 动画）；卡面是通用占位 |
| 悬停看信息 | — | ✅ | `ContentInfoPresenter` 可显示 defId/属性 |
| 点击攻击怪物 | ✅ | ⚠️ | 怪物受击/飘字/击杀淡出/旋转/补位**都有**；但**化身的攻击冲刺缺失**（化身无演员） |
| 击杀→旋转→补位 | ✅ | ✅ | 声明式重布局补位是亮点，能联动塌缩 |
| 通关→三选一 | ✅ | ✅ | 覆盖层入场→悬停→确认→退场全通，回传 Command 正确 |
| 选房间→进房 | ✅ | ✅ | 同上，`SelectRoom`/`EnterRoom` 闭环 |
| 进入下一节点 | ✅ | ❌ | **NodeCompleted 不会自动 StartNode**，必须再按 F5（`GamePhaseFlowShellProjection` 把 NodeCompleted 投影成 Idle，无人触发下一节点） |
| 道具使用 | ✅ | ❌ | `ItemCardInteractionFsm` 不发 `UseItemCommand`；只有 stat_boost 能走 `TryUseItemWithOptionOverlay`，但没有玩法入口去调它 |

**净结论**：**单节点闭环 95% 能跑通且不死锁**（内核单帧结算、批末必发回执，所以"看不见但不卡"）；**多节点靠手动 F5 串联**；**视觉完整度约 60%**（缺化身演员、缺卡脸、开局动画打折）。

---

## 七、全面接线方案 + 胶水计划"绕不过去的坎"

按"必须先行 → 流程完整 → 打磨"三档排序。**P0 是开胶水计划前绕不过去的坎，其余可并行或延后。**

### P0 · 唯一硬坎：统一演员工厂 / 生命周期（地基）

新增一个 `TableNineActorFactory`（或并入 ViewRegistry），职责单一：**给任意 CardUid 提供一个绑定了正确视觉与属性的演员，并管理对象池与销毁**。要点：

1. **化身落地入口**：Boot/EnterRunSession 后，读 `InitialGameSnapshot.AvatarUid` + `AvatarSlot`，造一个演员、`RegisterActor`、放到 avatar 锚点。（这一步解掉化身无演员的连锁问题，成本最低、收益最大，建议**第一个做**。）
2. **defId→视觉绑定**：演员创建时用 `ContentCatalogRuntimeBootstrap.VisualCatalog`（你正在建的那条线）按 defId 取卡面精灵；同时 `TableNineCardStatusView.SnapFromSlot(snapshot)` 填好攻/血/甲。
3. **三个现有出生点收编**：`EnsureDeckActor`、Item 的 hand 演员、未来的 avatar，全部改成调工厂，消除"各造各的"。
4. **对象池 + 销毁**：击杀/移除归还池，补位从池取。

> 这一步落地后，方法论里"位置是数据""重布局补位""快照 snap 兜底"才真正全部成立，散落的适配器立刻"咬合"上。

### P1 · 流程完整（让全流程自己跑起来，不靠手）

5. **NodeCompleted → 自动 StartNode**：在 `InGameFlowShellFsm.OnPhaseChanged` 命中 `NodeCompleted` 时，用规则构造 `NodeDeckOptions` 自动发 `StartNodeCommand` 并 `NotifyNodeSessionStarted`（把 DevStartNodeTool 的逻辑收编为正式流程；F5 仅保留为调试）。
6. **真实数据节点**：把 `DevStartNodeTool` 硬编码的 `NodeDeckOptions.CreateDefaultBattle()`（6 张写死卡）换成由 catalog/房间规则构造的节点卡池，跑真实怪物/奖励。

### P2 · 交互补全与打磨（你说的"挨个改"主力区）

7. **道具使用闭环**：`ItemCardInteractionFsm` 拖拽到使用区 → 发 `UseItemCommand`；stat_boost 类先走 `SelectionOverlayFsm.TryUseItemWithOptionOverlay`。
8. **开局发牌动画走正道**：去掉/改造 `IsOpeningDeal` 抄近路分支，让开局也走 deal-to-slot 表演（顺带按方法论"推迟一帧启动 Sequence"根治首帧瞬移）。
9. **有效属性显示**：等内核补 effective-stat Query 后，卡面/HUD 显示光环后的真实数值。
10. **Effect/ModifierApply/ItemUse 的 0s 占位** 替换为真实动效。

### P3 · 根治你最大的焦虑："不知道配合对不对"

11. **表现层冒烟/集成测试**：现在测试全在 `NineGrid.Core.Tests`，表现层 0 测试。建议加一个 **PlayMode 集成冒烟**：用 `CoreCommandDispatcher` 喂一段固定 Command 序列（StartNode→Attack→…→SelectReward→…），断言"批次播完后 `viewRegistry` 解析出的所有演员位置 == 快照终态"、"无 NullRef"、"输入锁正确开合"。这把"散件配合"的正确性变成可回归的红绿灯，正面解决你"开发到现在不知道到了什么程度"的痛点。

---