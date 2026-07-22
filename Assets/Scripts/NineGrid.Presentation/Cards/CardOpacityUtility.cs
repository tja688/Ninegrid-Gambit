using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 统一调节卡牌视图下所有 SpriteRenderer 的 alpha。
    /// </summary>
    public static class CardOpacityUtility
    {
        private static readonly Dictionary<int, Color[]> OriginalColorsByUid = new();

        public static void SetAlpha(ManagedCard card, float alpha)
        {
            if (card?.View == null)
            {
                return;
            }

            var renderers = card.View.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                return;
            }

            CacheOriginalColorsIfNeeded(card.Uid, renderers);
            alpha = Mathf.Clamp01(alpha);

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var original = OriginalColorsByUid[card.Uid][i];
                renderer.color = new Color(original.r, original.g, original.b, original.a * alpha);
            }
        }

        public static void ResetAlpha(ManagedCard card)
        {
            if (card?.View == null)
            {
                return;
            }

            if (!OriginalColorsByUid.TryGetValue(card.Uid, out var originals))
            {
                return;
            }

            var renderers = card.View.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (var i = 0; i < renderers.Length && i < originals.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.color = originals[i];
                }
            }
        }

        public static void ResetAll(IReadOnlyList<ManagedCard> cards)
        {
            if (cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Count; i++)
            {
                ResetAlpha(cards[i]);
            }
        }

        public static void ClearCache(int uid)
        {
            OriginalColorsByUid.Remove(uid);
        }

        private static void CacheOriginalColorsIfNeeded(int uid, SpriteRenderer[] renderers)
        {
            if (OriginalColorsByUid.ContainsKey(uid))
            {
                return;
            }

            var colors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                colors[i] = renderers[i] != null ? renderers[i].color : Color.white;
            }

            OriginalColorsByUid[uid] = colors;
        }
    }
}
