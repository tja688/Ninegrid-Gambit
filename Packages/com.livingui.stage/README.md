# Living UI Stage（`com.livingui.stage`）

世界空间「灵动 UI」舞台：**固定身份载体** + **构型蓝图终态** + **纯数据转场规划/播放** + **随行内容投影**。

本包从 TableNine（九宫牌局）抽出，目标是可跨项目复用；**不依赖** QFramework / 业务 Core / Cards / Flow。

---

## 现状（2026-07-21 对齐）

| 项 | 状态 |
| --- | --- |
| 核心转场（Planner / Player / Director） | 已落地，EditMode 可测 |
| 内容层（Marker / Controller / Driver） | 已落地 |
| Demo 输入（数字键 Commit / Hover Preview） | 已落地（Runtime/Demo） |
| 场地覆层 / 输入仲裁 / 完整碰撞搜索 | **未做**（见 Spec 后续阶段） |
| 与宿主游戏流程接线 | **无编译耦合**；靠场景物体名与宿主自取锚点 |
| 渲染 Look（PixelSnap / UICamera） | **不在本包**（宿主侧 VisualLook 等） |

在 TableNine 工程内：可玩主循环在 `MainScene`，**不依赖**本包 Director；完整舞台在 `UITestSence`。

---

## 安装

### 本仓库（已嵌入）

路径：`Packages/com.livingui.stage/`（Unity 自动识别为嵌入式包）。

### 其它项目

任选其一：

1. 复制整个文件夹到目标项目的 `Packages/com.livingui.stage/`
2. 或在目标 `Packages/manifest.json` 增加：
   `"com.livingui.stage": "file:../path/to/com.livingui.stage"`
3. 或日后改为 git URL 依赖

程序集名暂为 `NineGrid.LivingUI`（保留既有场景/Prefab 脚本 GUID 与 `m_EditorClassIdentifier`）。跨项目若要改名，需同步改 asmdef / 命名空间并重绑组件。

---

## 程序集

| 程序集 | 路径 | 依赖 |
| --- | --- | --- |
| `NineGrid.LivingUI` | `Runtime/` | 仅 Unity 引擎（asmdef `references: []`） |
| `NineGrid.LivingUI.Editor` | `Editor/` | Runtime |
| `NineGrid.LivingUI.Tests` | `Tests/Editor/` | Runtime + Test Runner |

命名空间：`NineGrid.LivingUI` / `.Unity` / `.Demo` / `.Editor` / `.Tests`。

---

## 运行时用法（最短）

1. 场景准备权威根（默认名「大盘」）下载体 `1`…`12`（`SpriteRenderer` + `CarrierView`）。
2. 准备若干构型蓝图根（默认可序列化绑定；TableNine 样例名为「大盘构型0-…」等）。
3. 挂 `LivingUiSceneLayoutSource` + `LivingUiDirector`（及可选 ContentController/Driver）。
4. 调用 `LivingUiDirector.Commit(layoutId)` / `Preview` / `ClearPreview`。

纯数据主测 seam：`TransitionPlanner`（见 Tests）。

---

## 宿主缝合约定（非编译依赖）

本包**不移动**外部玩法物体。宿主若要把手牌/场地等挂到载体上，应：

- 把物体挂在载体 `ContentAttach` 下，或
- 用 `CarrierAnchorRegistry` 按名取世界位姿后自行驱动。

`Editor/Legacy/*` 菜单下的迁移工具含历史项目物体名（如 `CardHandAnchors`），属一次性场景修补，**不是**跨项目 API。

---

## 文档

- [`Documentation~/README.md`](Documentation~/README.md) — 包内权威索引与类型路由
- [`Documentation~/STATUS.md`](Documentation~/STATUS.md) — 与 Spec 差距 / 解耦说明
- 历史 Spec（产品语境）：仓库 `Assets/Notes/归档/灵动UI架构-spec-2026-07-17.md`

---

## 编辑器菜单

统一前缀 **`LivingUI/`**（含动效手感、迁移、原初位姿）。带 `Legacy/` 的为历史项目批处理。
