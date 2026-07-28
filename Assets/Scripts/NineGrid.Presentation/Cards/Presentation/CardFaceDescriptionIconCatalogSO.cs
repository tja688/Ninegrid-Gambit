using System;
using System.Collections.Generic;
using NineGrid.Cards.Slots;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 项目级描述专用图标表：自定义代号 → Sprite（与装配槽无关）。
    /// 禁止占用装配槽代号；不可反向覆盖 Main_Icon / Action_Icon 等高层装配结果。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardFaceDescriptionIconCatalog",
        menuName = "NineGrid/Cards/Description Icon Catalog")]
    public sealed class CardFaceDescriptionIconCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("描述占位代号，例如 Poison（写入文案为 [Poison]）。")]
            public string code = string.Empty;

            [Tooltip("可选中文名，编辑器列表与详情展开显示。")]
            public string displayNameZh = string.Empty;

            [Tooltip("词条解释；详情页展开，与卡面图标同源。")]
            [TextArea(2, 6)]
            public string explanation = string.Empty;

            [Tooltip("认知分区（产品编排）；空则 Others。")]
            public string partition = "Others";

            [Tooltip("描述内联使用的 Sprite；空则该代号不解析为图标。")]
            public Sprite sprite;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public static bool IsReservedAssemblySlotCode(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            return string.Equals(code, CardFaceSlotCodes.MainIcon, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.FaceBackground, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.CardFrame, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.Banner, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.BackBorder, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.BackShirt, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.BackLogo, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.ActionIcon, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.Name, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.Attack, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.Armor, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.Hp, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.ActionCount, StringComparison.Ordinal)
                   || string.Equals(code, CardFaceSlotCodes.BasicDescription, StringComparison.Ordinal);
        }

        public bool TryGet(string code, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(code) || IsReservedAssemblySlotCode(code))
            {
                return false;
            }

            EnsureLookup();
            if (!_lookup.TryGetValue(code, out var entry) || entry == null || entry.sprite == null)
            {
                return false;
            }

            sprite = entry.sprite;
            return true;
        }

        public bool TryGetEntry(string code, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            EnsureLookup();
            return _lookup.TryGetValue(code, out entry) && entry != null;
        }

        public bool TryAddOrUpdate(string code, Sprite sprite, string displayNameZh, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(code))
            {
                error = "代号不能为空";
                return false;
            }

            code = code.Trim();
            if (IsReservedAssemblySlotCode(code))
            {
                error = "代号占用装配槽保留名：" + code;
                return false;
            }

            EnsureLookup();
            if (_lookup.TryGetValue(code, out var existing) && existing != null)
            {
                existing.sprite = sprite;
                if (displayNameZh != null)
                {
                    existing.displayNameZh = displayNameZh;
                }

                InvalidateLookup();
                return true;
            }

            var created = new Entry
            {
                code = code,
                sprite = sprite,
                displayNameZh = displayNameZh ?? string.Empty,
                explanation = string.Empty,
                partition = "Others",
            };
            entries.Add(created);
            InvalidateLookup();
            return true;
        }

        public bool TryRemove(string code)
        {
            if (string.IsNullOrEmpty(code) || entries == null)
            {
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !string.Equals(entry.code, code, StringComparison.Ordinal))
                {
                    continue;
                }

                entries.RemoveAt(i);
                InvalidateLookup();
                return true;
            }

            return false;
        }

        public bool TryRemoveEntry(Entry entry)
        {
            if (entry == null || entries == null)
            {
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (!ReferenceEquals(entries[i], entry))
                {
                    continue;
                }

                entries.RemoveAt(i);
                InvalidateLookup();
                return true;
            }

            return false;
        }

        public Entry AddBlankEntry()
        {
            var created = new Entry
            {
                code = string.Empty,
                displayNameZh = string.Empty,
                explanation = string.Empty,
                partition = "Others",
                sprite = null,
            };
            entries.Add(created);
            InvalidateLookup();
            return created;
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

                    if (IsReservedAssemblySlotCode(entry.code))
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
            if (entries == null)
            {
                entries = new List<Entry>();
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null
                    || string.IsNullOrEmpty(entry.code)
                    || IsReservedAssemblySlotCode(entry.code))
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
