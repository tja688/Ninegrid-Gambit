using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 战斗信息预览槽：固定框 + SpriteMask 裁切 + Cover 图标 + 命中代理 + 黄边悬停。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BattleInfoPreviewSlotView : MonoBehaviour, IPointerHitTarget
    {
        private const int HitSortBoost = BattleUiDimmerOverlay.HitSort + 3;
        private const string ArtChildName = "__Art";
        private const float FallbackSlotSize = 0.8f;

        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private BattleInfoPreviewIconPlayer iconPlayer;
        [SerializeField] private BattleInfoPreviewHighlight highlight;
        [SerializeField] private BoxCollider2D hitCollider;
        [SerializeField] private SpriteMask slotMask;
        [SerializeField] private Vector2 slotLocalSize;
        [SerializeField] private Sprite frameSprite;
        [SerializeField] private string defId;
        [SerializeField] private CardPresentationKind kindHint = CardPresentationKind.Unknown;

        private bool _slotSizeCaptured;

        public string DefId => defId ?? string.Empty;

        public CardPresentationKind KindHint => kindHint;

        public bool HasContent => !string.IsNullOrEmpty(DefId);

        public Vector2 SlotLocalSize =>
            slotLocalSize.x > 0.0001f && slotLocalSize.y > 0.0001f
                ? slotLocalSize
                : new Vector2(FallbackSlotSize, FallbackSlotSize);

        public Sprite FrameSprite => frameSprite;

        public Collider2D HitCollider =>
            hitCollider != null ? hitCollider : (hitCollider = GetComponent<BoxCollider2D>());

        public int HitSortOrder
        {
            get
            {
                var renderer = iconRenderer != null ? iconRenderer : GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    return Mathf.Max(HitSortBoost, renderer.sortingOrder);
                }

                return HitSortBoost;
            }
        }

        public int HitTypePriority => PointerHitSurfacePriorities.Overlay;

        public void EnsureWired(Color highlightColor, Sprite highlightSprite = null)
        {
            EnsureArtHierarchy();
            EnsureSlotMask();

            if (iconRenderer != null && iconRenderer.sortingOrder < HitSortBoost)
            {
                iconRenderer.sortingOrder = HitSortBoost;
            }

            SyncMaskSortingRange();

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
            highlight.BindSlotFrame(frameSprite, SlotLocalSize, iconRenderer);

            EnsureCollider();
        }

        public void Bind(string contentDefId, CardPresentationKind kind)
        {
            defId = contentDefId ?? string.Empty;
            kindHint = kind;
            if (string.IsNullOrEmpty(defId))
            {
                Clear();
                return;
            }

            if (iconPlayer == null)
            {
                EnsureWired(new Color(1f, 0.92f, 0.2f, 0.95f));
            }

            iconPlayer.PlayIdleOrStatic(defId);
            SyncColliderToSlot();
            highlight?.Hide();
        }

        public void Clear()
        {
            defId = string.Empty;
            kindHint = CardPresentationKind.Unknown;
            iconPlayer?.ClearVisual();
            highlight?.Hide();
            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }
        }

        /// <summary>换图后由 IconPlayer 调用：按槽尺寸 Cover，可选 JSON 构图覆盖。</summary>
        public void ApplyArtFit(CardPresentationBattleInfoSlotDisplayDto display)
        {
            EnsureArtHierarchy();
            if (iconRenderer == null || iconRenderer.sprite == null)
            {
                BattleInfoSlotArtFit.ResetArtTransform(iconRenderer);
                return;
            }

            BattleInfoSlotArtFit.ApplyCover(iconRenderer, SlotLocalSize, display);
            SyncMaskSortingRange();
            SyncColliderToSlot();
        }

        private void OnEnable()
        {
            if (HasContent)
            {
                PointerHitRegistry.Register(this);
            }
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
            highlight?.Hide();
        }

        public void HandlePointerEnter()
        {
            if (HasContent)
            {
                highlight?.Show();
            }
        }

        public void HandlePointerExit()
        {
            highlight?.Hide();
        }

        public void HandlePointerDown()
        {
            // 左键：面板内不关预览；详述由右键路径打开。
        }

        public void RefreshHitRegistration()
        {
            PointerHitRegistry.Unregister(this);
            if (isActiveAndEnabled && HasContent)
            {
                PointerHitRegistry.Register(this);
                if (hitCollider != null)
                {
                    hitCollider.enabled = true;
                }
            }
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

            // 根上原占位 SpriteRenderer 只用于量尺寸 / 作 Mask 形状，不再当图标。
            var rootSr = GetComponent<SpriteRenderer>();
            if (rootSr != null && rootSr != iconRenderer)
            {
                if (rootSr.sortingOrder >= HitSortBoost)
                {
                    iconRenderer.sortingOrder = rootSr.sortingOrder;
                    iconRenderer.sortingLayerID = rootSr.sortingLayerID;
                }
                else if (iconRenderer.sortingOrder < HitSortBoost)
                {
                    iconRenderer.sortingLayerID = rootSr.sortingLayerID;
                    iconRenderer.sortingOrder = HitSortBoost;
                }

                rootSr.enabled = false;
                rootSr.sprite = null;
            }

            if (iconRenderer.sortingOrder < HitSortBoost)
            {
                iconRenderer.sortingOrder = HitSortBoost;
            }

            iconRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
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

        private void EnsureSlotMask()
        {
            CaptureSlotSizeIfNeeded();

            if (slotMask == null)
            {
                slotMask = GetComponent<SpriteMask>();
            }

            if (slotMask == null)
            {
                slotMask = gameObject.AddComponent<SpriteMask>();
            }

            if (slotMask.sprite == null && frameSprite != null)
            {
                slotMask.sprite = frameSprite;
            }

            slotMask.enabled = slotMask.sprite != null;
            slotMask.alphaCutoff = 0.01f;
        }

        private void SyncMaskSortingRange()
        {
            if (slotMask == null || iconRenderer == null || !slotMask.enabled)
            {
                return;
            }

            CardMainVisualMaskAnchor.ApplyMaskSortingRange(slotMask, iconRenderer, -2, 2);
        }

        private void EnsureCollider()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            if (hitCollider == null)
            {
                hitCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            hitCollider.isTrigger = true;
            SyncColliderToSlot();
        }

        private void SyncColliderToSlot()
        {
            if (hitCollider == null)
            {
                return;
            }

            var size = SlotLocalSize;
            hitCollider.size = size;
            hitCollider.offset = Vector2.zero;
            hitCollider.enabled = HasContent;
        }
    }
}
