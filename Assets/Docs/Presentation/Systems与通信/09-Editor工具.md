# Presentation.Editor 编辑器工具（14 个）

> 覆盖范围：`Editor/` 全部 14 个文件（独立程序集 `NineGrid.Presentation.Editor`）。
> 这些工具不进包体（除两个打包入口本身产出包体）；按用途分四组。

## 关键类型表

### A. 卡面/特效预览（供表现层配置编辑器与网页工作台复用）

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `CardFacePreviewHost` | `Editor/CardFacePreviewHost.cs` | `PreviewRenderUtility` 内嵌预览宿主：Rebuild（构建底盘+L4 终态）、Draw（IMGUI 矩形）、`TryRenderStaticPng`（离屏渲染 PNG——网页工作台 `GET /api/asset` 消费；渲染前强制 TMP `ForceMeshUpdate`，因一次性离屏没有编辑器循环驱动 TMP 惰性重建）、`ValidateSortingOrders`（通层 sortingOrder 重复告警）、世界→GUI 坐标换算（词条 hover 命中用） |
| `CardFacePreviewBuilder` | `Editor/CardFacePreviewBuilder.cs` | 终态构建：底盘 prefab（`CardChassisPaths.ChassisPrefab`）+ 按 Kind 挂 L4 卡面 + `ApplyPresentation`（与运行时 Commit 同族出口）；`Room` Kind 走图标预制体分支；支持注入支架底盘/卡面供 EditMode 测试；产出 SortingWarnings |
| `CardFacePreviewRequest` | `Editor/CardFacePreviewRequest.cs` | 假投影输入 DTO：会话草稿 Sprite/描述/数值直接灌入（不必先保存），`ToSnapshot()` 转 `CardPresentationSnapshot` |
| `VisualEffectPreviewHost` | `Editor/VisualEffectPreviewHost.cs` | 特效库预览：左标准怪物卡参照（默认 `monster.stone_man`，构建时禁掉主视图遮罩）+ 右 `SpriteSheetLoopPlayer` 精灵表循环；按内容与预览宽高比自动取景 |

### B. 卡面特效 SO 烘焙

| 类型 | 文件 | 一句话职责 |
|-----|------|-----------|
| `CardDOTweenSequenceEffectSOEditor` | `Editor/CardDOTweenSequenceEffectSOEditor.cs` | `CardDOTweenSequenceEffectSO` 的 Inspector：「从选中对象烘焙 Tween 片段」按钮 |
| `CardDOTweenSequenceBakeUtility` | `Editor/CardDOTweenSequenceBakeUtility.cs` | 反射读取选中 GameObject 上的 `DOTweenAnimation`（DOTweenPro 类型按名解析）→ 映射为 `CardTweenClip` 列表写入 SO；不支持的动画类型逐个告警跳过 |

### C. 诊断/剖析

| 类型 | 文件 | 一句话职责 |
|-----|------|-----------|
| `BattleTracePlayModeExporter` | `Editor/BattleTracePlayModeExporter.cs` | `[InitializeOnLoad]`：进 Play 清四轨 Recorder + 重置 DiagBeatClock；退 Play（且 AutoExport 开）自动导出 BattleTrace/FlowTrace（CoreLog/PerfLog 在 `ExportOnPlayExit` 内一并尝试）+ AssetDatabase.Refresh；与 InBattle OnDestroy 双保险、DiagTraceShared 内去重 |
| `DiagTraceEditorWindow` | `Editor/DiagTraceEditorWindow.cs` | 菜单 `NineGrid/归档/诊断日志控制台`：四轨（Battle/Core/Perf/Registry）开关、会话状态、手动加记、最近日志文件列表（目录映射 `Assets/Notes/Logs/…`） |
| `ProfilerCaptureAnalyzer` | `Editor/ProfilerCaptureAnalyzer.cs` | 菜单 `NineGrid/Diagnostics/Analyze Latest Profiler Capture`：加载 Profiler `.data`，dump 尖峰帧 + hierarchy top 到 `.analysis.json`（一次性排查工具，默认路径写死历史捕获文件） |

### D. 打包与资产批处理

