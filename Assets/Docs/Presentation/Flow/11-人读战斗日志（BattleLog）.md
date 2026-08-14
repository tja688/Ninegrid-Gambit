# 人读战斗日志（BattleLog）—— 语义聚合 · 外部命名 · 场地层中间态面板

> 权威代码：`Flow/BattleLog/`（5 个文件）+ `Ui/BattleLogPanel.cs`。
> 关联 ADR：ADR-0001（EventLog 是 Core 唯一事实输出）、ADR-0046（名字本地化）、ADR-0023（覆层命中仲裁）。

## 职责综述

给**人**看的战斗日志。与隔壁 [`10-诊断与日志`](10-诊断与日志（Diagnostics）.md) 的五轨 Trace 是两套东西，互不依赖：

| | 五轨 Trace（`Flow/Diagnostics/`） | 人读战斗日志（`Flow/BattleLog/`） |
|---|---|---|
| 读者 | AI / 开发者深挖细节 | 玩家与开发者当场对账 |
| 粒度 | 原生、全量、带程序噪音 | 只留数值与结果，聚合到效果 |
| 出口 | Play 结束落盘 JSON | 局内面板即时可看 |
| 命名 | 内部 defId / uid | 表现层配置器的外部命名 |

日志只收**数值与结果**：伤害、治疗、护甲、金币、基础数值、击杀、效果触发、遗物获得、用牌。盘面动作（移格、旋转、翻面、发牌）一概不进——那些看画面就知道，写进来只会淹没真正要对账的数。

## 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `BattleLogRecorder`（MonoBehaviour） | `BattleLog/BattleLogRecorder.cs` | 采集端：游标增量扫 `EventLog` → 同因去重 + 语义聚合 → 成品行写 Store |
| `BattleLogStore`（静态类） | `BattleLog/BattleLogStore.cs` | 唯一存放处：按房间分段，`Changed` 事件通知面板重绘 |
| `BattleLogEntry` / `BattleLogSection` / `BattleLogEntryKind` | `BattleLog/BattleLogEntry.cs` | 行模型（序号 / 类别 / 缩进深度 / 富文本）与房间段模型 |
| `BattleLogNaming`（静态类） | `BattleLog/BattleLogNaming.cs` | 唯一取名口：表现层 `displayName` → Catalog 定义名 → defId |
| `BattleLogPalette`（静态类） | `BattleLog/BattleLogPalette.cs` | 富文本配色真源 |
| `BattleLogPanel`（MonoBehaviour） | `Ui/BattleLogPanel.cs` | 场景面板接线 + TMP 富文本渲染 + 按钮/关闭/层级让位 |

## 核心流程

### 1. 采集：游标增量扫描

`BattleLogRecorder` 由 `PresentationSceneRoot.WireHosts` 经 `EnsureInstalled()` 装上，订阅 `Evt_PresentationBatchOpened` 即时扫描，`Update` 每帧兜底追扫（未开表现批次的纯 Core 结算也不漏）。游标模型与 `RhythmFaceFlowTraceBinder` 同构：`entries.Count < mCursor` 判定为 EventLog 被清空（重开一轮），此时连带 `BattleLogStore.ResetAll()` 与命名缓存失效。

全流程外层 try-catch 吞异常——日志采集失败绝不阻塞游戏路径。

### 2. 同因去重：一次结算只留信息最全的那条

Core 一次 `DealDamageAction` 会连发 `ArmorChanged` + `HpChanged` + `DamageDealt`，`HealAction` 会发 `Healed` + `HpChanged`。原样铺开会让一次挨打变成三行。

`IsSuppressed` 在同一 `ActionId` 簇内前后查兄弟事件（同一动作的事件必然相邻）：

- `ArmorChanged` 且 `Delta < 0` 且同簇有 `DamageDealt` → 丢（破甲量已在 `DamageDealt.ArmorDamage`）
- `HpChanged` 且同簇有 `DamageDealt` 或 `Healed` → 丢

留下来的 `DamageDealt` 一行同时给出实际总伤、破甲/扣血拆分、被减伤吃掉的原始值、剩余血甲——排「伤害到底对不对」需要的列都在这一行里。

