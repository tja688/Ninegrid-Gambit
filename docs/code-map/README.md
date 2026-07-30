# Code Map（轻量入口）

> **权威长期文档**：本目录 + [`docs/adr/`](../adr/) + 根目录 [`CONTEXT.md`](../../CONTEXT.md)。  
> 旧源码镜像库 `Assets/Docs/九宫牌局-代码文档/` 已删除；勿再恢复为权威。

本地图描述 **仓库当下事实**，不描述未落地的目标树。

## 先读

| 文档 | 用途 |
|------|------|
| [presentation.md](./presentation.md) | `NineGrid.Presentation` 目录、装配、QF 边界、扩展点 |
| [tests.md](./tests.md) | EditMode 测试地图与护栏 |
| [ADR-0001](../adr/0001-battle-presentation-unified-timeline-batch-ack.md) | 统一时间线 / Batch-ack |
| [ADR-0002](../adr/0002-card-chassis-and-face-templates.md) | 单底盘四卡面 + Commit |
| [ADR-0004](../adr/0004-input-intake-two-axis-gating.md) | IntentIntake 两轴门禁 |
| [ADR-0005](../adr/0005-card-face-beat-commit.md) | 卡面数值表演锚点提交 |
| [ADR-0006](../adr/0006-windows-high-polling-mouse-mitigation.md) | Win Player 高回报率鼠标兜底 |
| [ADR-0007](../adr/0007-unified-presentation-pipeline.md) | 多处理器统一表现管线 |
| [ADR-0008](../adr/0008-single-source-content-and-resources-loading.md) | 一卡一文件 JSON + ContentArt Resources 加载 |
| [ADR-0009](../adr/0009-parameterized-effect-templates.md) | 效果参数化模板、分类三轴、词条、奖池查询 |
| [ADR-0010](../adr/0010-self-declared-effect-responsibility.md) | 效果责任自陈、拆除外部场域门禁 |
| [ADR-0011](../adr/0011-monster-attack-pattern-intrinsic.md) | 怪物攻击模式为内生必填属性（**#77 数据面 + #80 四开火模式 + #82 内容赋模**；表现见 #81） |
| [ADR-0012](../adr/0012-enemy-action-phase-volley.md) | 敌方行动阶段：齐射与盘面冻结（**#79/#80/#81 已落地**：Core 报名/逐条/收尾 + 四模式单向打击；表现 Counter 分拍 + ActionCount Commit） |
| [ADR-0013](../adr/0013-action-countdown-unified.md) | 行动计数统一为倒计时、开火窗口一次性（**#76 Core 效果侧已落地**；攻击模式消费见 #79/#80；卡面上屏见 #81） |
| [ADR-0014](../adr/0014-theme-ids-are-legacy-opaque.md) | 主题化 contentId/deckId 是历史残留不透明主键；卡组仅内部渠道；勿被虚构命名带偏 |

> ADR-0011–0013 已落地（含卡面倒计时 Commit 与单向打击 Counter 分拍，#81）。落地方案见 `Assets/Notes/怪物攻击模式与敌方行动阶段-落地方案-2026-07-29.md`（过程笔记，非权威）。

## 程序集一览

| 程序集 | 路径 | 职责 |
|--------|------|------|
| `NineGrid.Core` | `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/` | 规则核（QF） |
| `NineGrid.Content` | `Assets/Scripts/NineGrid.Foundation/NineGrid.Content/` | Catalog：**schema≥2 一卡一文件 JSON 投影** + **tables JSON**（效果模板 / **奖池查询规则** / 经济 / 节点规则；卡上 `effectAssemblies` 解析进 `Catalog.Effects`；分类三轴 `deckId`/`role`/`tags`+`rarity`；`ContentCatalogBootstrap.Load` 会 Invalidate 表现/模板静态缓存后重读盘，末尾 `RewardPoolQueryExpander`；业务只消费 `GameContentCatalog`）；`TableNineContentCatalog.CreateDefault` 为小型测试夹具；卡牌表现 JSON + **ContentArt** Resources 根（ADR-0008 / ADR-0009 / #69–#71） |
| `NineGrid.Content.Editor` | `…/NineGrid.Content.Editor/` | 卡牌表现编辑器（侧栏 **卡面 / 效果池 / 特效库 / 卡组·卡背**：按 `deckId` 分组卡面；卡面 **效果装配**默认只挂本卡种同类模板（道具/遗物/怪物技能，可搜索；跨类遗留挂载保留标注）；效果池按三类分组 + 描述词条；**特效库**一级分类+变体二级纯预览（`visual_effects.json`，与 DSL 效果池区分）；**卡组·卡背**页预览+翻转（按 deckId 选四套模板壳，空槽保留模板兜底；卡面页翻转按 `deckId` 回填组背））；`ContentArtBreakLinkValidator` 断链扫描；`VisualEffectsMigrateRunner` 迁 Effects→ContentArt |
| `NineGrid.Presentation` | `Assets/Scripts/NineGrid.Presentation/` | 表现层（原 Flow+Cards **合并后的单一程序集**） |
| `NineGrid.Presentation.Tests` | `…/Tests/` | EditMode |
| `NineGrid.Presentation.Editor` | `…/Editor/` | 编辑器工具 |
| `NineGrid.DevTest` | `Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/` | 小键盘 DevKeys；主菜单 QuickTest 入口（`QuickTestEntryInputHandler`）；**无**局内 `\` 调速 |

旧程序集 `NineGrid.Flow` / `NineGrid.Cards` 的 **asmdef 已删除**；源码仍以 `Flow/`、`Cards/` **目录 + 命名空间** 共存于 `NineGrid.Presentation` 内（见 presentation.md）。

## 场景入口

```
PresentationSceneRoot (IController)
  └─ PresentationCompositionRoot.Install(PresentationSceneBindings)
        ├─ PresentationRuntimeSystem → PresentationDirector → BattleTimeline
        ├─ BattleSession / Geometry / FieldBattle / GameFlowShell / InputState …
        └─ Controllers 由 SceneRoot / Hook 接线绑定场景 Host
```

主场景：`Assets/Scenes/MainScene.unity`、`Assets/Scenes/UITestSence.unity`。

## 维护约定

- 改表现层结构 / 通信范式 → 先改本目录，再改业务。  
- 长期行为不变量 → 写 / 改 `docs/adr/`。  
- 过程笔记 → `Assets/Notes/`（非权威）。
