using System.Collections.Generic;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 怪物卡面攻击模式槽图标（临时接线，ADR-0038 图标矩阵子集）。
    /// 覆盖普通近战 / 斜角近战 / 全向近战；普通远程复用全向近战图标；节奏源行动计数图标切换待后续 Catalog。
    /// 素材：<c>Assets/Resources/ContentArt/Multiple/1786242411378_d.png</c> 子 Sprite。
    /// </summary>
    public static class CardFaceAttackPatternIconResolver
    {
        public const string SheetResourcesKey = "ContentArt/Multiple/1786242411378_d";

        // 与图集中相邻的三档攻击模式图标对齐（预制体默认斜角 = d_35）：
        // d_33 = 正交「+」；d_34 = 全向命中点；d_35 = 斜角「X」。
        // 切勿再用 d_30~d_32（骷髅/背包等非攻击模式切片）。
        private const string OrthogonalMeleeSpriteName = "1786242411378_d_33";
        private const string OmnidirectionalMeleeSpriteName = "1786242411378_d_34";
        private const string DiagonalMeleeSpriteName = "1786242411378_d_35";

        private static readonly Dictionary<AttackPattern, string> PatternSpriteNames =
            new Dictionary<AttackPattern, string>
            {
                { AttackPattern.OrthogonalMelee, OrthogonalMeleeSpriteName },
                { AttackPattern.OmnidirectionalMelee, OmnidirectionalMeleeSpriteName },
                { AttackPattern.DiagonalMelee, DiagonalMeleeSpriteName },
                { AttackPattern.Ranged, OmnidirectionalMeleeSpriteName },
            };

        private static Dictionary<string, Sprite> sSpritesByName;
        private static bool sLoadAttempted;

        /// <summary>
        /// 解析局内怪物 <see cref="AttackPattern"/> 对应的攻击模式槽 Sprite。
        /// 未接线模式（如普通远程）返回 false，由 Binder 回退模板默认。
        /// </summary>
        public static bool TryGet(AttackPattern pattern, out Sprite sprite)
        {
            sprite = null;
            if (!PatternSpriteNames.TryGetValue(pattern, out var spriteName)
                || string.IsNullOrEmpty(spriteName))
            {
                return false;
            }

            EnsureLoaded();
            return sSpritesByName != null
                && sSpritesByName.TryGetValue(spriteName, out sprite)
                && sprite != null;
        }

#if UNITY_EDITOR
        public static void InvalidateCacheForTests()
        {
            sSpritesByName = null;
            sLoadAttempted = false;
        }
#endif

        private static void EnsureLoaded()
        {
            if (sLoadAttempted)
            {
                return;
            }

            sLoadAttempted = true;
            sSpritesByName = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
            var loaded = Resources.LoadAll<Sprite>(SheetResourcesKey);
            if (loaded == null)
            {
                Debug.LogWarning("[CardFaceAttackPatternIcon] Missing sprite sheet: " + SheetResourcesKey);
                return;
            }

            for (var i = 0; i < loaded.Length; i++)
            {
                var entry = loaded[i];
                if (entry == null || string.IsNullOrEmpty(entry.name))
                {
                    continue;
                }

                sSpritesByName[entry.name] = entry;
            }
        }
    }
}
