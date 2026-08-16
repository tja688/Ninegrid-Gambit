using System;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 炸牌散点场地边界：以 <c>GroundPanel </c>（SpriteRenderer.bounds）为界，
    /// 与 <see cref="NineGrid.Flow.BoardBriefTip.VenueEnvironmentPresenter"/> 同款查找（含无尾随空格回退）。
    /// 面板缺失时回退 9 格锚点包围盒，再回退默认场地矩形——保证炸牌永不依赖宿主位置。
    /// </summary>
    public static class BurstScatterFieldBounds
    {
        public const string GroundPanelObjectName = "GroundPanel ";
        private const string GroundPanelFallbackObjectName = "GroundPanel";
        private const string GroundAnchorsObjectName = "GroundAnchors";

        /// <summary>回退锚点包围盒的外扩（世界单位），略大于格阵本身。</summary>
        private const float AnchorMargin = 0.5f;

        /// <summary>无面板无锚点时的兜底场地矩形（世界单位）。</summary>
        private static readonly Rect FallbackRect = new Rect(-6f, -4.5f, 12f, 10f);

        /// <summary>
        /// 解析炸牌散点世界矩形（已按 <paramref name="padding"/> 内缩）。
        /// 面板 bounds 为权威来源；面板不可用时逐级回退。
        /// </summary>
        public static Rect ResolveWorldRect(float padding)
        {
            var safePadding = Mathf.Max(0f, padding);

            var panelRect = ResolveFromGroundPanel();
            if (panelRect.HasValue)
            {
                return Inset(panelRect.Value, safePadding);
            }

            var anchorRect = ResolveFromGroundAnchors();
            if (anchorRect.HasValue)
            {
                return Inset(anchorRect.Value, safePadding);
            }

            return Inset(FallbackRect, safePadding);
        }

        private static Rect? ResolveFromGroundPanel()
        {
            var panel = FindSceneObjectByName(GroundPanelObjectName);
            if (panel == null)
            {
                panel = FindSceneObjectByName(GroundPanelFallbackObjectName);
            }

            if (panel == null)
            {
                return null;
            }

            var renderer = panel.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
            {
                return null;
            }

            var bounds = renderer.bounds;
            return new Rect(
                bounds.min.x,
                bounds.min.y,
                bounds.size.x,
                bounds.size.y);
        }

        private static Rect? ResolveFromGroundAnchors()
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return null;
            }

            var hasAnchor = false;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var slot = 1; slot <= 9; slot++)
            {
                var anchor = field.GetGroundAnchor(slot);
                if (anchor == null)
                {
                    continue;
                }

                hasAnchor = true;
                minX = Mathf.Min(minX, anchor.position.x);
                maxX = Mathf.Max(maxX, anchor.position.x);
                minY = Mathf.Min(minY, anchor.position.y);
                maxY = Mathf.Max(maxY, anchor.position.y);
            }

            if (!hasAnchor)
            {
                return null;
            }

            return new Rect(
                minX - AnchorMargin,
                minY - AnchorMargin,
                maxX - minX + AnchorMargin * 2f,
                maxY - minY + AnchorMargin * 2f);
        }

        private static Rect Inset(Rect rect, float padding)
        {
            var half = padding;
            if (rect.width <= half * 2f || rect.height <= half * 2f)
            {
                return rect;
            }

            return new Rect(
                rect.x + half,
                rect.y + half,
                rect.width - half * 2f,
                rect.height - half * 2f);
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != objectName)
                {
                    continue;
                }

                if (!t.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (!t.gameObject.activeInHierarchy)
                {
                    continue;
                }

                return t.gameObject;
            }

            return null;
        }
    }
}
