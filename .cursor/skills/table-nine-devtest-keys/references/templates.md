# 模板与成本说明

## 最小 DevKeys 模板

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.DevTest;
using UnityEngine;

namespace NineGrid.DevTest.MyArea
{
    [DisallowMultipleComponent]
    public sealed class FooDevKeys : TestKeyModuleBehaviour
    {
        protected override string ModuleId => "foo-feature";

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder.Bind(KeyCode.Keypad1, "触发 Foo", OnKeypad1);
        }

        private void OnKeypad1()
        {
            // 调用 FooSingleton.Instance / GetComponent / 注入字段
        }
    }
}

#endif
```

## 带依赖解析的模板

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.DevTest;
using UnityEngine;

namespace NineGrid.DevTest.MyArea
{
    public sealed class FooDevKeys : TestKeyModuleBehaviour
    {
        [Tooltip("运行时自动查找同物体 FooBehaviour；也可手动拖入。")]
        [SerializeField] private FooBehaviour foo;

        protected override string ModuleId => "foo-feature";

        protected override void OnEnable()
        {
            foo ??= GetComponent<FooBehaviour>();
            base.OnEnable();
        }

        protected override void ConfigureBindings(TestKeyRegistrationBuilder builder)
        {
            builder.Bind(KeyCode.Keypad1, "执行", () => foo?.Run());
        }
    }
}

#endif
```

## 接入成本评估（对业务脚本）

| 维度 | 重量 | 说明 |
|------|------|------|
| **业务类本身** | **几乎为零** | 不必改业务代码；不引 Input、不引 DevTest 条件编译 |
| **DevKeys 脚本** | **轻** | 通常 20–40 行：一个类 + `ConfigureBindings` + 回调里调业务 API |
| **SO 资产** | **轻** | 一个 Layer Profile + 在 TestKeyStack 登记一行（菜单可一键栈底） |
| **场景装配** | **轻** | Add Component + 可选拖 layerProfile；单例常挂在已有 Manager 物体上 |
| **键位规划** | **视情况** | 若与现有层同键，靠栈优先级；若全新键位，零协调成本 |

**结论**：对「各个脚本本身实现」成本 **基本没有**；额外负担在 **独立的 `*DevKeys` 薄封装 + SO 登记**，整体 **轻量**，不必为测试污染业务架构。

## 反模式

```csharp
// ❌ 业务类内
void Update() { if (Input.GetKeyDown(KeyCode.Keypad1)) ... }

// ❌ layerId 带 InstanceID 后缀（除非刻意做未入 SO 的动态层）
protected override string ModuleId => $"foo::{GetInstanceID()}";

// ❌ 新建层却不写入 TestKeyStack → 最低优先级 + Warning

// ❌ 手改 .unity 挂组件
```

## 多实例

同一 `layerId` 的多组件会**覆盖**同一层的 Bindings（最后 OnEnable 者生效）。  
常规单例/Manager 测试一个 DevKeys 即可；多卡牌实例应共用一个 `standard-card` 层 ID 或只用单例入口测试。
