using System;
using System.Collections.Generic;
using NineGrid.Cards.Slots;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 项目级词条表：名字 / 详细介绍 / 可选颜色；可选 code+sprite 供 <c>[code]</c> 内联图标。
    /// <c>[[展示名]]</c> 按 <see cref="Entry.displayNameZh"/> 精确匹配；禁止占用装配槽代号。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardFaceDescriptionIconCatalog",
        menuName = "NineGrid/Cards/Description Icon Catalog")]
    public sealed class CardFaceDescriptionIconCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("描述占位代号，例如 armor（写入文案为 [armor]）。空 = 纯文字词条。")]
            public string code = string.Empty;

            [Tooltip("词条名字；[[名字]] 精确匹配键，详情行标题。")]
            public string displayNameZh = string.Empty;

            [Tooltip("词条详细介绍；右键详情行正文。")]
            [TextArea(2, 6)]
            public string explanation = string.Empty;

            [Tooltip("认知分区（产品编排）；空则 Others。")]
            public string partition = "Others";

            [Tooltip("描述内联使用的 Sprite；空则该代号不解析为图标。")]
            public Sprite sprite;

            [Tooltip("可选词条着色；a=0 表示未配置，跟随正文默认色。")]
            public Color color = new Color(1f, 1f, 1f, 0f);

            /// <summary>是否配置了覆盖色（a &gt; 0）。</summary>
            public bool HasColorOverride => color.a > 0.001f;

            /// <summary>是否可作为 <c>[code]</c> 图标词条。</summary>
            public bool HasInlineIcon =>
                !string.IsNullOrWhiteSpace(code) && sprite != null;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _codeLookup;
        private Dictionary<string, Entry> _nameLookup;

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
                   || string.Equals(code, CardFaceSlotCodes.SyncRhythmIcon, StringComparison.Ordinal)
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
            if (!TryGetByCode(code, out var entry) || entry == null || entry.sprite == null)
            {
                return false;
            }

            sprite = entry.sprite;
            return true;
        }

        /// <summary>按 <c>[code]</c> 代号查词条（保留装配槽名时拒绝）。</summary>
        public bool TryGetByCode(string code, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(code) || IsReservedAssemblySlotCode(code))
            {
                return false;
            }

            EnsureLookup();
            return _codeLookup.TryGetValue(code, out entry) && entry != null;
        }

        /// <summary>兼容旧调用：等同 <see cref="TryGetByCode"/>。</summary>
        public bool TryGetEntry(string code, out Entry entry) => TryGetByCode(code, out entry);

        /// <summary>按 <c>[[展示名]]</c> 精确匹配 <see cref="Entry.displayNameZh"/>。</summary>
        public bool TryGetByDisplayName(string displayName, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            EnsureLookup();
            return _nameLookup.TryGetValue(displayName.Trim(), out entry) && entry != null;
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
            if (_codeLookup.TryGetValue(code, out var existing) && existing != null)
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
                color = new Color(1f, 1f, 1f, 0f),
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
                color = new Color(1f, 1f, 1f, 0f),
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
                    if (entry == null)
                    {
                        continue;
                    }

                    // 允许纯文字词条（无 code）；有 code 时仍禁装配槽保留名。
                    if (!string.IsNullOrEmpty(entry.code) && IsReservedAssemblySlotCode(entry.code))
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
            _codeLookup = null;
            _nameLookup = null;
        }

        private void EnsureLookup()
        {
            if (_codeLookup != null && _nameLookup != null)
            {
                return;
            }

            _codeLookup = new Dictionary<string, Entry>(StringComparer.Ordinal);
            _nameLookup = new Dictionary<string, Entry>(StringComparer.Ordinal);
            if (entries == null)
            {
                entries = new List<Entry>();
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.code)
                    && !IsReservedAssemblySlotCode(entry.code))
                {
                    _codeLookup[entry.code] = entry;
                }

                if (!string.IsNullOrWhiteSpace(entry.displayNameZh))
                {
                    _nameLookup[entry.displayNameZh.Trim()] = entry;
                }
            }
        }

        private void OnValidate()
        {
            InvalidateLookup();
        }
    }
}
