using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 右键详情 ScrollView Content：把装配词条写入统一描述框（ADR-0037）。
    /// mesh TMP 不受 uGUI Mask 裁切，须把 Viewport SpriteMask 缩到视口并配合动态 margin。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectGlossaryListView : MonoBehaviour
    {
        private const string ViewportClipMaskName = "__GlossaryViewportClipMask";
        private const float ViewportInsetCanvasUnits = 10f;

        [SerializeField] private RectTransform content;
        [SerializeField] private CardInspectGlossaryRowView rowPrefab;
        [SerializeField] private Color defaultBodyColor = new Color(0.816f, 0.816f, 0.816f, 0.875f);

        private readonly List<CardInspectGlossaryRowView> _spawned = new List<CardInspectGlossaryRowView>();
        private static Sprite s_viewportClipSprite;

        /// <summary>
        /// 词条行正文默认色（浅色底面板设为黑色，深色底面板设为浅白）。
        /// 优先使用场景/预制体序列化的颜色；亦支持运行时按需覆盖。
        /// </summary>
        public void SetDefaultBodyColor(Color color)
        {
            defaultBodyColor = color;
        }

        private void ApplyDefaultBodyColor(CardInspectGlossaryRowView row)
        {
            if (row != null)
            {
                row.SetDefaultBodyColor(defaultBodyColor);
            }
        }

        public void Configure(RectTransform contentRoot, CardInspectGlossaryRowView prefab)
        {
            content = contentRoot;
            rowPrefab = prefab;
            EnsureLayout();
            EnsureViewportClipMask();
        }

        public void BindExplicitTerms(IReadOnlyList<CardGlossaryTerms.ResolvedTerm> terms)
        {
            EnsureLayout();
            HideLegacySceneTemplates();
            EnsureViewportClipMask();
            ClearSpawned();

            if (terms == null || terms.Count == 0 || rowPrefab == null || content == null)
            {
                return;
            }

            var box = Instantiate(rowPrefab, content);
            box.gameObject.SetActive(true);
            box.name = "词条描述框";
            StretchBoxWidth(box);
            box.EnsureLayoutElement();
            ApplyDefaultBodyColor(box);
            box.BindTerms(terms);
            box.transform.SetAsLastSibling();
            ApplyGlossaryRowMaskInteraction(box.gameObject, GetScrollViewport());
            _spawned.Add(box);
            RebuildContentLayout(box);
            EnsureViewportClipMask();
            ApplyCurrentClip();
        }

        public void ClearAll()
        {
            ClearSpawned();
        }

        private void ClearSpawned()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                var row = _spawned[i];
                if (row != null)
                {
                    Destroy(row.gameObject);
                }
            }

            _spawned.Clear();
        }

        private void EnsureLayout()
        {
            if (content == null)
            {
                content = transform as RectTransform;
            }

            if (content == null)
            {
                return;
            }

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(4, 4, 4, 4);
                vlg.spacing = 8f;
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
            }

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void HideLegacySceneTemplates()
        {
            if (content == null)
            {
                return;
            }

            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child == null || !child.name.Contains("详细效果信息"))
                {
                    continue;
                }

                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                }

                var ignore = child.GetComponent<LayoutElement>();
                if (ignore == null)
                {
                    ignore = child.gameObject.AddComponent<LayoutElement>();
                }

                ignore.ignoreLayout = true;
            }
        }

        private static void StretchBoxWidth(CardInspectGlossaryRowView box)
        {
            var rt = box != null ? box.transform as RectTransform : null;
            if (rt == null)
            {
                return;
            }

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }

        private void RebuildContentLayout(CardInspectGlossaryRowView box)
        {
            if (content == null || box == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            box.RefreshPreferredHeight();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void ApplyCurrentClip()
        {
            if (content == null)
            {
                return;
            }

            var scroll = content.GetComponentInParent<ScrollRect>();
            var clip = scroll != null && scroll.viewport != null
                ? scroll.viewport.GetComponent<CardInspectGlossaryViewportClip>()
                : null;
            clip?.ApplyClip();
        }

        private void EnsureViewportClipMask()
        {
            if (content == null)
            {
                return;
            }

            var scroll = content.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null)
            {
                return;
            }

            ApplyViewportInsetIfNeeded(scroll.viewport);
            EnsureViewportSpriteMask(scroll.viewport);
            SyncViewportMaskToRect(scroll.viewport);
            EnsureViewportClipDriver(scroll);
        }

        private static void EnsureViewportClipDriver(ScrollRect scroll)
        {
            if (scroll?.viewport == null)
            {
                return;
            }

            var clip = scroll.viewport.GetComponent<CardInspectGlossaryViewportClip>();
            if (clip == null)
            {
                clip = scroll.viewport.gameObject.AddComponent<CardInspectGlossaryViewportClip>();
            }

            clip.Configure(scroll);
        }

        private RectTransform GetScrollViewport()
        {
            if (content == null)
            {
                return null;
            }

            var scroll = content.GetComponentInParent<ScrollRect>();
            return scroll != null ? scroll.viewport : null;
        }

        private static void ApplyViewportInsetIfNeeded(RectTransform viewport)
        {
            if (viewport == null)
            {
                return;
            }

            if (viewport.GetComponent<CardInspectGlossaryViewportInset>() != null)
            {
                return;
            }

            viewport.gameObject.AddComponent<CardInspectGlossaryViewportInset>();
            viewport.offsetMin += new Vector2(ViewportInsetCanvasUnits, ViewportInsetCanvasUnits);
            viewport.offsetMax -= new Vector2(ViewportInsetCanvasUnits, ViewportInsetCanvasUnits);
        }

        private static void EnsureViewportSpriteMask(RectTransform viewport)
        {
            if (viewport == null)
            {
                return;
            }

            var maskTransform = viewport.Find(ViewportClipMaskName) as RectTransform;
            if (maskTransform == null)
            {
                var go = new GameObject(ViewportClipMaskName);
                maskTransform = go.AddComponent<RectTransform>();
                maskTransform.SetParent(viewport, false);
                maskTransform.anchorMin = Vector2.zero;
                maskTransform.anchorMax = Vector2.one;
                maskTransform.pivot = new Vector2(0.5f, 0.5f);
                maskTransform.offsetMin = Vector2.zero;
                maskTransform.offsetMax = Vector2.zero;
                maskTransform.localRotation = Quaternion.identity;
                maskTransform.localScale = Vector3.one;

                var mask = go.AddComponent<SpriteMask>();
                mask.sprite = GetViewportClipSprite();
                mask.alphaCutoff = 0.01f;
                mask.enabled = true;
            }

            SyncViewportMaskSorting(maskTransform, viewport);
            SyncViewportMaskToRect(viewport);
        }

        internal static void SyncViewportMaskToRect(RectTransform viewport)
        {
            if (viewport == null)
            {
                return;
            }

            var maskTransform = viewport.Find(ViewportClipMaskName) as RectTransform;
            if (maskTransform == null)
            {
                return;
            }

            var mask = maskTransform.GetComponent<SpriteMask>();
            if (mask == null || mask.sprite == null)
            {
                return;
            }

            var spriteSize = mask.sprite.bounds.size;
            var rect = viewport.rect;
            maskTransform.anchorMin = new Vector2(0.5f, 0.5f);
            maskTransform.anchorMax = new Vector2(0.5f, 0.5f);
            maskTransform.pivot = new Vector2(0.5f, 0.5f);
            maskTransform.anchoredPosition = Vector2.zero;
            maskTransform.localRotation = Quaternion.identity;
            maskTransform.localScale = new Vector3(
                Mathf.Max(0.0001f, rect.width / Mathf.Max(0.0001f, spriteSize.x)),
                Mathf.Max(0.0001f, rect.height / Mathf.Max(0.0001f, spriteSize.y)),
                1f);
        }

        private static void SyncViewportMaskSorting(RectTransform maskTransform, RectTransform viewport)
        {
            var mask = maskTransform.GetComponent<SpriteMask>();
            if (mask == null)
            {
                return;
            }

            var canvas = viewport.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var layerId = canvas.sortingLayerID;
            var order = canvas.sortingOrder;
            mask.sortingLayerID = layerId;
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = layerId;
            mask.backSortingLayerID = layerId;
            mask.frontSortingOrder = order + 30;
            mask.backSortingOrder = order - 1;
            mask.enabled = true;
        }

        private static Sprite GetViewportClipSprite()
        {
            if (s_viewportClipSprite != null)
            {
                return s_viewportClipSprite;
            }

            s_viewportClipSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 4, 4),
                new Vector2(0.5f, 0.5f),
                4f);
            return s_viewportClipSprite;
        }

        private static void ApplyGlossaryRowMaskInteraction(GameObject rowRoot, RectTransform viewport)
        {
            if (rowRoot == null)
            {
                return;
            }

            var canvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;

            var renderers = rowRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SpriteRenderer spriteRenderer)
                {
                    spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
            }

            var tmp = rowRoot.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.overflowMode = TextOverflowModes.Masking;
                var meshRenderer = tmp.GetComponent<MeshRenderer>();
                if (meshRenderer != null && canvas != null)
                {
                    meshRenderer.sortingLayerID = canvas.sortingLayerID;
                    meshRenderer.sortingOrder = canvas.sortingOrder + 2;
                }
            }
        }
    }

    /// <summary>标记 ScrollView Viewport 已应用详述词条区内缩，避免重复叠加。</summary>
    [DisallowMultipleComponent]
    internal sealed class CardInspectGlossaryViewportInset : MonoBehaviour
    {
    }

    /// <summary>ScrollRect 滚动时按 Viewport 重算 mesh TMP margin 裁切。</summary>
    [DisallowMultipleComponent]
    internal sealed class CardInspectGlossaryViewportClip : MonoBehaviour
    {
        private ScrollRect _scroll;
        private bool _configured;

        public void Configure(ScrollRect scroll)
        {
            _scroll = scroll;
            _configured = scroll != null && scroll.content != null && scroll.viewport != null;
            ApplyClip();
        }

        private void LateUpdate()
        {
            if (!_configured)
            {
                return;
            }

            ApplyClip();
        }

        public void ApplyClip()
        {
            var viewport = _scroll?.viewport;
            var content = _scroll?.content;
            if (viewport == null || content == null)
            {
                return;
            }

            CardInspectGlossaryListView.SyncViewportMaskToRect(viewport);

            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var row = child.GetComponent<CardInspectGlossaryRowView>();
                if (row != null)
                {
                    row.ApplyViewportClip(viewport);
                }
            }
        }
    }
}
