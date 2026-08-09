using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 战斗信息槽 Cover 适配：固定槽位尺寸铺满裁切 + 可选 pivot/zoom。
    /// 不复用卡面 <c>mainVisual</c> / <see cref="NineGrid.Cards.Anim.CardMainVisualPlacement"/>。
    /// </summary>
    public static class BattleInfoSlotArtFit
    {
        private const float MinSize = 0.0001f;

        public static void ApplyCover(
            SpriteRenderer art,
            Vector2 slotLocalSize,
            CardPresentationBattleInfoSlotDisplayDto display = null)
        {
            if (art == null || art.sprite == null)
            {
                return;
            }

            if (slotLocalSize.x < MinSize || slotLocalSize.y < MinSize)
            {
                return;
            }

            var spriteSize = art.sprite.bounds.size;
            if (spriteSize.x < MinSize || spriteSize.y < MinSize)
            {
                return;
            }

            var zoom = display != null && display.zoom > MinSize ? display.zoom : 1f;
            var cover = Mathf.Max(slotLocalSize.x / spriteSize.x, slotLocalSize.y / spriteSize.y);
            var scale = cover * zoom;
            if (scale < MinSize || float.IsNaN(scale) || float.IsInfinity(scale))
            {
                return;
            }

            var pivotX = display != null ? display.pivotX : 0f;
            var pivotY = display != null ? display.pivotY : 0f;

            // 先让 sprite.bounds 中心对齐槽中心，再按溢出量叠 pivot（0 = 居中）。
            var center = art.sprite.bounds.center;
            var scaledW = spriteSize.x * scale;
            var scaledH = spriteSize.y * scale;
            var local = new Vector3(
                -center.x * scale + pivotX * (scaledW - slotLocalSize.x),
                -center.y * scale + pivotY * (scaledH - slotLocalSize.y),
                0f);

            if (!IsFinite(local) || !IsFinite(scale))
            {
                return;
            }

            art.transform.localScale = new Vector3(scale, scale, 1f);
            art.transform.localPosition = local;
        }

        public static void ResetArtTransform(SpriteRenderer art)
        {
            if (art == null)
            {
                return;
            }

            art.transform.localPosition = Vector3.zero;
            art.transform.localScale = Vector3.one;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
