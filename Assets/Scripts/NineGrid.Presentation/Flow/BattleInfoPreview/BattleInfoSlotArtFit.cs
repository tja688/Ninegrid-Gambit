using NineGrid.Cards.Anim;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 战斗信息槽图标摆放：复用卡面 <c>mainVisual</c>（+ idle 槽偏移），
    /// 以槽框为 Mask 做 BottomCenter 锚定，再乘面板分类外部缩放。
    /// </summary>
    public static class BattleInfoSlotArtFit
    {
        private const float MinScale = 0.0001f;
        public const float FallbackSlotSize = 0.8f;

        public static void ApplyMainVisual(
            SpriteRenderer art,
            Vector2 slotLocalSize,
            CardPresentationMainVisualDto mainVisual,
            float slotOffsetX,
            float slotOffsetY,
            float externalScale)
        {
            if (art == null)
            {
                return;
            }

            var baseScale = mainVisual != null && mainVisual.uniformScale > MinScale
                ? mainVisual.uniformScale
                : 1f;
            var ext = externalScale > MinScale ? externalScale : 1f;
            var scale = baseScale * ext;
            if (scale < MinScale || float.IsNaN(scale) || float.IsInfinity(scale))
            {
                return;
            }

            var offsetX = ((mainVisual != null ? mainVisual.offsetX : 0f) + slotOffsetX) * ext;
            var offsetY = ((mainVisual != null ? mainVisual.offsetY : 0f) + slotOffsetY) * ext;
            var maskInParent = BuildMaskBounds(slotLocalSize);

            CardMainVisualPlacement.ApplyToRendererWithMaskBounds(
                art,
                maskInParent,
                referenceSprite: null,
                uniformScale: scale,
                offsetX: offsetX,
                offsetY: offsetY,
                anchorMode: CardMainVisualAnchorMode.BottomCenter);
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

        private static Bounds BuildMaskBounds(Vector2 slotLocalSize)
        {
            var width = slotLocalSize.x > MinScale ? slotLocalSize.x : FallbackSlotSize;
            var height = slotLocalSize.y > MinScale ? slotLocalSize.y : FallbackSlotSize;
            return new Bounds(Vector3.zero, new Vector3(width, height, 0.01f));
        }
    }
}
