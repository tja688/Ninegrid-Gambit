# 置顶某测试层

用户表述示例：置顶 XX 测试、让牌组测试优先、把 XX 调到最高优先级。

## 原则

置顶 = 修改 `TestKeyStackConfigSO.layers` 顺序，使目标层位于**列表最底**。  
不是运行时改 `TestKeyManager` 内存顺序（Play 中除外：Monitor 按钮会**写回 SO**）。

## 方式 A — 编辑期（推荐，可提交）

1. 打开 `Assets/Resources/DevTest/TestKeyStack.asset`
2. 将目标 `TestKeyLayerProfileSO` 拖到列表**最底部**
3. 或在 Project 选中 Layer Profile → 菜单 `NineGrid/DevTest/Register Selected Layer As Highest Priority`
4. 或在 Editor 脚本中：

```csharp
TestKeyDevTestAssetMenu.RegisterLayerAsHighestPriority(layerProfile);
// 内部：stack.RegisterLayerAsHighestPriority → PromoteLayer → 移到底部
```

## 方式 B — Play 模式临时调试

1. `Window > NineGrid > Test Key Monitor`
2. 目标层点击 **「写入 SO 并置顶」**
3. 调用链：`TestKeyManager.PromoteLayerToTop(layerId)` → 写 SO → `SetStack` 重载

仅 `#if UNITY_EDITOR` 可用；会 `SetDirty` 栈 SO，注意是否提交该资源变更。

## 方式 C — 按 layerId 置顶（SO 已有该层）

`TestKeyStackConfigSO.PromoteLayerById(layerId)` 后 `EditorUtility.SetDirty(stack)`。

## AI 决策

| 用户意图 | 动作 |
|----------|------|
| 新建测试并要默认最高优 | `CreateOrUpdateLayerAsHighestPriority` |
| 已有层，要求置顶 | `RegisterLayerAsHighestPriority` 或拖 SO 到底 |
| Play 中快速试 | 提示 Monitor「写入 SO 并置顶」，或代点 MCP 若可用 |
| 仅调低某层 | 把该层拖到栈列表**上方**（不要用已删除的运行时 API） |

## 勿用

- ~~`PromoteThisLayer()`~~（已从 `TestKeyModuleBehaviour` 移除）
- ~~`AttachLayer(..., appendToBottom: true)`~~（已移除）
- 业务 `OnEnable` 里改优先级
