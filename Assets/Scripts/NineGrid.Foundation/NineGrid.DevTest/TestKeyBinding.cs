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
    /// 已注册测试动作描述，供编辑器面板等 UI 展示；不受小键盘级联影响。
    /// </summary>
    public readonly struct TestKeyRegisteredAction
    {
        public TestKeyRegisteredAction(
            string layerId,
            string layerDisplayName,
            KeyCode key,
            string label,
            bool isKeypadActive)
        {
            LayerId = layerId ?? throw new ArgumentNullException(nameof(layerId));
            LayerDisplayName = layerDisplayName ?? layerId;
            Key = key;
            Label = label ?? string.Empty;
            IsKeypadActive = isKeypadActive;
        }

        public string LayerId { get; }

        public string LayerDisplayName { get; }

        public KeyCode Key { get; }

        public string Label { get; }

        /// <summary>该键位在小键盘级联中是否由本层持有。</summary>
        public bool IsKeypadActive { get; }
    }

    /// <summary>
    /// 全局表中生效的按键条目，包含当前拥有层与原始绑定。
    /// </summary>
    public readonly struct ActiveTestKeyBinding
    {
        public ActiveTestKeyBinding(string layerId, KeyCode key, TestKeyBinding binding)
        {
            LayerId = layerId ?? throw new ArgumentNullException(nameof(layerId));
            Key = key;
            Binding = binding;
        }

        public string LayerId { get; }

        /// <summary>兼容旧字段名。</summary>
        public string ModuleId => LayerId;

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
