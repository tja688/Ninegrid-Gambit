using System;

namespace NineGrid.Content
{
    /// <summary>
    /// 特效库条目（visual_effects.json）。与 DSL 效果模板无关。
    /// </summary>
    [Serializable]
    public sealed class VisualEffectEntryDto
    {
        public string id = string.Empty;
        public string category = string.Empty;
        public string variantId = string.Empty;
        public string size = string.Empty;
        public string color = string.Empty;
        public string sheetPath = string.Empty;
        public float defaultFps = 12f;
        public float defaultScale = 1f;
        public string displayName = string.Empty;

        /// <summary>预留：后续时机装配关联（本阶段恒为空数组）。</summary>
        public string[] timingBindings = Array.Empty<string>();
    }
}
