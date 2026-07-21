# Editor / Cards 编辑器

> 覆盖 `Assets/Scripts/Cards/Editor/`。  
> 程序集：`NineGrid.Cards.Editor`（仅 Editor 平台；引用 `NineGrid.Cards`）。

---

## 1. 职责

编辑期工具：预制体装配、效果 SO 资产菜单、战斗遭遇默认 Catalog、DOTween 序列烘焙、像素图库重载、表现迁移。  
**不参与运行时播放逻辑。**

---

## 2. 类型详解

### StandardCardPrefabSetup.cs

| API | 作用 |
|-----|------|
| `SetupPrefab` / `SetupAllStandardCardPrefabs` | 装配 Standard Card 预制体 |
| `InstallTransformTowerOnPrefab` / `InstallTransformTower` | 安装 `CardTransformTower` |

菜单驱动的预制体一致性工具。

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

| API | 作用 |
|-----|------|
| `BakeFromSelectedObjectMenu` | 菜单入口 |
| `BakeFromGameObject` | 程序入口 |
| `TryMapClip` | Component → CardTweenClip |

### CardDOTweenSequenceEffectSOEditor.cs

`CardDOTweenSequenceEffectSO` 的自定义 Inspector（`UnityEditor.Editor` 子类）。

### CardAttackBasicPerformanceSetup.cs

| API | 作用 |
|-----|------|
| `SetupSceneRigs` | 场景内 CardAttackBasic 方向 Rig 装配 |

含内部 `ClipSpec` 辅助（`WithEndValue`）。

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
| Standard Card + TransformTower | `CardManagerSingleton` Spawn、`SlotFrameConvergence` |
| CardEffect* SO | `CardEffectManager.bindings` |
| BattleEncounter Catalog/Profile | `FieldBattleManagerSingleton.encounterCatalog` |
| PixelCardPackSpriteLibrary | `StandardCardView.spriteLibrary` |
| CardAttackBasic Rigs | `CardAttackBasicAdapter.directionRigs` |

---

## 4. 完整文件路由

| 文件 | 一句话 |
|------|--------|
| `BattleEncounterDefaultsSetup.cs` | 默认战斗 Catalog/Profile 生成 |
| `CardAttackBasicPerformanceSetup.cs` | 场景攻击 Rig 装配 |
| `CardDOTweenSequenceBakeUtility.cs` | DOTween → CardTweenClip 烘焙 |
| `CardDOTweenSequenceEffectSOEditor.cs` | 序列 SO 自定义 Inspector |
| `CardEffectAssetMenu.cs` | 效果 SO 菜单与加载 |
| `CardPresentationMigrationUtility.cs` | 卡根/塔层迁移修复 |
| `PixelCardPackSpriteLibraryMenu.cs` | 像素数字库菜单 |
| `StandardCardPrefabSetup.cs` | Standard Card 预制体与塔安装 |
