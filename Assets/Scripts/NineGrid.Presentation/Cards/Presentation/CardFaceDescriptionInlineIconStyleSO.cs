using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 描述内联图标的项目级布局：按槽代号存 em 相对 bearing / baseScale。
    /// 调好后靠 TMP 随字号与描述节点缩放自动跟从，不进单卡 JSON。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardFaceDescriptionInlineIconStyle",
        menuName = "NineGrid/Cards/Description Inline Icon Style")]
    public sealed class CardFaceDescriptionInlineIconStyleSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("槽代号，例如 Action_Icon。")]
            public string code = string.Empty;

            [Tooltip("相对 em 的水平偏移（写入 GlyphMetrics.bearingX）。")]
            public float bearingX;

            [Tooltip("相对 em 的垂直偏移，叠加在默认基线对齐之上（写入 GlyphMetrics.bearingY）。")]
            public float bearingY;

            [Tooltip("相对 1em 行高的倍率；1 = 图标高度约等于当前字号。")]
            public float baseScale = 1f;
        }

        [SerializeField]
        [Tooltip("未单独配置的代号使用的默认 baseScale。")]
        private float defaultBaseScale = 1f;

        [SerializeField]
        [Tooltip("未单独配置的代号使用的默认 bearingX（em）。")]
        private float defaultBearingX;

        [SerializeField]
        [Tooltip("未单独配置的代号使用的默认 bearingY（em，叠加在 0.85em 基线之上）。")]
        private float defaultBearingY;

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _lookup;

        public float DefaultBaseScale => defaultBaseScale > 0.0001f ? defaultBaseScale : 1f;
        public float DefaultBearingX => defaultBearingX;
        public float DefaultBearingY => defaultBearingY;
        public IReadOnlyList<Entry> Entries => entries;

        public void Resolve(string slotCode, out float bearingX, out float bearingY, out float baseScale)
        {
            EnsureLookup();
            if (!string.IsNullOrEmpty(slotCode)
                && _lookup.TryGetValue(slotCode, out var entry)
                && entry != null)
            {
                baseScale = entry.baseScale > 0.0001f ? entry.baseScale : DefaultBaseScale;
                bearingX = entry.bearingX;
                bearingY = entry.bearingY;
                return;
            }

            baseScale = DefaultBaseScale;
            bearingX = DefaultBearingX;
            bearingY = DefaultBearingY;
        }

        public Entry GetOrCreateEntry(string slotCode)
        {
            if (string.IsNullOrEmpty(slotCode))
            {
                return null;
            }

            EnsureLookup();
            if (_lookup.TryGetValue(slotCode, out var existing) && existing != null)
            {
                return existing;
            }

            var created = new Entry
            {
                code = slotCode,
                bearingX = DefaultBearingX,
                bearingY = DefaultBearingY,
                baseScale = DefaultBaseScale,
            };
            entries.Add(created);
            _lookup[slotCode] = created;
            return created;
        }

        public void SetEntry(string slotCode, float bearingX, float bearingY, float baseScale)
        {
            var entry = GetOrCreateEntry(slotCode);
            if (entry == null)
            {
                return;
            }

            entry.bearingX = bearingX;
            entry.bearingY = bearingY;
            entry.baseScale = baseScale > 0.0001f ? baseScale : DefaultBaseScale;
            InvalidateLookup();
        }

        public void ReplaceEntries(IEnumerable<Entry> source)
        {
            entries = new List<Entry>();
            if (source != null)
            {
                foreach (var entry in source)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.code))
                    {
                        continue;
                    }

                    entries.Add(entry);
                }
            }

            InvalidateLookup();
        }

        public void InvalidateLookup()
        {
            _lookup = null;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
            {
                return;
            }

            _lookup = new Dictionary<string, Entry>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.code))
                {
                    continue;
                }

                _lookup[entry.code] = entry;
            }
        }

        private void OnValidate()
        {
            InvalidateLookup();
        }
    }
}
