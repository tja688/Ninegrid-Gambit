# 08 · Editor 与工具链（代码事实）

---

## 路由

| 区域 | 文档 / 清单 |
|------|-------------|
| Cards Editor | 见下表；细述见 [04 Cards/Editor](../04-卡牌表现层-Cards/Editor/Cards编辑器.md) |
| Flow Editor | 见下表；细述见 [03 Flow/Editor](../03-流程层-Flow/Editor/Flow编辑器工具.md) |
| Content Editor | 见 [02 Content/Editor](../02-内容层-NineGrid.Content/Editor/内容编辑器工具.md) |
| DevTest Editor | 见 [06 DevTest/Editor工具](../06-DevTest/Editor工具.md) |
| LivingUI Editor | 见 [05 LivingUI](../05-UI层/LivingUI/活字与世界UI.md) |
| 项目级 `Assets/Editor` | 本页 |
| Luban 工具链 | 本页 |

---

## `Assets/Editor`（无 asmdef，默认 Assembly-CSharp-Editor）

| 文件 | 从类型名推断 |
|------|----------------|
| `NineGridSpriteBatchWindow.cs` | 精灵批处理 EditorWindow |
| `PixelArtImageProcessorWindow.cs` | 像素图处理 EditorWindow |

---

## 各程序集 Editor 文件清单

### Cards/Editor

- `BattleEncounterDefaultsSetup.cs`
- `CardAttackBasicPerformanceSetup.cs`
- `CardDOTweenSequenceBakeUtility.cs`
- `CardDOTweenSequenceEffectSOEditor.cs`
- `CardEffectAssetMenu.cs`
- `CardPresentationMigrationUtility.cs`
- `PixelCardPackSpriteLibraryMenu.cs`
- `StandardCardPrefabSetup.cs`

### Flow/Editor

- `BattleTracePlayModeExporter.cs`
- `DamageNumberPrefabSetup.cs`
- `DiagTraceEditorWindow.cs`
- `LivingUi/LivingUiGrandLayoutSession.cs`
- `LivingUi/LivingUiGrandLayoutWindow.cs`
- `LivingUi/LivingUiSlicedRectUtil.cs`
- `LivingUi/LivingUiStageLayoutSolver.cs`

### Content.Editor

- `CardFrameStyleXlsxIO.cs`
- `ContentVisualCardPreview.cs`
- `ContentVisualEditorSession.cs`
- `ContentVisualEditorWindow.cs`
- `ContentVisualLubanMenu.cs`
- `ContentVisualSpriteCatalogMigrationMenu.cs`
- `ContentVisualSpriteKeyCodec.cs`
- `ContentVisualXlsxIO.cs`
- `TableNineLubanDataExporter.cs`
- `Ui/ContentVisualWarmConsoleUi.cs`

### DevTest/Editor

- `TestKeyCatalogEntry.cs` / `TestKeyCatalogProvider.cs`
- `TestKeyDevTestAssetMenu.cs`
- `TestKeyMonitorWindow.cs`
- `TestKeyRunnerPlayModeLauncher.cs` / `TestKeyRunnerWindow.cs`
- `TestKeyStackConfigSOEditor.cs`
- `Ui/TestKeyRunnerWarmConsoleUi.cs`

### LivingUI/Editor

- `LivingUiAuthorityStageMigrator.cs`
- `LivingUiContentAnchorMigrator.cs`
- `LivingUiContentBindMigrator.cs`
- `LivingUiFeelWindow.cs`
- `LivingUiLiveContentPoseResync.cs`
- `LivingUiMappingFixBatch.cs`
- `LivingUiPrimordialPoseInit.cs`

---

## Luban 工具链（`Assets/Tools/Luban`）

### 脚本 / 配置

| 文件 | 角色（从文件名） |
|------|------------------|
| `luban.conf` | Luban 配置 |
| `gen_table_nine.bat` / `gen_table_nine.ps1` | 生成入口 |
| `bootstrap_content_visual.py` / `bootstrap_visual_tables.py` | 视觉表引导 |
| `content_visual_xlsx_io.py` / `card_frame_style_xlsx_io.py` | xlsx IO |
| `patch_content_visual_descriptions.py` | 描述补丁 |

### Datas（数据源文件，非 C#）

| 文件 |
|------|
| `cards.json` |
| `effects.json` |
| `economy.json` |
| `monster_decks.json` |
| `node_deck_rules.json` |
| `relics.json` |
| `reward_entries.json` / `reward_pools.json` |
| `rooms.json` |
| `card_frame_style.xlsx` / `content_visual.xlsx` |

生成产物落在 `NineGrid.Content/Generated/Luban/`（见 Content 文档）。

---

## 观察

- 编辑器工具集中在：**内容视觉 / Luban 导出**、**卡牌 Prefab/DOTween 序列烘焙**、**诊断 Trace 导出**、**LivingUI 布局迁移**、**TestKey Runner**。  
- Flow.Editor 含 LivingUi Grand Layout，说明部分 UI 布局工具挂在 Flow 程序集 Editor 下，而非 LivingUI.Editor。
