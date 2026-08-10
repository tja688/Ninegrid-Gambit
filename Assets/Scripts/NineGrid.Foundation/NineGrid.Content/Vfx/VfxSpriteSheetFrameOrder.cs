using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NineGrid.Content.Vfx
{
    /// <summary>atlas / folder 帧名排序：<c>spritesheet_N</c> 数字序稳定，其余回退 _N 后缀规则。</summary>
    public static class VfxSpriteSheetFrameOrder
    {
        private const string SpriteSheetPrefix = "spritesheet_";

        private static readonly Regex FrameIndexRegex = new Regex(
            @"_(\d+)\.(png|PNG)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static int CompareNames(string a, string b)
        {
            var ia = ExtractSortIndex(a);
            var ib = ExtractSortIndex(b);
            var cmp = ia.CompareTo(ib);
            if (cmp != 0)
            {
                return cmp;
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static int ExtractSortIndex(string nameOrPath)
        {
            var name = nameOrPath ?? string.Empty;
            var slash = name.LastIndexOf('/');
            if (slash >= 0 && slash < name.Length - 1)
            {
                name = name.Substring(slash + 1);
            }

            if (name.StartsWith(SpriteSheetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var tail = name.Substring(SpriteSheetPrefix.Length);
                if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                {
                    return index;
                }
            }

            var match = FrameIndexRegex.Match(name);
            if (match.Success
                && int.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var pngIndex))
            {
                return pngIndex;
            }

            var underscore = name.LastIndexOf('_');
            if (underscore >= 0 && underscore < name.Length - 1)
            {
                var tail = name.Substring(underscore + 1);
                if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var suffixIndex))
                {
                    return suffixIndex;
                }
            }

            return int.MaxValue;
        }
    }
}