### 3. 语义聚合：日志只认效果容器

一个效果常由多个内核原子拼成（Trigger + Condition + Target + Action，还会派生 FollowUp 子动作，`ActionId` 各不相同）。日志不关心拼法，只认 `CoreGameEvent.SourceDefId`——效果所属的技能 / 遗物 / 卡主键。

分组规则只有两条：

1. `EffectTriggered` **一律另起一组**并打组头。这样「每回合灼烧」这类反复触发的效果每次各成一组，不会全糊在同一个头下面；也保证「触发了但没造成任何数值」的效果仍然看得见。
2. 数值事件按 `SourceDefId` 与当前组比对：相同就缩进挂进去，不同就开新组；`SourceDefId` 为空（玩家普攻 / 系统结算）则关掉当前组、平铺在第 0 层。

呈现出来就是效果名作组头、它造成的每一笔数值缩进列在下面：

```text
── 第 1 层 · 普通战斗 ──
玩家 → 骷髅战士 -6 (甲 2 · 血 4 · 余 8/0)
【火焰灌注】 骷髅战士
    骷髅战士 -2 (血 2 · 原始 4 · 余 6/0)
    骷髅战士 攻击 -1 (→ 3)
击杀 骷髅战士
+12 金币 (共 132)
```

### 4. 命名：表现层配置器是权威

`BattleLogNaming.ResolveContentName` 的顺序固定：`CardPresentationAuthority.TryGetOwnedDisplayName`（`Assets/Arts/ContentVisual/cards/<contentId>.json` 的 `displayName`，本地化自动生效）→ Catalog 的 `CardContentDefinition` / `SkillContentDefinition` / `RelicContentDefinition` 的 `DisplayName` → 原样返回 defId。

最后那条兜底是刻意的：日志里出现内部 ID 说明表现层缺配，比显示「未知」更容易发现问题。名字按 `LocalizationCatalog.TablesVersion` 缓存，切语言即整体失效重取。

### 5. 分段：`NodeStarted` 即一页

扫到 `CoreEventType.NodeStarted` 就 `BeginSection`，标题取当前 `RunModel.Floor` + `RunModel.Room`（房间名同样走 `Catalog.Rewards.TryGetRoom` 的 `DisplayName`）。段上限 64、段内 600 行，超出丢最旧。

面板打开时滚到底，所以默认视野是当前房间的最新几笔，往上翻就是整局历史——不需要额外的切换按钮。

### 6. 呈现：单块 TMP 富文本

`BattleLogPanel` 不为每行造 GameObject，而是在场景预置的 `canvas/Scroll View/Viewport/Content` 下建一个 `__BattleLogText`（`TextMeshProUGUI`，字体取场景已有的 SmileySans），把整个 Store 拼成一段富文本。缩进用 `<indent=1.6em>`（换行后仍对齐），段标题用 `<b>── 标题 ──</b>`。

渲染预算：最多 400 行 / 20000 字符，从最新的段往回收集，超出在顶部标「…更早的记录已省略」。这既是 TMP 单文本顶点上限的现实约束，也避免长局把面板拖慢。

Store 变更时只打脏标记，`Update` 里合并成一次重建；面板关着时不重建。

### 7. 配色：色相跟飘字，明度跟面板底

飘字打在暗色场地上，日志打在浅米色羊皮纸面板上。照搬飘字的亮绿（`#59FF66`）亮黄会在浅底上糊成一片，所以 `BattleLogPalette` 保色相、压明度：

| 语义 | 日志色 | 对应飘字 |
|---|---|---|
| 血量伤害 | `#B62F2B` | `hpDamageColor` `#CB3834` |
| 治疗 | `#1E7A2C` | `healColor` `#59FF66`（压暗） |
| 护甲 | `#4C6B61` | `armorDamageColor` `#5E7E74` |
| 击杀 / 金币 / 攻击力 / 遗物 / 其他基础数值 | `#8E1B0C` / `#A87508` / `#9A5410` / `#61389A` / `#1F5C86` | — |
| 效果来源名 | `#7A4E0A` | — |
| 正文 / 段标题 / 次要信息 | `#3A2E22` / `#4A3B2A` / `#7A6A55` | — |

