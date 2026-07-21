# Living UI Stage · 包内文档索引

权威代码根：`Packages/com.livingui.stage/`  
程序集：`NineGrid.LivingUI`（+ Editor / Tests）

## 一句话

以纯数据转场引擎驱动固定身份「载体」在有限构型终态间流动，并投影挂在载体上的随行内容；不拥有宿主玩法物体，也不依赖业务框架。

## 目录

| 路径 | 内容 |
| --- | --- |
| `Runtime/Core/` | 布局契约、内容策略/投影、Planner/Player、缓动与五次曲线 |
| `Runtime/Unity/` | Director、SceneLayoutSource、Carrier*、Content* MonoBehaviour |
| `Runtime/Demo/` | 键盘 Commit / Hover Preview（验收用） |
| `Editor/` | 手感窗口与场景迁移工具（`LivingUI/` 菜单） |
| `Tests/Editor/` | EditMode：Policy / Planner |
| [`STATUS.md`](./STATUS.md) | 与 Spec 差距、解耦与跨项目带走清单 |
| 包根 [`../README.md`](../README.md) | 安装与最短用法 |

## 数据流

```text
构型蓝图根 ──Capture──► LivingUiSceneLayoutSource
                              │
                              ▼
                        LivingUiDirector ──Plan──► TransitionPlanner
                              │                         │
                              │◄──── LivingUiTransitionPlayer ◄─┘
                              ▼
                     运行时载体 1…12（位姿/尺寸）
                              │
            LivingUiContentDriver ◄── Marker / Policy
```

执行顺序（属性）：LayoutSource −300 → Director −200 → ContentController −190 → ContentDriver −180 → Demo −150/−100。

## 源码路由（相对包根）

### Runtime · Core

| 文件 | 主要类型 |
| --- | --- |
| `Runtime/Core/LivingUiContracts.cs` | `LivingUiLayoutId`、`LivingUiTerminal`、`LivingUiLayout`、`LivingUiCarrierState`、`LivingUiTransitionStyle`、`ILivingUiTransitionPlanner` |
| `Runtime/Core/LivingUiContentContracts.cs` | Anchor / MotionMode / Binding / Phase / Policy |
| `Runtime/Core/LivingUiContentNames.cs` | 原初元素命名 |
| `Runtime/Core/LivingUiContentProjector.cs` | 内容投影与 envelope 地板 |
| `Runtime/Core/LivingUiTransitionPlanner.cs` | `TransitionPlanner`、`LivingUiTransitionPlan` |
| `Runtime/Core/LivingUiTransitionPlayer.cs` | 分代播放 |
| `Runtime/Core/LivingUiEasing.cs` | 缓动 |
| `Runtime/Core/QuinticMotion.cs` | 五次位姿 / 有界尺寸 |

### Runtime · Unity

| 文件 | 主要类型 |
| --- | --- |
| `Runtime/Unity/LivingUiDirector.cs` | Commit / Preview / 舞台相机 |
| `Runtime/Unity/LivingUiSceneLayoutSource.cs` | 蓝图抓取、大盘绑定 |
| `Runtime/Unity/CarrierView.cs` | 载体视图 |
| `Runtime/Unity/CarrierAnchorRegistry.cs` | B 类命名锚点查询 |
| `Runtime/Unity/LivingUiContentMarker.cs` | A 类内容声明 |
| `Runtime/Unity/LivingUiContentController.cs` | 构型→内容映射 |
| `Runtime/Unity/LivingUiContentDriver.cs` | LateUpdate 投影 |
| `Runtime/Unity/LivingUiBlueprintPoseSampler.cs` | 蓝图位姿采样 |
| `Runtime/Unity/LivingUiStableHitZoneRoot.cs` | 稳定命中区 |

### Runtime · Demo / Editor / Tests

见包内对应目录；Editor 菜单统一 `LivingUI/`，历史批处理在 `LivingUI/Legacy/`。

## 与 VisualLook 的边界

本包**不包含** PixelSnap / UICamera / 扫描线 Feature。世界字清晰度属渲染例外，由宿主 Look 管线负责；Living UI 只保证文字可作为载体子物体随行。
