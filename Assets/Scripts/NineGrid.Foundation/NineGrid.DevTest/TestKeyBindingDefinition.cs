#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// SO 可序列化的按键声明（仅键位与标签，回调由运行时模块挂载）。
    /// </summary>
    [Serializable]
    public struct TestKeyBindingDefinition
    {
        [Tooltip("绑定的按键。")]
        public KeyCode key;

        [Tooltip("在监视器/调试 UI 中显示的标签。")]
        public string label;
    }
}

#endif