换面板底图时这张表要跟着重算——它是为浅底调的。

## 面板层级与命中

面板定位是**场地层中间态**：临时打开看一眼，任何正式 UI 打开都盖住它，它自己不阻塞别的 UI，也不占半黑屏。

- **渲染**：`战斗日志BG` SpriteRenderer `UI/-12`、内部 canvas `UI/-11`、关闭钮 `UI/-10`。UI 层任意 order 都压过场地（BG / Main 层），而 −12 又低于半黑屏与纯黑屏（`UI/-1`）、局内功能菜单与作弊面板（`UI/0`）——正式 UI 一开就盖住它。
- **命中**：占覆层 `HitSortOrder` 6（按钮）/ 7（面板吞点面）/ 8（关闭钮）段位。`BattleUiDimmerOverlay.IsActive` 时，面板的吞点面与关闭钮 collider 每帧自动禁用——视觉被盖住，交互也一并让位。
- **吞点**：Scroll View 那块由 uGUI 自己挡住世界命中（`PointerHitRouter` 见 `EventSystem.IsPointerOverGameObject` 即整体让位），但面板边框没有 Graphic，靠面板根上的 `WorldUiHitButton`（onClick=null）吞掉，否则会点穿到棋盘。
- **按钮门禁**：`战斗日志按钮` 常驻激活而面板默认失活，所以可点性判断挂在按钮上的 `ToggleButtonGate`：主菜单相位、覆层激活、面板已开三者任一成立即禁用 collider。
- **Esc**：面板开着时 Esc 先收面板；`PlayerAudioSettingsPanel.CanOpenFromEscape` 已加入 `BattleLogPanel.IsOpen` 判断，不会同一下既关日志又开功能菜单。

## 对外通信面

- **读**：`IActionPipelineSystem.EventLog`（唯一数据源）、`RunModel`（分段标题）、`CardRegistry`（uid → 卡）、`IContentSystem.Catalog` + `CardPresentationAuthority`（取名）。
- **写**：只写 `BattleLogStore`（进程内内存）。不发 Core 指令、不落盘、不进表现批次。
- **被谁用**：`PresentationSceneRoot.WireHosts`（装 Recorder）、`BattleLogPanel`（读 Store）。

## 不变量与坑

- **只读旁路**：Recorder 全程 try-catch，任何异常都不得反噬结算或表演。Store 的 `Changed` 回调同样包着 try-catch——面板出错不能拖垮记录。
- **聚合看 `SourceDefId`，不看 `ActionId`**：效果的 FollowUp 子动作 ActionId 会变，按 ActionId 分组会把一个效果切成好几段。
- **去重看同 `ActionId` 簇**：`HasSiblingInAction` 依赖「同一动作的事件在 EventLog 里必然相邻」这一事实。若将来 Core 改成交错追加，去重会失效。
- **名字兜底露 defId 是有意的**，别改成「未知」——那样表现层缺配就查不出来了。
- **配色是为浅米色面板底调的**，换底图必须重调，不能直接抄飘字颜色。
- **字形要验**：SmileySans 缺 `▸`（U+25B8）这类几何图形区字符，会渲成方块；箭头用 `→`（U+2192）。新增符号先在 Play 里看一眼。
- 面板排序层与命中段位都是**当前场景元素的空隙**，新增覆层前先查 `docs/code-map/presentation.md` 的覆层 `HitSortOrder` 分段表，同 sort + 同 type 会被 Router 判为装配错误。

## 本篇文件清单（5）

`BattleLog/BattleLogRecorder.cs`、`BattleLog/BattleLogStore.cs`、`BattleLog/BattleLogEntry.cs`、`BattleLog/BattleLogNaming.cs`、`BattleLog/BattleLogPalette.cs`（均在 `Flow/BattleLog/`）；面板 `Ui/BattleLogPanel.cs` 见 [Systems与通信/08-Ui与Cheat](../Systems与通信/08-Ui与Cheat.md)。
