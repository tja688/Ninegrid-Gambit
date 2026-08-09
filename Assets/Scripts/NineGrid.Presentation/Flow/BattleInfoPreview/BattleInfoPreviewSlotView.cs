using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow.BattleInfoPreview
{
    /// <summary>
    /// 战斗信息预览槽：图标 idle + 命中代理 + 黄边悬停。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BattleInfoPreviewSlotView : MonoBehaviour, IPointerHitTarget
    {
        private const int HitSortBoost = BattleUiDimmerOverlay.HitSort + 3;

        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private BattleInfoPreviewIconPlayer iconPlayer;
        [SerializeField] private BattleInfoPreviewHighlight highlight;
        [SerializeField] private BoxCollider2D hitCollider;
        [SerializeField] private string defId;
        [SerializeField] private CardPresentationKind kindHint = CardPresentationKind.Unknown;

        public string DefId => defId ?? string.Empty;

        public CardPresentationKind KindHint => kindHint;

        public bool HasContent => !string.IsNullOrEmpty(DefId);

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
            if (iconRenderer == null)
            {
                iconRenderer = GetComponent<SpriteRenderer>();
            }

            if (iconRenderer != null && iconRenderer.sortingOrder < HitSortBoost)
            {
                iconRenderer.sortingOrder = HitSortBoost;
            }

            if (iconPlayer == null)
            {
                iconPlayer = GetComponent<BattleInfoPreviewIconPlayer>();
                if (iconPlayer == null)
                {
                    iconPlayer = gameObject.AddComponent<BattleInfoPreviewIconPlayer>();
                }
            }

            if (highlight == null)
            {
                highlight = GetComponent<BattleInfoPreviewHighlight>();
                if (highlight == null)
                {
                    highlight = gameObject.AddComponent<BattleInfoPreviewHighlight>();
                }
            }

            highlight.Configure(highlightColor, highlightSprite);
            highlight.BindIcon(iconRenderer);

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
            SyncColliderToSprite();
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

        private void EnsureCollider()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            if (hitCollider == null)
            {
                hitCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            hitCollider.isTrigger = true;
            SyncColliderToSprite();
        }

        private void SyncColliderToSprite()
        {
            if (hitCollider == null)
            {
                return;
            }

            if (iconRenderer != null && iconRenderer.sprite != null)
            {
                hitCollider.size = iconRenderer.sprite.bounds.size;
                hitCollider.offset = iconRenderer.sprite.bounds.center;
                hitCollider.enabled = true;
            }
            else
            {
                hitCollider.size = new Vector2(0.8f, 0.8f);
                hitCollider.offset = Vector2.zero;
                hitCollider.enabled = HasContent;
            }
        }
    }
}
