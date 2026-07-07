---
name: table-nine-devtest-keys
description: >-
  Integrate TableNine DevTest keypad bindings: implement *DevKeys scripts via
  TestKeyModuleBehaviour, register TestKeyLayerProfileSO in TestKeyStackConfigSO,
  and set stack priority. Use when the user asks for 开发测试/小键盘/DevTest/TestKey
  接入、装配测试键、新建测试层、置顶某测试/提高测试优先级, or when landing temporary
  keypad acceptance for a feature script.
---

# TableNine DevTest 测试键接入

Editor / Development Build 专用。业务代码**禁止**散落 `Input` 监听；小键盘验收统一走本流程。

## 核心原则

| 概念 | 规则 |
|------|------|
| **优先级唯一权威** | `Assets/Resources/DevTest/TestKeyStack.asset`（`TestKeyStackConfigSO`）的 `layers` 列表顺序 |
| **顺序语义** | 列表**上方低优先级，下方高优先级**；**最底项**在同键冲突时生效 |
| **运行时挂载** | `TestKeyModuleBehaviour.OnEnable` 只注册回调，**不改变**栈顺序 |
| **新建层默认策略** | 必须写入 `TestKeyStack` **栈底**（最高优先级） |
| **置顶** | 修改 SO 顺序，不是运行时抢栈 |

基础设施（通常无需改动）：`TestKeyInputPoller` 在 `BeforeSceneLoad` 自动 `SetStack`；`TestKeyManager` 轮询小键盘。

## 何时读本 Skill

- 为任意脚本/单例/Prefab **新建** DevTest 小键盘验收
- 用户说 **置顶 / 提高优先级 / 让 XX 测试优先**
- 排查测试键冲突、溢出、归属错误

详细步骤：`references/integration-workflow.md`  
置顶专用：`references/promote-workflow.md`  
模板与反模式：`references/templates.md`

## 快速决策

```
用户要小键盘验收？
  ├─ 是 → 读本 skill，走「新建接入」或「置顶」
  └─ 否 → 不碰 DevTest

新建接入？
  └─ 默认 RegisterLayerAsHighestPriority（栈底 = 最高优先级）

用户说置顶某测试？
  └─ 走 promote-workflow（写 SO，Play 中可用 Monitor「写入 SO 并置顶」）
```

## 新建接入（默认最高优先级）

按顺序执行，**不要跳步**：

### 1. 层 Profile SO

路径：`Assets/Resources/DevTest/Layer_<Feature>.asset`

- `layerId`：kebab-case，全局唯一，与脚本 `ModuleId` / `LayerProfile.LayerId` **完全一致**
- `displayName`：监视器显示名
- `declaredBindings`：声明键位与标签（元数据；回调在脚本里）

**注册为最高优先级**（新建层必做）：

```csharp
// Editor 脚本或菜单已提供的 API
NineGrid.DevTest.Editor.TestKeyDevTestAssetMenu.CreateOrUpdateLayerAsHighestPriority(
    "Assets/Resources/DevTest/Layer_MyFeature.asset",
    "my-feature",
    "我的功能测试",
    new[]
    {
        (KeyCode.Keypad1, "动作一"),
        (KeyCode.Keypad2, "动作二"),
    });
```

等效菜单：`NineGrid/DevTest/Register Selected Layer As Highest Priority`（选中 Layer Profile 后执行）。

### 2. DevKeys 脚本

位置：`Assets/Scripts/NineGrid.Foundation/NineGrid.DevTest/`（按域分子目录，如 `Cards/`）

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD

public sealed class MyFeatureDevKeys : TestKeyModuleBehaviour
{
    // 可选：Inspector 将 Layer_<Feature>.asset 拖到基类 layerProfile 字段

    protected override string ModuleId => "my-feature"; // 必须与 SO layerId 一致

    protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
    {
        builder
            .Bind(KeyCode.Keypad1, "动作一", () => { /* 调业务 API */ })
            .Bind(KeyCode.Keypad2, "动作二", () => { /* ... */ });
    }
}

#endif
```

要点：

- 继承 `TestKeyModuleBehaviour`，只实现 `ModuleId` + `ConfigureBindings`
- **不要**在业务类里写 `Input`；**不要**运行时改栈顺序
- 需引用场景对象时：`[SerializeField]` + `[Tooltip]`，或 `Awake`/`OnEnable` 自动查找（Tooltip 注明「运行时自动查找/装配」）
- 键位优先用小键盘 `Keypad1`…（与现有层错开，或依赖级联溢出）

### 3. 场景 / Prefab 装配

- **禁止**手改 `.unity`；用 **Unity MCP** 给目标 GameObject 添加 `*DevKeys` 组件
- 将 `Layer_<Feature>.asset` 拖到 `layerProfile`（推荐）
- 编译后 `refresh_unity`，读 Console 确认无错

### 4. 验证

- Play → `Window > NineGrid > Test Key Monitor`
- 确认新层在栈**最底**（最高优先级）
- 同键冲突时，新层应「生效」，其余层显示「溢出」

## 置顶某测试（用户明确要求时）

见 `references/promote-workflow.md`。摘要：

| 场景 | 动作 |
|------|------|
| 编辑期 | 在 `TestKeyStack` Inspector 把目标层拖到底部，或 `RegisterLayerAsHighestPriority` |
| Play 调试 | Monitor 窗口「**写入 SO 并置顶**」→ 调 `TestKeyManager.PromoteLayerToTop`（写回 SO 再 `SetStack`） |

**禁止**：在 `OnEnable`、业务逻辑或运行时 API 里偷偷改内存栈序。

## 项目约束（与本 skill 同时遵守）

- 不手改 `.unity`；场景组件用 Unity MCP
- `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹 DevTest 脚本
- Inspector 暴露字段必须有 `[Tooltip]`
- 非 Core 代码不使用 QFramework

## 完成汇报模板

```markdown
## DevTest 接入
- 层 ID / SO：`...`
- 脚本：`...DevKeys.cs`
- 装配：`<GameObject 或 Prefab>`
- 栈优先级：栈底（最高）/ 已置顶 `<layerId>`
- 键位：Keypad1 → …, Keypad2 → …
- 验证：Monitor 截图或归属表
```
