# DevTest 接入完整流程

## 架构一览

```
TestKeyLayerProfileSO (层元数据 + 声明键位)
        ↓ 列入
TestKeyStackConfigSO.layers  ← 优先级唯一权威（下高上低）
        ↓ SetStack (BeforeSceneLoad)
TestKeyManager._stackOrder
        ↓ AttachLayer (OnEnable，只挂回调)
RebuildCascade → ActiveBindings → TestKeyInputPoller.PollInput
```

## 文件与类型

| 路径 | 用途 |
|------|------|
| `Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/` | 运行时框架 |
| `Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/Editor/` | 编辑器菜单与 Monitor |
| `Assets/Resources/DevTest/TestKeyStack.asset` | 级联栈 SO（权威） |
| `Assets/Resources/DevTest/Layer_*.asset` | 各层 Profile |

| 类型 | 职责 |
|------|------|
| `TestKeyModuleBehaviour` | 模块基类：`OnEnable`/`OnDisable` |
| `TestKeyLayerProfileSO` | `layerId`、显示名、声明键位 |
| `TestKeyStackConfigSO` | 层列表顺序 |
| `TestKeyManager` | 级联解析与轮询 |
| `TestKeyDevTestAssetMenu` | 创建资产、**RegisterLayerAsHighestPriority** |

## 新建层（AI 执行清单）

- [ ] 1. 确定 `layerId`（kebab-case，与现有层不重复）
- [ ] 2. 创建 `Layer_<Name>.asset`，填写 `layerId` / `displayName` / `declaredBindings`
- [ ] 3. **调用 `RegisterLayerAsHighestPriority` 或 `CreateOrUpdateLayerAsHighestPriority`**，确保层在 `TestKeyStack` 栈底
- [ ] 4. 新建 `<Feature>DevKeys.cs`，继承 `TestKeyModuleBehaviour`
- [ ] 5. `ConfigureBindings` 内 `builder.Bind(KeyCode.KeypadN, "标签", callback)`
- [ ] 6. Unity MCP：目标对象 Add Component，绑定 `layerProfile`
- [ ] 7. `refresh_unity` + `read_console`
- [ ] 8. Play + Test Key Monitor 验证归属

## layerId 对齐规则

`TestKeyModuleBehaviour.LayerId` 解析顺序：

1. 有 `layerProfile` → `layerProfile.LayerId`
2. 无 profile → `ModuleId`

**必须**与 `TestKeyStack` 中该层的 `layerId` 一致。不一致会导致：

- 层不在 SO 配置集 → 被当作动态层，**最低优先级** + Console Warning
- SO 里留下空壳层，监视器显示异常

## 键位与级联

- 多层的同键（如都绑 `Keypad1`）只有**栈底**层生效，其余为「溢出」
- 新功能默认栈底 → 自然获得同键优先权
- 若需与高层共存不同键，在 `declaredBindings` 与 `ConfigureBindings` 同步声明即可

## 参考实现

| 示例 | 路径 |
|------|------|
| 牌组单例 | `Cards/CardDeckManagerDevKeys.cs` |
| 卡牌视图 | `Cards/StandardCardViewDevKeys.cs` |
| 单元测试 | `NineGrid.DevTest.Tests/TestKeyManagerTests.cs` |

## Editor API（AI 可调用）

```csharp
using NineGrid.DevTest.Editor;

// 新建或更新 Profile 并置栈底
TestKeyDevTestAssetMenu.CreateOrUpdateLayerAsHighestPriority(
    assetPath, layerId, displayName, bindings);

// 已有 Profile，仅置顶
TestKeyDevTestAssetMenu.RegisterLayerAsHighestPriority(layerProfile);
```

菜单：`NineGrid/DevTest/Register Selected Layer As Highest Priority`