| 类型 | 文件 | 一句话职责 |
|-----|------|-----------|
| `DevPlayerBuild` | `Editor/DevPlayerBuild.cs` | Development Win64 打包（菜单两条：`Builds/DevWin64/` 与桌面 `game1/`，后者含 F12 作弊面板）；异步队列 + `Temp/ninegrid_dev_player_build_status.json` 状态轮询——绕过 Pipeline `build --options` string[] 绑定失效（CLI 传 Development 到不了服务端） |
| `ReleasePlayerBuild` | `Editor/ReleasePlayerBuild.cs` | Release（非 Development）Win64 打包镜像（#142 终验：验证 Release 无 QuickTest 入口、无 DevTest Missing Script——`#if DEVELOPMENT_BUILD` 组件被剥掉） |
| `FeelTransitionSceneInstall` | `Editor/FeelTransitionSceneInstall.cs` | 一次性/可重入：MainScene 装配 Feel `MMFaderRound`+`Directional` 过场 Canvas + 创建 `Resources/Transitions/RunSceneTransition.asset`（菜单 `NineGrid/Setup/Install Run Scene Transitions`） |
| `SmileySansSdfCharsetBaker` | `Editor/SmileySansSdfCharsetBaker.cs` | SmileySans SDF 原地补字（7000 汉字字符集，不换 GUID 不重绑场景）：开 Multi Atlas、`TryAddCharacters`、关 Clear Dynamic Data On Build（菜单 `NineGrid/Fonts/…`；供 CLI eval） |
| `UiStrokeThickenBatch` | `Editor/UiStrokeThickenBatch.cs` | Fantasy UI 包素材 9-slice 描边加粗批处理（localScale×2 + Sprite size÷2 保持世界尺寸，同步 collider/子节点/文字缩放；`__UiStroke2x` 标记防重复；支持 Dry Run；作用于 MainScene + 5 个指定预制体） |

## 核心流程

- **预览链**（表现层配置编辑器 / 网页工作台共用）：`CardPresentationEditorSession`（Content.Editor 侧）→ `CardFacePreviewRequest`（草稿直灌）→ `CardFacePreviewBuilder.TryBuild`（底盘+卡面+ApplyPresentation，同运行时 Commit 出口——**预览即最终态**，ADR-0002 精神）→ `CardFacePreviewHost.Draw` 或 `TryRenderStaticPng`。
- **日志链**：Play 中四轨 Recorder 收集（由 `DiagnosticOutputController` Attach，见《07》）→ 退 Play `BattleTracePlayModeExporter` 自动导出 → `Assets/Notes/Logs/…` → `table-nine-battlelog-analysis` 技能消费。
- **打包链**：菜单/CLI → Queue（写 status json）→ `EditorApplication.update` 泵真正 BuildPlayer → 状态文件轮询（Pipeline/agent 可读）。

## 对外通信面

- 消费方：`NineGrid.Content.Editor`（卡牌表现编辑器、网页工作台 asset 端点）、Unity CLI eval、构建流水线。
- 依赖：`NineGrid.Cards`（底盘/卡面/动画）、`NineGrid.Flow.Diagnostics`（四轨 Recorder）、DOTweenPro（反射）、Feel/MMTools（过场资产）、TMP。

## 关联 ADR / Issue

ADR-0002（预览走同族 Commit 出口）、ADR-0008（Resources 加载约定）、#141（预览房间图标分支）、#142（Release 终验）、#195/表现层网页工作台（`TryRenderStaticPng` 端点）。

## 不变量与坑

- 预览构建**必须**走 `ApplyPresentation` 同族出口——禁止在编辑器里另写一套"手动摆卡面"，否则预览与运行时漂移。
- `TryRenderStaticPng` 前置 TMP `ForceMeshUpdate` 是必需的；去掉会得到无文字的 PNG。
- 两个打包入口是**异步队列**：连续调用会得到 busy；状态只认 `Temp/*.json`。
- `ProfilerCaptureAnalyzer.DefaultCapture` 写死本机历史路径——菜单入口在别的机器上会 `missing:`，用 `Analyze(path)` 传参。
- `UiStrokeThickenBatch`/`SmileySansSdfCharsetBaker`/`FeelTransitionSceneInstall` 均是改资产的批处理，跑之前确认 git 干净、跑之后目视检查。
