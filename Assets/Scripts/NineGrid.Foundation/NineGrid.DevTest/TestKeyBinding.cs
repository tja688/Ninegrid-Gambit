#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using UnityEngine;

namespace NineGrid.DevTest
{
    /// <summary>
    /// 单个测试按键绑定：归属模块、显示标签与回调。
    /// </summary>
    public readonly struct TestKeyBinding
    {
        public TestKeyBinding(string label, Action callback)
        {
            Label = label ?? string.Empty;
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public string Label { get; }

        public Action Callback { get; }

        public void Invoke()
        {
            Callback();
        }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// 全局表中生效的按键条目，包含当前拥有者与原始绑定。
    /// </summary>
    public readonly struct ActiveTestKeyBinding
    {
        public ActiveTestKeyBinding(string moduleId, KeyCode key, TestKeyBinding binding)
        {
            ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
            Key = key;
            Binding = binding;
        }

        public string ModuleId { get; }

        public KeyCode Key { get; }

        public TestKeyBinding Binding { get; }

        public string Label => Binding.Label;

        public void Invoke()
        {
            Binding.Invoke();
        }
    }
}

#endif
