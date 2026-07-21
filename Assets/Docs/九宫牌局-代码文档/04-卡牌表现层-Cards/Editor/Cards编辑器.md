# Editor / Cards 编辑器

> 覆盖 `Assets/Scripts/Cards/Editor/`。  
> 程序集：`NineGrid.Cards.Editor`（仅 Editor；引用 `NineGrid.Cards` / `NineGrid.Content` / `NineGrid.Core`）。

---

## 1. 职责

编辑期工具：底盘/卡面装配、装配槽注册表资产、**卡面终态预览构建器**、通层 order 告警、效果 SO、遭遇 Catalog、DOTween 烘焙、像素图库、表现迁移。  
**不参与运行时播放逻辑。**

内容配图的**权威预览**在 `TableNine/Content/Content Visual Editor`（内嵌 `CardFacePreviewHost`）。本程序集提供共享 Builder / Host；`Face Final Preview` 菜单仅为调试薄壳。

---

## 2. 类型详解

### StandardCardPrefabSetup.cs

| API / 菜单 | 作用 |
|------------|------|
| `Setup Card Chassis Prefab` | 演化底盘为 FacePivot 形态 + 确保交互/效果组件（不再在 L4 重建旧万能视觉） |
| `Install Transform Tower On Card Chassis Prefab` | 安装 `CardTransformTower` |
| `Evolve Chassis FacePivot` | 剥 L4 旧视觉，仅留 FacePivot |
| `Ensure Card Face Slot Registry Asset` | 创建/刷新 `Assets/Arts/Cards/CardFaceSlotRegistry.asset` |

底盘权威路径：`CardChassisPaths.ChassisPrefab`（`老Standard Card.prefab`；旧 `Standard Card.prefab` 已失效）。

### CardFacePreviewRequest / Builder / Host

| 类型 | 作用 |
|------|------|
| `CardFacePreviewRequest` | 编辑器假投影输入（含会话草稿 Sprite / BasicDescription） |
| `CardFacePreviewBuilder` | Instantiate 底盘 → 挂 Kind 卡面 → `AttachFaceBinder` → `ApplyPresentation`；通层 order 收集；Insertable 槽列表 |
| `CardFacePreviewHost` | `PreviewRenderUtility` 内嵌渲染；供 Content Visual Editor 使用 |

- 正式路径与运行时 Commit 同族：`ApplyPresentation`（含名字 / 数值样例 / `[SlotCode]` 描述图标）。
- EditMode 可注入支架底盘/卡面 prefab（`TryBuild` 重载）做无资产依赖测试。

### CardFaceFinalPreviewWindow.cs

| 菜单 | 作用 |
|------|------|
| `NineGrid/Cards/Face Final Preview (Chassis + L4)` | **调试薄壳**：调用同一 Builder，Scene 选中/Frame；权威在 Content Visual Editor |

- 假投影默认 DefId → Catalog；可选手填覆盖直暴露槽。
- 「校验通层 sortingOrder」调用 `CardFaceSortingOrderValidator`。

### CardEffectAssetMenu.cs

| API | 作用 |
|-----|------|
| `CreateDefaultEffectAssets` | 创建默认 Attack/Hit/Death/Use/HitFlash 资产 |
| `CreateBasicAttackHitSequenceAssets` | 基础攻击/受击序列资产 |
| `EnsureDefaultAssetsExist` | 缺失则补齐 |
| `LoadDeathEffect` / `LoadUseEffect` / `LoadHitEffect` / `LoadHitFlashEffect` / `LoadAttackEffect` | 按路径加载 |
| `LoadBasicAttackSequenceEffect` / `LoadBasicHitSequenceEffect` | 序列 SO |

### CardDOTweenSequenceBakeUtility.cs

从场景/选中 GO 上的 DOTweenAnimation 组件烘焙为 `CardTweenClip` 列表写入 `CardDOTweenSequenceEffectSO`。

### CardDOTweenSequenceEffectSOEditor.cs

`CardDOTweenSequenceEffectSO` 的自定义 Inspector。

### CardAttackBasicPerformanceSetup.cs

| API | 作用 |
|-----|------|
| `SetupSceneRigs` | 场景内 CardAttackBasic 方向 Rig 装配 |

### BattleEncounterDefaultsSetup.cs

| API | 作用 |
|-----|------|
| `CreateDefaults` | 创建默认 `BattleEncounterCatalogSO` / Profile 资产 |

### CardPresentationMigrationUtility.cs

| API | 作用 |
|-----|------|
| `MigrateSelectedCards` / `MigrateSceneTestCards` | 旧卡根迁移 |
| `RepairSceneTestCardLink` | 修复测试卡链接 |
| `MigrateCardRoot` | 单根迁移（含塔层结构） |

### PixelCardPackSpriteLibraryMenu.cs

| API | 作用 |
|-----|------|
| `CreateOrReloadLibrary` | 创建或从图包重载 `PixelCardPackSpriteLibrary` |

---

## 3. 与运行时的关系

| 编辑器产出 | 运行时消费 |
|------------|------------|
| 卡牌底盘 + FacePivot | `CardManagerSingleton` Spawn |
| 四套卡面模板 | Kind→挂面 |
| `CardFaceSlotRegistry` | 槽代号契约（内容 Catalog 按代号填值） |
| CardEffect* SO | `CardEffectManager.bindings` |
| BattleEncounter Catalog/Profile | `FieldBattleManagerSingleton.encounterCatalog` |
| PixelCardPackSpriteLibrary | `StandardCardView.spriteLibrary` |
| 预览假投影 | 与 `CardFacePresentationBinder.ApplyPresentation` 同出口 |

---

## 4. 完整文件路由

| 文件 | 一句话 |
|------|--------|
| `BattleEncounterDefaultsSetup.cs` | 默认战斗 Catalog/Profile 生成 |
| `CardAttackBasicPerformanceSetup.cs` | 场景攻击 Rig 装配 |
| `CardDOTweenSequenceBakeUtility.cs` | DOTween → CardTweenClip 烘焙 |
| `CardDOTweenSequenceEffectSOEditor.cs` | 序列 SO 自定义 Inspector |
| `CardEffectAssetMenu.cs` | 效果 SO 菜单与加载 |
| `CardFacePreviewRequest.cs` | 编辑器假投影 DTO |
| `CardFacePreviewBuilder.cs` | 底盘+L4+ApplyPresentation 构建 |
| `CardFacePreviewHost.cs` | PreviewRenderUtility 内嵌宿主 |
| `CardFaceFinalPreviewWindow.cs` | 调试薄壳窗（共用 Builder） |
| `CardPresentationMigrationUtility.cs` | 卡根/塔层迁移修复 |
| `PixelCardPackSpriteLibraryMenu.cs` | 像素数字库菜单 |
| `StandardCardPrefabSetup.cs` | 底盘 FacePivot / 槽表资产 |
