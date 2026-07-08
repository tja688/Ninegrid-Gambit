#if UNITY_EDITOR

using UnityEngine;

namespace NineGrid.DevTest.Editor
{
    /// <summary>
    /// 测试目录项：来自 Layer Profile SO 声明，可在编辑器中直接展示；执行仍依赖 Play Mode 回调。
    /// </summary>
    public readonly struct TestKeyCatalogEntry
    {
        public TestKeyCatalogEntry(
            string layerId,
            string layerDisplayName,
            KeyCode key,
            string label,
            bool hasLiveCallback,
            bool isKeypadActive)
        {
            LayerId = layerId ?? string.Empty;
            LayerDisplayName = layerDisplayName ?? layerId;
            Key = key;
            Label = label ?? string.Empty;
            HasLiveCallback = hasLiveCallback;
            IsKeypadActive = isKeypadActive;
        }

        public string LayerId { get; }

        public string LayerDisplayName { get; }

        public KeyCode Key { get; }

        public string Label { get; }

        /// <summary>Play Mode 中对应 DevKeys 模块是否已挂载回调。</summary>
        public bool HasLiveCallback { get; }

        /// <summary>小键盘级联中该键是否由本层持有（仅 Play Mode 有意义）。</summary>
        public bool IsKeypadActive { get; }
    }
}

#endif
