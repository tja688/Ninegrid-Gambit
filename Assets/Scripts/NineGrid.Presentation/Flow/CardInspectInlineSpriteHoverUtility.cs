using System;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 详述预览描述内联 <c>[code]</c> sprite 的 hover 命中与代号解析（ADR-0037）。
    /// 按 TMP 字符四边形世界点测，不依赖 RectTransform 闸门或最近字符。
    /// </summary>
    internal static class CardInspectInlineSpriteHoverUtility
    {
        /// <summary>世界空间点测外扩，补偿像素对齐与负 margin 描边。</summary>
        public const float SpriteHitPaddingWorld = 0.02f;

        public static bool TryFindHoveredSpriteCode(TMP_Text text, Camera uiCamera, out string code)
        {
            code = null;
            if (text == null || text.textInfo == null || !text.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!WorldPointerUtility.TryGetPointerWorld(uiCamera, out var world))
            {
                return false;
            }

            text.ForceMeshUpdate(true);
            var textInfo = text.textInfo;
            var bestArea = float.MaxValue;
            string bestCode = null;

            for (var i = 0; i < textInfo.characterCount; i++)
            {
                var info = textInfo.characterInfo[i];
                if (!info.isVisible || info.elementType != TMP_TextElementType.Sprite)
                {
                    continue;
                }

                var bl = text.transform.TransformPoint(info.bottomLeft);
                var tl = text.transform.TransformPoint(info.topLeft);
                var tr = text.transform.TransformPoint(info.topRight);
                var br = text.transform.TransformPoint(info.bottomRight);

                if (!ContainsPointInQuadWorld(world, bl, tl, tr, br, SpriteHitPaddingWorld))
                {
                    continue;
                }

                if (!TryResolveSpriteCode(text, info, out var resolved) || string.IsNullOrEmpty(resolved))
                {
                    continue;
                }

                var area = ComputeQuadArea2D(bl, tl, tr, br);
                if (area < bestArea)
                {
                    bestArea = area;
                    bestCode = resolved;
                }
            }

            if (string.IsNullOrEmpty(bestCode))
            {
                return false;
            }

            code = bestCode;
            return true;
        }

        /// <summary>
        /// 优先读运行时 <see cref="TMP_SpriteAsset"/> 字符名（Builder 写入的 <c>[code]</c>）；
        /// 失败则从源串当前标签正向解析 <c>&lt;sprite name="…"&gt;</c>。
        /// </summary>
        internal static bool TryResolveSpriteCode(TMP_Text text, TMP_CharacterInfo info, out string code)
        {
            code = null;
            if (TryResolveSpriteCodeFromAsset(text, info, out code))
            {
                return true;
            }

            return TryParseSpriteNameFromSource(text != null ? text.text : null, info.index, out code);
        }

        private static bool TryResolveSpriteCodeFromAsset(TMP_Text text, TMP_CharacterInfo info, out string code)
        {
            code = null;
            var spriteAsset = text?.spriteAsset;
            if (spriteAsset?.spriteCharacterTable == null)
            {
                return false;
            }

            var unicode = (uint)info.character;
            for (var i = 0; i < spriteAsset.spriteCharacterTable.Count; i++)
            {
                var spriteChar = spriteAsset.spriteCharacterTable[i];
                if (spriteChar == null
                    || spriteChar.unicode != unicode
                    || string.IsNullOrEmpty(spriteChar.name))
                {
                    continue;
                }

                code = spriteChar.name;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 从 <paramref name="tagIndex"/> 起正向截取当前 <c>&lt;sprite name="…"&gt;</c>，禁止往回跨标签。
        /// </summary>
        internal static bool TryParseSpriteNameFromSource(string source, int tagIndex, out string code)
        {
            code = null;
            if (string.IsNullOrEmpty(source) || tagIndex < 0 || tagIndex >= source.Length)
            {
                return false;
            }

            var tagStart = tagIndex;
            if (!StartsWithIgnoreCase(source, tagIndex, "<sprite"))
            {
                var forward = source.IndexOf("<sprite", tagIndex, StringComparison.OrdinalIgnoreCase);
                if (forward >= 0)
                {
                    tagStart = forward;
                }
                else
                {
                    var backward = source.LastIndexOf("<sprite", tagIndex, StringComparison.OrdinalIgnoreCase);
                    if (backward < 0)
                    {
                        return false;
                    }

                    var close = source.IndexOf('>', backward);
                    if (close < 0 || tagIndex > close)
                    {
                        return false;
                    }

                    tagStart = backward;
                }
            }

            var sliceEnd = Math.Min(source.Length, tagStart + 96);
            var slice = source.Substring(tagStart, sliceEnd - tagStart);
            const string marker = "name=\"";
            var nameAt = slice.IndexOf(marker, StringComparison.Ordinal);
            if (nameAt < 0)
            {
                return false;
            }

            var valueStart = nameAt + marker.Length;
            var valueEnd = slice.IndexOf('"', valueStart);
            if (valueEnd <= valueStart)
            {
                return false;
            }

            code = slice.Substring(valueStart, valueEnd - valueStart);
            return !string.IsNullOrEmpty(code);
        }

        internal static bool ContainsPointInQuadWorld(
            Vector3 worldPoint,
            Vector3 bottomLeft,
            Vector3 topLeft,
            Vector3 topRight,
            Vector3 bottomRight,
            float paddingWorld)
        {
            var p = new Vector2(worldPoint.x, worldPoint.y);
            var bl = new Vector2(bottomLeft.x, bottomLeft.y);
            var tl = new Vector2(topLeft.x, topLeft.y);
            var tr = new Vector2(topRight.x, topRight.y);
            var br = new Vector2(bottomRight.x, bottomRight.y);

            if (paddingWorld > 0f)
            {
                var center = (bl + tl + tr + br) * 0.25f;
                bl = ExpandFromCenter(bl, center, paddingWorld);
                tl = ExpandFromCenter(tl, center, paddingWorld);
                tr = ExpandFromCenter(tr, center, paddingWorld);
                br = ExpandFromCenter(br, center, paddingWorld);
            }

            return PointInTriangle(p, bl, tl, tr) || PointInTriangle(p, bl, tr, br);
        }

        internal static float ComputeQuadArea2D(
            Vector3 bottomLeft,
            Vector3 topLeft,
            Vector3 topRight,
            Vector3 bottomRight)
        {
            var bl = new Vector2(bottomLeft.x, bottomLeft.y);
            var tl = new Vector2(topLeft.x, topLeft.y);
            var tr = new Vector2(topRight.x, topRight.y);
            var br = new Vector2(bottomRight.x, bottomRight.y);
            return TriangleArea(bl, tl, tr) + TriangleArea(bl, tr, br);
        }

        private static Vector2 ExpandFromCenter(Vector2 vertex, Vector2 center, float padding)
        {
            var dir = vertex - center;
            if (dir.sqrMagnitude <= 0.000001f)
            {
                return vertex;
            }

            return vertex + dir.normalized * padding;
        }

        private static float TriangleArea(Vector2 a, Vector2 b, Vector2 c)
        {
            return Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Sign(p, a, b);
            var d2 = Sign(p, b, c);
            var d3 = Sign(p, c, a);
            var hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !hasNeg || !hasPos;
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static bool StartsWithIgnoreCase(string source, int startIndex, string value)
        {
            if (string.IsNullOrEmpty(value)
                || startIndex < 0
                || startIndex + value.Length > source.Length)
            {
                return false;
            }

            return source.IndexOf(value, startIndex, value.Length, StringComparison.OrdinalIgnoreCase) == startIndex;
        }
    }
}
