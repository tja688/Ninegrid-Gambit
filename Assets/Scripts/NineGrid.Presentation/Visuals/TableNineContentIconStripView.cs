using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 遗物/技能 HUD 条：按 content defId 列表展示图标精灵。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineContentIconStripView : MonoBehaviour
    {
        [SerializeField] private Transform iconRoot;
        [SerializeField, Min(0f)] private float iconSpacing = 0.55f;
        [SerializeField] private Vector3 iconLocalScale = Vector3.one;
        [SerializeField] private int sortingOrder = 1;

        private readonly List<SpriteRenderer> mIconRenderers = new();

        public void ApplyDefIds(IArchitecture architecture, IReadOnlyList<string> defIds)
        {
            if (iconRoot == null)
            {
                iconRoot = transform;
            }

            int count = defIds?.Count ?? 0;
            EnsureRendererCount(count);

            if (count == 0 || architecture == null || !ContentCatalogRuntimeBootstrap.EnsureLoaded(architecture))
            {
                HideAllIcons();
                return;
            }

            var coreCatalog = architecture.GetSystem<IContentSystem>().Catalog;
            var visualCatalog = ContentCatalogRuntimeBootstrap.VisualCatalog;
            var frameStyleCatalog = ContentCatalogRuntimeBootstrap.FrameStyleCatalog;
            if (coreCatalog == null || visualCatalog == null || frameStyleCatalog == null)
            {
                HideAllIcons();
                return;
            }

            float startX = count <= 1 ? 0f : -(count - 1) * iconSpacing * 0.5f;
            for (var i = 0; i < count; i++)
            {
                SpriteRenderer renderer = mIconRenderers[i];
                renderer.gameObject.SetActive(true);
                renderer.transform.localPosition = new Vector3(startX + i * iconSpacing, 0f, 0f);

                string defId = defIds[i];
                if (ContentVisualResolver.TryResolve(
                        defId,
                        coreCatalog,
                        visualCatalog,
                        frameStyleCatalog,
                        out ContentVisualResolvedView view)
                    && ContentVisualSpriteLoader.TryLoad(view.IconVisualId, view.IconAssetKey, out Sprite sprite))
                {
                    renderer.sprite = sprite;
                }
                else
                {
                    renderer.sprite = null;
                }
            }
        }

        private void EnsureRendererCount(int count)
        {
            while (mIconRenderers.Count < count)
            {
                mIconRenderers.Add(CreateIconRenderer(mIconRenderers.Count));
            }
        }

        private SpriteRenderer CreateIconRenderer(int index)
        {
            var iconObject = new GameObject($"Icon_{index}");
            iconObject.transform.SetParent(iconRoot, false);
            iconObject.transform.localScale = iconLocalScale;

            var renderer = iconObject.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void HideAllIcons()
        {
            for (var i = 0; i < mIconRenderers.Count; i++)
            {
                if (mIconRenderers[i] != null)
                {
                    mIconRenderers[i].gameObject.SetActive(false);
                }
            }
        }

        private void Reset()
        {
            if (iconRoot == null)
            {
                iconRoot = transform;
            }
        }
    }
}
