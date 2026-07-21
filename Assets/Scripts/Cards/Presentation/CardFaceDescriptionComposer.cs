using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NineGrid.Cards.Slots;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 旁路纯数据 seam：把基础描述中的 `[SlotCode]` 解析为 TMP 内联图标标签。
    /// 仅替换注册表中带 <see cref="CardFaceSlotRole.InsertableInDescription"/> 且已装配到 Sprite 的代号；
    /// 普通语义括号（如 `[使用时]`）原样保留。不进数值 Commit 跳动。
    /// </summary>
    public static class CardFaceDescriptionComposer
    {
        private static readonly Regex BracketToken =
            new Regex(@"\[([^\]]+)\]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public readonly struct InlineIcon
        {
            public InlineIcon(string slotCode, Sprite sprite)
            {
                SlotCode = slotCode ?? string.Empty;
                Sprite = sprite;
            }

            public string SlotCode { get; }
            public Sprite Sprite { get; }
        }

        public readonly struct Result
        {
            public Result(string tmpRichText, IReadOnlyList<InlineIcon> icons)
            {
                TmpRichText = tmpRichText ?? string.Empty;
                Icons = icons ?? Array.Empty<InlineIcon>();
            }

            public string TmpRichText { get; }
            public IReadOnlyList<InlineIcon> Icons { get; }
        }

        public static Result Compose(
            string basicDescription,
            IReadOnlyDictionary<string, Sprite> assembledIcons,
            CardFaceSlotRegistrySO registry)
        {
            if (string.IsNullOrWhiteSpace(basicDescription))
            {
                return new Result(string.Empty, Array.Empty<InlineIcon>());
            }

            if (registry == null)
            {
                return new Result(basicDescription, Array.Empty<InlineIcon>());
            }

            var icons = new List<InlineIcon>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var builder = new StringBuilder(basicDescription.Length + 16);
            var offset = 0;
            foreach (Match match in BracketToken.Matches(basicDescription))
            {
                builder.Append(basicDescription, offset, match.Index - offset);
                var code = match.Groups[1].Value;
                if (TryResolveInsertable(code, assembledIcons, registry, out var sprite))
                {
                    builder.Append("<sprite name=\"").Append(code).Append("\">");
                    if (seen.Add(code))
                    {
                        icons.Add(new InlineIcon(code, sprite));
                    }
                }
                else
                {
                    builder.Append(match.Value);
                }

                offset = match.Index + match.Length;
            }

            builder.Append(basicDescription, offset, basicDescription.Length - offset);
            return new Result(builder.ToString(), icons);
        }

        private static bool TryResolveInsertable(
            string slotCode,
            IReadOnlyDictionary<string, Sprite> assembledIcons,
            CardFaceSlotRegistrySO registry,
            out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(slotCode)
                || !registry.TryGet(slotCode, out var definition)
                || definition == null
                || !definition.HasRole(CardFaceSlotRole.InsertableInDescription))
            {
                return false;
            }

            if (assembledIcons == null
                || !assembledIcons.TryGetValue(slotCode, out sprite)
                || sprite == null)
            {
                sprite = null;
                return false;
            }

            return true;
        }
    }
}
