using System;
using NineGrid.Flow.BoardBriefTip;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow.TavernBoard
{
    /// <summary>
    /// 卡店服务 / 刷新 / 二级候选：悬停简要文案 + 任意距离点击（#93 / ADR-0020）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TavernBoardHitProxy : MonoBehaviour, IPointerHitTarget
    {
        // 高于底盘 GroundCardHitProxy(30)，否则同 GO 上悬停/点击被场地卡代理抢走。
        private const int TypePriority = 40;

        private BoxCollider2D mCollider;
        private int mHoverGeneration;
        private TavernBoardHitKind mKind;
        private int mOptionIndex;
        private string mTip = string.Empty;
        private Action<TavernBoardHitKind, int> mOnHit;

        public Collider2D HitCollider =>
            mCollider != null ? mCollider : (mCollider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => ResolveHitSortOrder();

        public int HitTypePriority => TypePriority;

        public void Configure(
            TavernBoardHitKind kind,
            int optionIndex,
            string tip,
            Action<TavernBoardHitKind, int> onHit,
            Vector2? colliderSize = null)
        {
            mKind = kind;
            mOptionIndex = optionIndex;
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
            mOnHit?.Invoke(mKind, mOptionIndex);
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

    public enum TavernBoardHitKind
    {
        SelectService,
        Refresh,
        SelectFixCandidate
    }
}
