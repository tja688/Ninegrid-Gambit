using UnityEngine;

namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 场地图标预制体本地缩放不一致（有的 0.2、有的 5–6）；落格后统一压进格内。
    /// </summary>
    public static class RoomIconVisualFit
    {
        /// <summary>相对 slotHitBox 的可视占比（略小于命中盒）。</summary>
        public const float TargetFill = 0.85f;

        public static float ComputeUniformScale(Vector2 spriteWorldSizeAtUnitScale, Vector2 targetMaxSize)
        {
            if (spriteWorldSizeAtUnitScale.x <= 0.0001f || spriteWorldSizeAtUnitScale.y <= 0.0001f)
            {
                return 1f;
            }

            if (targetMaxSize.x <= 0.0001f || targetMaxSize.y <= 0.0001f)
            {
                return 1f;
            }

            var sx = targetMaxSize.x / spriteWorldSizeAtUnitScale.x;
            var sy = targetMaxSize.y / spriteWorldSizeAtUnitScale.y;
            return Mathf.Min(sx, sy);
        }

        /// <summary>
        /// 重置根缩放后按 Sprite 世界尺寸压进目标盒；无 Sprite 则保持 prefab 缩放。
        /// </summary>
        public static void FitToTarget(GameObject go, Vector2 targetMaxSize)
        {
            if (go == null)
            {
                return;
            }

            var renderer = go.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            go.transform.localScale = Vector3.one;
            // bounds 已含 renderer 相对根的损失缩放；unit 根下即「单位缩放世界尺寸」。
            var size = renderer.bounds.size;
            var scale = ComputeUniformScale(new Vector2(size.x, size.y), targetMaxSize);
            go.transform.localScale = new Vector3(scale, scale, scale);
        }

        public static Vector2 ResolveTargetSize(Vector2 slotHitBoxSize)
        {
            if (slotHitBoxSize.x <= 0.0001f || slotHitBoxSize.y <= 0.0001f)
            {
                slotHitBoxSize = new Vector2(1.6f, 2.2f);
            }

            return slotHitBoxSize * TargetFill;
        }
    }
}
