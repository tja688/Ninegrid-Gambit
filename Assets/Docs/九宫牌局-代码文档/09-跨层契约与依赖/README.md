# 09 · 跨层契约与依赖（代码事实）

> 回答重构时最关键的问题：**谁允许依赖谁、桥在哪里、体量集中在哪**。

---

## 允许的依赖方向（asmdef）

```text
Core ← Content
Core ← Flow
Content ← Flow
Cards ← Flow
Cards ↛ Core     （程序集级禁止；Cards 运行时源码亦不 using NineGrid.Core）
LivingUI（`Packages/com.livingui.stage`）/ VisualLook ↛ Flow/Cards/Core（asmdef 空或仅 URP/TMP）
DevTest → Flow, Cards, Core
TemporaryTest → Flow
```

---

## 运行时桥接点

| 桥 | 位置 | 事实 |
|----|------|------|
| Architecture 单例 | `NineGridArchitecture.Current` | Flow（尤其 `InBattleManagerSingleton`）大量 `GetModel` / `GetSystem` |
| 内容装载 | `TableNineContentCatalog` + Core `IContentSystem` | Content 实现/填充，Core 消费契约 |
| 命令入口 | `CoreCommands` / `CoreCommandDispatcher` | Flow 发命令进核 |
| 表现同步 | `IPresentationSyncSystem` + Flow `PresentationDirector` | 核内排队/批次 ↔ Flow 时间线 ACK |
| 卡牌视图 | Flow → `CardManagerSingleton` / `GroundFieldManagerSingleton` 等 | Cards 提供表现 API，不读规则 Model |
| UITest 反向解耦 | `IUITestKeyConsumer` 在 Flow；实现在 TemporaryTest | 避免 Flow → Temporary 依赖 |

---

## 枢纽文件体量（行数，扫描时点）

| 文件 | 行数 | 角色 |
|------|------|------|
| `Flow/InBattleManagerSingleton.cs` | **~5048** | 局内 Core 桥 + Present 适配；类注释称编排出口已硬切到 `PresentationDirector` |
| `Cards/GroundFieldManagerSingleton.cs` | **~1986** | 场地表现 |
| `Flow/MainGameLoopManagerSingleton.cs` | **~1187** | 主循环 |
| `Cards/CardManagerSingleton.cs` | **~676** | 卡牌实体/视图注册 |
| `Flow/Presentation/PresentationDirector.cs` | **~166** | 时间线导演（相对瘦） |

重构含义（事实推导）：逻辑与集成密度集中在 **InBattle + GroundField + MainGameLoop**；导演本身不厚。

---

## Cards ↔ Core 隔离验证

- `NineGrid.Cards.asmdef` **无** `NineGrid.Core` / `QFramework`。  
- `Assets/Scripts/Cards/**` 生产代码 grep：`NineGrid.Core` / `QFramework` **无匹配**。  
- `Cards/Tests` 中少数文件引用 Core/Flow 类型（测试程序集可放宽），属测试侧桥，不是运行时 Cards→Core。

---

## Flow 同时触摸的层

`InBattleManagerSingleton` using 列表（事实样本）：

- `NineGrid.Cards` / `NineGrid.Cards.Convergence`
- `NineGrid.Core` / `NineGrid.Core.Stats` / `NineGrid.Core.Systems`
- `NineGrid.Flow.Diagnostics` / `NineGrid.Flow.Presentation`
- `QFramework`、UniTask、DOTween

即：**Flow = 集成皮层**。

---

## Content ↔ Core

- Content 引用 Core；Catalog（如 `TableNineContentCatalog`）using `NineGrid.Core` / `Core.Content` / `Core.Effects`。  
- Core **不**引用 Content 程序集（asmdef）；通过 Core 内 Content 契约 + 运行时注入/注册对接。

---

## UI 层耦合方式

- LivingUI asmdef `references: []` → 与玩法程序集**无编译依赖**。  
- Flow.Editor 存在 `LivingUi/*` 布局工具 → **编辑期**耦合。  
- 运行时若联调，依赖场景里的 MonoBehaviour 引用或同默认程序集（需以场景装配为准；本库不读 `.unity` 文本）。

---

## 重构切割建议线（仍是事实归纳，非策划）

1. **保留**：Core 无引擎 + QFramework 边界。  
2. **保留**：Cards 不引 Core。  
3. **必拆焦点**：`InBattleManagerSingleton` 体量与「导演已外提」注释之间的残留职责。  
4. **数据边界**：`Generated/Luban` 只经生成管线更新。  
5. **可删候选**：`Temporary Test` 程序集（见 10）。

---

## 子文档（后续可增）

| 文档 | 用途 |
|------|------|
| （本页） | 总契约 |
| 各层 README | 层内 API |

更细的「命令列表 / 表现 Intent 枚举」见 Core Commands-Queries 与 Flow Presentation 文档。
