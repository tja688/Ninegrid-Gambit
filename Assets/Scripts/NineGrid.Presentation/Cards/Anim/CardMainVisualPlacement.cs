using UnityEngine;

namespace NineGrid.Cards.Anim
{
    /// <summary>
    /// 主视图 scale / 参考 sprite / Mask 锚定 + 偏移（Player 与 Editor 共用）。
    /// </summary>
    public static class CardMainVisualPlacement
    {
        public static void ApplyToRenderer(
            SpriteRenderer target,
            CardMainVisualMaskAnchor maskAnchor,
            Sprite referenceSprite,
            float uniformScale,
            float offsetX,
            float offsetY,
            CardMainVisualAnchorMode anchorMode = CardMainVisualAnchorMode.BottomCenter)
        {
            if (target == null)
            {
                return;
            }

            var scale = uniformScale > 0.0001f ? uniformScale : 1f;
            target.transform.localScale = new Vector3(scale, scale, 1f);

            if (referenceSprite != null)
            {
                target.sprite = referenceSprite;
            }

            Vector3 local;
            if (maskAnchor != null && target.sprite != null)
            {
                local = maskAnchor.GetAnchoredLocalPosition(target, anchorMode);
                local.x += offsetX;
                local.y += offsetY;
            }
            else if (maskAnchor != null)
            {
                local = maskAnchor.GetAnchoredLocalPosition(target, CardMainVisualAnchorMode.TransformOrigin);
                local.x += offsetX;
                local.y += offsetY;
            }
            else
            {
                local = target.transform.localPosition;
                local.x = offsetX;
                local.y = offsetY;
            }

            target.transform.localPosition = local;
        }
    }
}
