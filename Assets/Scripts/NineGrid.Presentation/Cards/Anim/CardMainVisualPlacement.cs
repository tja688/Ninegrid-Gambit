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
            CardMainVisualAnchorMode anchorMode = CardMainVisualAnchorMode.BottomCenter,
            bool mirrorX = false)
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

            // 拒写 NaN/Inf，避免退化锚定路径把节点甩飞。
            if (!IsFinite(local))
            {
                return;
            }

            target.transform.localPosition = local;
            PreserveVisualCenterIfMirrored(target, scale, mirrorX);
        }

        /// <summary>
        /// 在父节点局部空间的 mask 包围盒内摆放主视图（无 MonoBehaviour Mask 时，如战前预览槽框）。
        /// </summary>
        public static void ApplyToRendererWithMaskBounds(
            SpriteRenderer target,
            Bounds maskInParent,
            Sprite referenceSprite,
            float uniformScale,
            float offsetX,
            float offsetY,
            CardMainVisualAnchorMode anchorMode = CardMainVisualAnchorMode.BottomCenter,
            bool mirrorX = false)
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
            if (target.sprite != null)
            {
                local = CardMainVisualMaskAnchor.GetAnchoredLocalPositionInParentSpace(
                    target,
                    maskInParent,
                    anchorMode);
                local.x += offsetX;
                local.y += offsetY;
            }
            else
            {
                local = target.transform.localPosition;
                local.x = offsetX;
                local.y = offsetY;
            }

            if (!IsFinite(local))
            {
                return;
            }

            target.transform.localPosition = local;
            PreserveVisualCenterIfMirrored(target, scale, mirrorX);
        }

        /// <summary>
        /// 先按正向摆好，再只翻 scale.x，并把世界可视中心钉回翻转前，
        /// 这样左右看只改朝向，不改卡面里的站位。
        /// </summary>
        private static void PreserveVisualCenterIfMirrored(SpriteRenderer target, float absScale, bool mirrorX)
        {
            if (!mirrorX || target == null)
            {
                return;
            }

            var keepX = target.bounds.center.x;
            var scale = target.transform.localScale;
            scale.x = -Mathf.Abs(absScale > 0.0001f ? absScale : 1f);
            target.transform.localScale = scale;

            var deltaX = keepX - target.bounds.center.x;
            if (Mathf.Abs(deltaX) < 0.0001f)
            {
                return;
            }

            var world = target.transform.position;
            world.x += deltaX;
            target.transform.position = world;
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
