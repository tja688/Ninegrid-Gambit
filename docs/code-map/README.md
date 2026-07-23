# Code Map（轻量入口）

> 权威长期文档：本目录 + [`docs/adr/`](../adr/)。  
> `Assets/Docs/九宫牌局-代码文档/` 已 **DEPRECATED**（历史镜像，勿再当现状权威）。

## 先读

| 文档 | 用途 |
|------|------|
| [presentation.md](./presentation.md) | `NineGrid.Presentation` 角色目录、装配、扩展点 |
| [tests.md](./tests.md) | EditMode 测试地图 |
| [ADR-0001](../adr/0001-battle-presentation-unified-timeline-batch-ack.md) | 统一时间线 / Batch-ack |
| [ADR-0002](../adr/0002-card-chassis-and-face-templates.md) | 单底盘四卡面 + Commit |

## 程序集一览

| 程序集 | 路径 | 职责 |
|--------|------|------|
| `NineGrid.Core` | `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/` | 规则核（QF） |
| `NineGrid.Content` | `Assets/Scripts/NineGrid.Foundation/NineGrid.Content/` | Catalog / Luban |
| `NineGrid.Presentation` | `Assets/Scripts/NineGrid.Presentation/` | 表现层（原 Flow+Cards） |
| `NineGrid.Presentation.Tests` | `…/Tests/` | EditMode |
| `NineGrid.Presentation.Editor` | `…/Editor/` | 编辑器工具 |
| `NineGrid.DevTest` | `Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/` | 小键盘 DevKeys |

## 场景入口

```
PresentationSceneRoot (IController)
  └─ PresentationCompositionRoot.Install(PresentationSceneBindings)
        ├─ PresentationRuntimeSystem → PresentationDirector → BattleTimeline
        ├─ BattleSession / Geometry / FieldBattle / GameFlowShell / InputState …
        └─ Controllers 由 SceneRoot 绑定场景 Host
```

主场景：`Assets/Scenes/MainScene.unity`、`Assets/Scenes/UITestSence.unity`。
