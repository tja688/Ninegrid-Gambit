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
            if (spriteMask != null)
            {
                var maskRendererOnMask = spriteMask.GetComponent<SpriteRenderer>();
                if (maskRendererOnMask != null && maskRendererOnMask.sprite != null)
                {
                    return maskRendererOnMask.bounds;
                }
            }

            if (maskRenderer != null && maskRenderer.sprite != null)
            {
                return maskRenderer.bounds;
            }

            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sprite != null)
                {
                    return renderers[i].bounds;
                }
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
            if (spriteMask != null)
            {
                visual.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
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
