using NineGrid.Cards;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 战斗信息预览槽：图标复用卡面 <c>mainVisual</c>，再叠分类外部缩放。
    /// 暂不接入 <see cref="PointerHitRegistry"/>（槽 collider / 黄边悬停待动态框选系统）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BattleInfoPreviewSlotView : MonoBehaviour
    {
        private const int VisualSortBoost = BattleUiDimmerOverlay.HitSort + 3;
        private const string ArtChildName = "__Art";
        private const float FallbackSlotSize = 0.8f;

        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private BattleInfoPreviewIconPlayer iconPlayer;
        [SerializeField] private BattleInfoPreviewHighlight highlight;
        [SerializeField] private BoxCollider2D hitCollider;
        [SerializeField] private Vector2 slotLocalSize;
        [SerializeField] private Sprite frameSprite;
        [SerializeField] private string defId;
        [SerializeField] private CardPresentationKind kindHint = CardPresentationKind.Unknown;
        [SerializeField] private float externalScale = 1f;

        private bool _slotSizeCaptured;

        public string DefId => defId ?? string.Empty;

        public CardPresentationKind KindHint => kindHint;

        public bool HasContent => !string.IsNullOrEmpty(DefId);

        public Vector2 SlotLocalSize =>
            slotLocalSize.x > 0.0001f && slotLocalSize.y > 0.0001f
                ? slotLocalSize
                : new Vector2(FallbackSlotSize, FallbackSlotSize);

        public Sprite FrameSprite => frameSprite;

        public void EnsureWired(Color highlightColor, Sprite highlightSprite = null)
        {
            EnsureArtHierarchy();

            if (iconRenderer != null && iconRenderer.sortingOrder < VisualSortBoost)
            {
                iconRenderer.sortingOrder = VisualSortBoost;
            }

            if (iconPlayer == null)
            {
                iconPlayer = GetComponent<BattleInfoPreviewIconPlayer>();
                if (iconPlayer == null)
                {
                    iconPlayer = gameObject.AddComponent<BattleInfoPreviewIconPlayer>();
                }
            }

            iconPlayer.BindTarget(iconRenderer, this);

            if (highlight == null)
            {
                highlight = GetComponent<BattleInfoPreviewHighlight>();
                if (highlight == null)
                {
                    highlight = gameObject.AddComponent<BattleInfoPreviewHighlight>();
                }
            }

            highlight.Configure(highlightColor, highlightSprite);
            highlight.BindTarget(iconRenderer, SlotLocalSize);
            highlight.Hide();

            DisableLegacyHitCollider();
        }

        public void Bind(string contentDefId, CardPresentationKind kind, float iconExternalScale = 1f)
        {
            defId = contentDefId ?? string.Empty;
            kindHint = kind;
            externalScale = iconExternalScale > 0.0001f ? iconExternalScale : 1f;
            if (string.IsNullOrEmpty(defId))
            {
                Clear();
                return;
            }

            if (iconPlayer == null)
            {
                EnsureWired(new Color(1f, 0.92f, 0.2f, 0.95f));
            }

            iconPlayer.PlayIdleOrStatic(defId, externalScale);
            DisableLegacyHitCollider();
            highlight?.Hide();
        }

        public void Clear()
        {
            defId = string.Empty;
            kindHint = CardPresentationKind.Unknown;
            externalScale = 1f;
            iconPlayer?.ClearVisual();
            highlight?.Hide();
            DisableLegacyHitCollider();
        }

        /// <summary>换图后由 IconPlayer 调用：复用卡面 mainVisual，再乘外部缩放。</summary>
        public void ApplyMainVisual(
            CardPresentationMainVisualDto mainVisual,
            float slotOffsetX,
            float slotOffsetY,
            float iconExternalScale)
        {
            EnsureArtHierarchy();
            if (iconRenderer == null || iconRenderer.sprite == null)
            {
                BattleInfoSlotArtFit.ResetArtTransform(iconRenderer);
                return;
            }

            BattleInfoSlotArtFit.ApplyMainVisual(
                iconRenderer,
                SlotLocalSize,
                mainVisual,
                slotOffsetX,
                slotOffsetY,
                iconExternalScale);
            DisableLegacyHitCollider();
        }

        private void OnDisable()
        {
            highlight?.Hide();
        }

        /// <summary>兼容旧调用点：槽位已不再注册 PointerHit。</summary>
        public void RefreshHitRegistration()
        {
            DisableLegacyHitCollider();
        }

        private void EnsureArtHierarchy()
        {
            CaptureSlotSizeIfNeeded();

            var art = transform.Find(ArtChildName);
            if (art == null)
            {
                var go = new GameObject(ArtChildName);
                art = go.transform;
                art.SetParent(transform, false);
                art.localPosition = Vector3.zero;
                art.localRotation = Quaternion.identity;
                art.localScale = Vector3.one;
            }

            iconRenderer = art.GetComponent<SpriteRenderer>();
            if (iconRenderer == null)
            {
                iconRenderer = art.gameObject.AddComponent<SpriteRenderer>();
            }

            // 根上原占位 SpriteRenderer 只用于量尺寸 / 高亮框，不再当图标。
            var rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null && rootSr != iconRenderer)
            {
                if (rootSr.sortingOrder >= VisualSortBoost)
                {
                    iconRenderer.sortingOrder = rootSr.sortingOrder;
                    iconRenderer.sortingLayerID = rootSr.sortingLayerID;
                }
                else if (iconRenderer.sortingOrder < VisualSortBoost)
                {
                    iconRenderer.sortingLayerID = rootSr.sortingLayerID;
                    iconRenderer.sortingOrder = VisualSortBoost;
                }

                rootSr.enabled = false;
                rootSr.sprite = null;
            }

            if (iconRenderer.sortingOrder < VisualSortBoost)
            {
                iconRenderer.sortingOrder = VisualSortBoost;
            }

            // 不再 Cover + SpriteMask 裁切；完整显示卡面调好的图标。
            iconRenderer.maskInteraction = SpriteMaskInteraction.None;

            var leftoverMask = GetComponent<SpriteMask>();
            if (leftoverMask != null)
            {
                leftoverMask.enabled = false;
            }
        }

        private void CaptureSlotSizeIfNeeded()
        {
            if (_slotSizeCaptured && slotLocalSize.x > 0.0001f && slotLocalSize.y > 0.0001f)
            {
                return;
            }

            var rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null && rootSr.sprite != null)
            {
                frameSprite = rootSr.sprite;
                slotLocalSize = rootSr.sprite.bounds.size;
                _slotSizeCaptured = true;
                return;
            }

            if (frameSprite != null)
            {
                slotLocalSize = frameSprite.bounds.size;
                _slotSizeCaptured = true;
                return;
            }

            if (slotLocalSize.x < 0.0001f || slotLocalSize.y < 0.0001f)
            {
                slotLocalSize = new Vector2(FallbackSlotSize, FallbackSlotSize);
            }

            _slotSizeCaptured = true;
        }

        private void DisableLegacyHitCollider()
        {
            if (hitCollider == null)
            {
                hitCollider = GetComponent<BoxCollider2D>();
            }

            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }
        }
    }
}
