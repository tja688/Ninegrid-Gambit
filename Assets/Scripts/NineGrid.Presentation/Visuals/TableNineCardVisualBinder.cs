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
            if (coreCatalog == null || visualCatalog == null)
            {
                return false;
            }

            ContentVisualResolvedView view;
            if (!ContentVisualResolver.TryResolve(defId, coreCatalog, visualCatalog, out view))
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

            TryApplyChild(actorRoot, MainIconChild, view.IconKey);
            TryApplyChild(actorRoot, FaceChild, view.FaceKey);
            TryApplyChild(actorRoot, FrameChild, view.FrameKey);
        }

        private static void TryApplyChild(Transform root, string childName, string spriteKey)
        {
            if (string.IsNullOrEmpty(spriteKey))
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
            if (ContentVisualSpriteLoader.TryLoad(spriteKey, out sprite) && sprite != null)
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
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
