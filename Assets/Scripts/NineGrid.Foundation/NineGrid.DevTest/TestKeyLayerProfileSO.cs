#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 测试按键层配置：声明一层可绑定的键位与显示名。运行时回调由 TestKeyModuleBehaviour 挂载。
    /// </summary>
    [CreateAssetMenu(fileName = "TestKeyLayer", menuName = "NineGrid/DevTest/Test Key Layer")]
    public sealed class TestKeyLayerProfileSO : ScriptableObject
    {
        [Tooltip("层唯一标识，与运行时 Register 使用的 LayerId 对应。")]
        [SerializeField] private string layerId = "layer-id";

        [Tooltip("在监视器与调试 UI 中显示的层名称。")]
        [SerializeField] private string displayName = "Test Layer";

        [Tooltip("本层声明的按键（仅元数据；回调在运行时由模块提供）。")]
        [SerializeField] private List<TestKeyBindingDefinition> declaredBindings = new();

        public string LayerId => layerId;

        public string DisplayName => displayName;

        public IReadOnlyList<TestKeyBindingDefinition> DeclaredBindings => declaredBindings;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(layerId))
            {
                layerId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }
    }
}

#endif
