using System;
using NineGrid.Flow.BoardBriefTip;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow.ShopBoard
{
    /// <summary>
    /// 商店货架 / 刷新：悬停简要文案 + 任意距离点击（ADR-0020）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ShopBoardHitProxy : MonoBehaviour, IPointerHitTarget
    {
        // 高于底盘 GroundCardHitProxy(30)，否则同 GO 上悬停/点击被场地卡代理抢走。
        private const int TypePriority = 40;

        private BoxCollider2D mCollider;
        private int mHoverGeneration;
        private ShopBoardHitKind mKind;
        private int mShelfIndex;
        private string mTip = string.Empty;
        private Action<ShopBoardHitKind, int> mOnHit;

        public Collider2D HitCollider =>
            mCollider != null ? mCollider : (mCollider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => ResolveHitSortOrder();

        public int HitTypePriority => TypePriority;

        public void Configure(
            ShopBoardHitKind kind,
            int shelfIndex,
            string tip,
            Action<ShopBoardHitKind, int> onHit,
            Vector2? colliderSize = null)
        {
            mKind = kind;
            mShelfIndex = shelfIndex;
            mTip = tip ?? string.Empty;
            mOnHit = onHit;
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
            if (string.IsNullOrEmpty(mTip))
            {
                return;
            }

            var presenter = BoardBriefTipPresenter.EnsureExists();
            mHoverGeneration = presenter.ShowHover(mTip);
        }

        public void HandlePointerExit()
        {
            ClearTipIfHovering();
        }

        public void HandlePointerDown()
        {
            mOnHit?.Invoke(mKind, mShelfIndex);
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
