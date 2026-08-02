using NineGrid.Flow;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 非战斗场地对象悬停 → 简要解释文字框。战斗真卡不挂本代理（右键详述另责）。
    /// M1 挂场地图标；就地选项卡 / 商店货架由 M2 Spawn 时复用本代理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BoardBriefTipHitProxy : MonoBehaviour, IPointerHitTarget
    {
        private const int TypePriority = 25;

        [SerializeField] private string tipText = string.Empty;

        private BoxCollider2D mCollider;
        private int mHoverGeneration;

        public string TipText
        {
            get => tipText;
            set => tipText = value ?? string.Empty;
        }

        public Collider2D HitCollider =>
            mCollider != null ? mCollider : (mCollider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => ResolveHitSortOrder();

        public int HitTypePriority => TypePriority;

        public void Configure(string tip, Vector2? colliderSize = null)
        {
            TipText = tip;
            EnsureCollider(colliderSize);
        }

        private void Awake()
        {
            EnsureCollider(null);
        }

        private void OnEnable()
        {
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
            ClearTipIfHovering();
        }

        public void HandlePointerEnter()
        {
            if (string.IsNullOrEmpty(tipText))
            {
                return;
            }

            var presenter = BoardBriefTipPresenter.EnsureExists();
            mHoverGeneration = presenter.ShowHover(tipText);
        }

        public void HandlePointerExit()
        {
            ClearTipIfHovering();
        }

        public void HandlePointerDown()
        {
            // 场地图标走驻留提交；就地选项/货架点击由 M2 另挂。
        }

        private void ClearTipIfHovering()
        {
            if (mHoverGeneration <= 0)
            {
                return;
            }

            var presenter = BoardBriefTipPresenter.InstanceOrNull();
            if (presenter != null)
            {
                presenter.ClearHover(mHoverGeneration);
            }

            mHoverGeneration = 0;
        }

        private void EnsureCollider(Vector2? size)
        {
            mCollider = GetComponent<BoxCollider2D>();
            if (mCollider == null)
            {
                mCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            mCollider.isTrigger = true;
            if (size.HasValue)
            {
                mCollider.size = size.Value;
                return;
            }

            if (mCollider.size.sqrMagnitude > 0.01f)
            {
                return;
            }

            var renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null && renderer.sprite != null)
            {
                mCollider.size = renderer.sprite.bounds.size;
            }
            else
            {
                mCollider.size = new Vector2(1.2f, 1.2f);
            }
        }

        private int ResolveHitSortOrder()
        {
            var group = GetComponent<SortingGroup>();
            if (group != null)
            {
                return group.sortingOrder;
            }

            var renderer = GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sortingOrder : 0;
        }
    }
}
