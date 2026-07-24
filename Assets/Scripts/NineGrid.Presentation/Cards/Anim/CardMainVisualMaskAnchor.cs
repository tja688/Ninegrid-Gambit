using UnityEngine;

namespace NineGrid.Cards.Anim
{
    /// <summary>
    /// 卡面「主视图Mask」锚点：提供世界包围盒、建议本地居中、以及 SpriteMask 交互。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardMainVisualMaskAnchor : MonoBehaviour
    {
        public const string NodeName = "主视图Mask";

        [SerializeField]
        private SpriteMask spriteMask;

        [SerializeField]
        private SpriteRenderer maskRenderer;

        private void Awake()
        {
            CacheRefsIfNeeded();
        }

        public Bounds GetWorldBounds()
        {
            CacheRefsIfNeeded();

            // SpriteMask.bounds 在 Renderer 禁用时仍可用；优先它，避免依赖已隐藏的调试 SpriteRenderer。
            if (spriteMask != null && spriteMask.sprite != null)
            {
                return spriteMask.bounds;
            }

            if (maskRenderer != null && maskRenderer.sprite != null)
            {
                return maskRenderer.bounds;
            }

            return new Bounds(transform.position, Vector3.one);
        }

        /// <summary>
        /// 将 visual 中心对齐到 Mask 中心；可选叠加 contentBounds 修正（世界空间内容包围盒）。
        /// </summary>
        public Vector3 GetSuggestedLocalPosition(Transform visual, Bounds? contentBounds = null)
        {
            if (visual == null)
            {
                return Vector3.zero;
            }

            var maskBounds = GetWorldBounds();
            var targetWorld = maskBounds.center;
            if (contentBounds.HasValue)
            {
                var content = contentBounds.Value;
                var delta = maskBounds.center - content.center;
                targetWorld = visual.position + delta;
            }

            var parent = visual.parent;
            if (parent == null)
            {
                return targetWorld;
            }

            return parent.InverseTransformPoint(targetWorld);
        }

        public void ApplyMaskInteraction(SpriteRenderer visual)
        {
            if (visual == null)
            {
                return;
            }

            CacheRefsIfNeeded();
            if (spriteMask == null)
            {
                return;
            }

            // URP / SortingGroup：Mask 必须与被裁剪 Sprite 同 Sorting Layer，
            // 并用 Custom Range 罩住其 order，否则 VisibleInsideMask 会整段消失。
            SyncMaskSortingTo(visual);
            visual.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        /// <summary>
        /// 编辑器 PreviewRenderUtility 往往不跑 URP 2D Mask 模板，导致 VisibleInsideMask 全灭。
        /// 预览实例上关掉 Mask 交互，仅保留定位；Play/运行时仍走真实裁剪。
        /// </summary>
        public static void DisableMaskingForEditorPreview(Transform faceRoot)
        {
            if (faceRoot == null)
            {
                return;
            }

            var masks = faceRoot.GetComponentsInChildren<SpriteMask>(true);
            for (var i = 0; i < masks.Length; i++)
            {
                if (masks[i] != null)
                {
                    masks[i].enabled = false;
                }
            }

            var renderers = faceRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr != null && sr.maskInteraction != SpriteMaskInteraction.None)
                {
                    sr.maskInteraction = SpriteMaskInteraction.None;
                }
            }
        }

        public void SyncMaskSortingTo(SpriteRenderer visual)
        {
            if (visual == null)
            {
                return;
            }

            CacheRefsIfNeeded();
            if (spriteMask == null)
            {
                return;
            }

            spriteMask.frontSortingLayerID = visual.sortingLayerID;
            spriteMask.backSortingLayerID = visual.sortingLayerID;
            var order = visual.sortingOrder;
            spriteMask.frontSortingOrder = order + 32;
            spriteMask.backSortingOrder = order - 32;
            spriteMask.isCustomRangeActive = true;

            // SpriteMask 自身也挂在同一层，避免 Default 层与卡面层脱节。
            spriteMask.sortingLayerID = visual.sortingLayerID;
            spriteMask.sortingOrder = order;
        }

        public static CardMainVisualMaskAnchor FindOrAdd(Transform faceRoot)
        {
            if (faceRoot == null)
            {
                return null;
            }

            var existing = faceRoot.GetComponentInChildren<CardMainVisualMaskAnchor>(true);
            if (existing != null)
            {
                return existing;
            }

            var node = FindDeep(faceRoot, NodeName);
            if (node == null)
            {
                return null;
            }

            var anchor = node.GetComponent<CardMainVisualMaskAnchor>();
            if (anchor == null)
            {
                anchor = node.gameObject.AddComponent<CardMainVisualMaskAnchor>();
            }

            return anchor;
        }

        private void CacheRefsIfNeeded()
        {
            if (spriteMask == null)
            {
                spriteMask = GetComponent<SpriteMask>();
                if (spriteMask == null)
                {
                    spriteMask = GetComponentInChildren<SpriteMask>(true);
                }
            }

            if (maskRenderer == null)
            {
                maskRenderer = GetComponent<SpriteRenderer>();
                if (maskRenderer == null)
                {
                    maskRenderer = GetComponentInChildren<SpriteRenderer>(true);
                }
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
