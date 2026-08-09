using NineGrid.Cards.Anim;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 战斗信息槽图标摆放：直接复用卡面 <c>mainVisual</c>（+ idle 槽偏移），再乘面板分类外部缩放。
    /// 不再做 Cover 铺满 / pivot·zoom 倒推。
    /// </summary>
    public static class BattleInfoSlotArtFit
    {
        private const float MinScale = 0.0001f;

        public static void ApplyMainVisual(
            SpriteRenderer art,
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

            // 无卡面 Mask：offset 即本地坐标；与编辑器调好的 scale/偏移一致后再叠外部缩放。
            CardMainVisualPlacement.ApplyToRenderer(
                art,
                maskAnchor: null,
                referenceSprite: null,
                uniformScale: scale,
                offsetX: offsetX,
                offsetY: offsetY,
                anchorMode: CardMainVisualAnchorMode.TransformOrigin);
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
    }
}
