# Flow 编辑器工具（Editor）

> 覆盖 `Assets/Scripts/Flow/Editor/**`。程序集：`NineGrid.Flow.Editor`（依赖 NineGrid.Flow、DamageNumbersPro；仅 Editor）。

---

## 菜单与入口

| 菜单路径 | 类型 | 作用 |
|----------|------|------|
| `NineGrid/Diagnostics/诊断日志控制台` | `DiagTraceEditorWindow` | 四轨开关、会话状态、手动导出、历史文件 |
| `NineGrid/VFX/Create Damage Number Popup Prefab` | `DamageNumberPrefabSetup` | 创建伤害飘字 Prefab |
| `NineGrid/Living UI/大盘构型布局工具` | `LivingUiGrandLayoutWindow` | 场景切片 Sprite 前台布局 |

Play 模式钩子：`BattleTracePlayModeExporter`（`[InitializeOnLoad]` 静态构造）在退出 Play 时自动导出。

---

## 逐文件说明

### DiagTraceEditorWindow.cs

- 四轨定义与目录前缀：

  | Track | 标签 | Dir | 文件前缀 |
  |-------|------|-----|----------|
  | Battle | BattleLog | Logs/OtherLog/BattleLog | battlelog |
  | Core | CoreLog | Logs/CoreLog | corelog |
  | Perf | PerfLog | Logs/PerfLog | perflog |
  | Registry | RegistryLog | Logs/OtherLog/RegistryLog | registrylog |

- UI：录制开关、自动导出总闸与分轨、手动「全部/分轨」导出（`BattleTraceRecorder.ExportBothNow` / `ExportTrackNow`）、打开 Notes/Logs、最近文件列表。
- 与 `DiagTraceExportPreferences` / `DiagTraceShared` 直连。

### BattleTracePlayModeExporter.cs

- Play 退出自动导出 BattleTrace（及 ExportOnPlayExit 内连带轨）。
- 与 `InBattleManager.OnDestroy` 双保险；`DiagTraceShared` 去重。
- 启动时 `Reload` + `ApplyRecordingToRecorders`。

### DamageNumberPrefabSetup.cs

- 菜单创建 Damage Numbers Pro 用的 Popup Prefab，供 `DamageNumberManagerSingleton.defaultPrefab` 装配。

### LivingUi/LivingUiGrandLayoutWindow.cs

- EditorWindow：活体 UI 大舞台前台布局；驱动 Session 与 Scene 预览。

### LivingUi/LivingUiGrandLayoutSession.cs

- 会话状态：选中集合、舞台矩形、Scene 预览与写回。

### LivingUi/LivingUiStageLayoutSolver.cs

- 舞台卡位求解：水平铺排/居中；约束边距与舞台高。

### LivingUi/LivingUiSlicedRectUtil.cs

- 读写九宫切片矩形：`SpriteRenderer` DrawMode.Sliced/Tiled + size。

---

## Asmdef

`NineGrid.Flow.Editor.asmdef`：

- references: `NineGrid.Flow`, `DamageNumbersPro`
- includePlatforms: `Editor`

---

## 完整文件清单（7）

1. `DiagTraceEditorWindow.cs`
2. `BattleTracePlayModeExporter.cs`
3. `DamageNumberPrefabSetup.cs`
4. `LivingUi/LivingUiGrandLayoutWindow.cs`
5. `LivingUi/LivingUiGrandLayoutSession.cs`
6. `LivingUi/LivingUiStageLayoutSolver.cs`
7. `LivingUi/LivingUiSlicedRectUtil.cs`

另：`NineGrid.Flow.Editor.asmdef`
