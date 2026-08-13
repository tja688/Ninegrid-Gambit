using System;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 世界空间叠层面板按钮：BoxCollider2D + PointerHitRegistry 命中，悬停缩放 + 点击回调。
    /// 与 PlayerAudioSettingsPanel 的命中范式同构，供人物选择 / 结算面板运行时接线复用。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WorldUiHitButton : MonoBehaviour, IPointerHitTarget
    {
        private BoxCollider2D mCollider;
        private Action mOnClick;
        private Action mOnHoverEnter;
        private Action mOnHoverExit;
        private int mHitSort = BattleUiDimmerOverlay.CloseHitSort;
        private int mTypePriority = PointerHitSurfacePriorities.Overlay;
        private float mHoverScale = 1f;
        private Vector3 mBaseScale = Vector3.one;
        private bool mBaseScaleCaptured;

        public Collider2D HitCollider =>
            mCollider != null ? mCollider : (mCollider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => mHitSort;

        public int HitTypePriority => mTypePriority;

        public void Configure(
            Action onClick,
            int hitSort,
            int typePriority,
            float hoverScale = 1f,
            Action onHoverEnter = null,
            Action onHoverExit = null)
        {
            mOnClick = onClick;
            mHitSort = hitSort;
            mTypePriority = typePriority;
            mHoverScale = Mathf.Max(0.5f, hoverScale);
            mOnHoverEnter = onHoverEnter;
            mOnHoverExit = onHoverExit;
        }

        private void Awake()
        {
            mCollider = GetComponent<BoxCollider2D>();
            if (mCollider != null)
            {
                mCollider.isTrigger = false;
            }
        }

        private void OnEnable()
        {
            CaptureBaseScaleIfNeeded();
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
            RestoreBaseScale();
        }

        public void HandlePointerEnter()
        {
            CaptureBaseScaleIfNeeded();
            if (mHoverScale > 1.001f)
            {
                transform.localScale = mBaseScale * mHoverScale;
            }

            mOnHoverEnter?.Invoke();
        }

        public void HandlePointerExit()
        {
            RestoreBaseScale();
            mOnHoverExit?.Invoke();
        }

        public void HandlePointerDown()
        {
            mOnClick?.Invoke();
        }

        private void CaptureBaseScaleIfNeeded()
        {
            if (mBaseScaleCaptured)
            {
                return;
            }

            mBaseScale = transform.localScale;
            mBaseScaleCaptured = true;
        }

        private void RestoreBaseScale()
        {
            if (mBaseScaleCaptured)
            {
                transform.localScale = mBaseScale;
            }
        }
    }
}
