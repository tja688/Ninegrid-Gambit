using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 将 <see cref="ContentVisualResolvedView"/> 绑定到 Standard Card 预制体层级。
    /// </summary>
    public static class TableNineCardVisualBinder
    {
        private const string MainIconChild = "MainIcon";
        private const string FaceChild = "Standard Card";
        private const string FrameChild = "Card Frame ";

        public static bool TryApplyForDefId(IArchitecture architecture, Transform actorRoot, string defId)
        {
            if (actorRoot == null || string.IsNullOrEmpty(defId) || architecture == null)
            {
                return false;
            }

            if (!ContentCatalogRuntimeBootstrap.EnsureLoaded(architecture))
            {
                return false;
            }

            var coreCatalog = architecture.GetSystem<IContentSystem>().Catalog;
            var visualCatalog = ContentCatalogRuntimeBootstrap.VisualCatalog;
            var frameStyleCatalog = ContentCatalogRuntimeBootstrap.FrameStyleCatalog;
            if (coreCatalog == null || visualCatalog == null || frameStyleCatalog == null)
            {
                return false;
            }

            ContentVisualResolvedView view;
            if (!ContentVisualResolver.TryResolve(
                    defId,
                    coreCatalog,
                    visualCatalog,
                    frameStyleCatalog,
                    out view))
            {
                return false;
            }

            Apply(actorRoot, view);
            return true;
        }

        public static void Apply(Transform actorRoot, ContentVisualResolvedView view)
        {
            if (actorRoot == null || view == null)
            {
                return;
            }

            TryApplySpriteChild(actorRoot, MainIconChild, view.IconVisualId, view.IconAssetKey);
            TryApplySpriteChild(actorRoot, FaceChild, view.FaceVisualId, view.FaceAssetKey);
            TryApplyFrameColor(actorRoot, view.FrameColor);
        }

        private static void TryApplySpriteChild(
            Transform root,
            string childName,
            string visualId,
            string conventionAssetKey)
        {
            if (string.IsNullOrEmpty(visualId) && string.IsNullOrEmpty(conventionAssetKey))
            {
                return;
            }

            Transform child = FindChildRecursive(root, childName);
            if (child == null)
            {
                return;
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }

            Sprite sprite;
            if (ContentVisualSpriteLoader.TryLoad(visualId, conventionAssetKey, out sprite) && sprite != null)
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
        }

        private static void TryApplyFrameColor(Transform root, ContentColor color)
        {
            Transform child = FindChildRecursive(root, FrameChild);
            if (child == null)
            {
                return;
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.color = new Color(color.R, color.G, color.B, color.A);
            renderer.enabled = true;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName)
            {
                return parent;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                Transform match = FindChildRecursive(parent.GetChild(i), childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
