using DG.Tweening;
using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 金币 HUD 域宿主（#203）：向 gold-flight 播放器提供父级、起终点转换、排序边界与图标吞噬/复位反馈。
    /// 由 <see cref="PlayerInfoHudPresenter"/> 在绑定 HUD 后经 <see cref="Install"/> 显式安装；
    /// 播放器经 <see cref="Instance"/> 使用，不自行搜索或任意修改场景。
    /// </summary>
    public interface IGoldHudDomainHost : IVfxDomainHost
    {
        /// <summary>金币图标当前世界位（终点）。</summary>
        Vector3 ResolveSinkWorldPosition();

        /// <summary>缺省出生点（屏幕中心，只读 Camera.main）。</summary>
        Vector3 ResolveDefaultOriginWorld();

        /// <summary>金币图标当前 Sprite（飞币视觉的兜底资源）。</summary>
        bool TryGetCoinSprite(out Sprite sprite);

        /// <summary>每吞一枚调用：叠加图标缩放并安排窗口后回缩到基础值。</summary>
        void PunchIcon();

        /// <summary>扣减/首刷瞬时对齐：立即复位图标缩放。</summary>
        void SnapIconToBase();

        /// <summary>窗口后平滑回缩，OnComplete 精确恢复基础值。</summary>
        void SettleIconToBase();
    }

    public sealed class GoldHudDomainHost : IGoldHudDomainHost
    {
        private const float PunchPerCoin = 0.12f;
        private const float MaxPunchScale = 1.85f;
        private const float PunchStackWindow = 0.55f;
        private const float SettleDuration = 0.28f;
        private const int SortMinOrder = 1;
        private const int SortMaxOrder = 500;

        private static GoldHudDomainHost sInstance;

        private readonly Transform mRoot;
        private readonly Transform mIcon;
        private Vector3 mBaseScale = Vector3.one;
        private bool mHasBaseScale;
        private float mPunch;
        private float mLastPunchUnscaledTime = -999f;
        private Tween mSettleTween;

        private GoldHudDomainHost(Transform root, Transform icon)
        {
            mRoot = root;
            mIcon = icon;
        }

        public static IGoldHudDomainHost Instance =>
            sInstance != null && sInstance.IsAvailable ? sInstance : null;

        /// <summary>HUD Presenter 显式安装 root/icon（幂等）；不按名搜索场景。</summary>
        public static bool Install(Transform root, Transform icon)
        {
            if (root == null || icon == null)
            {
                sInstance = null;
                return false;
            }

            if (sInstance != null
                && sInstance.IsAvailable
                && ReferenceEquals(sInstance.mRoot, root)
                && ReferenceEquals(sInstance.mIcon, icon))
            {
                return true;
            }

            sInstance = new GoldHudDomainHost(root, icon);
            sInstance.CaptureBaseScale();
            return true;
        }

        public static void ClearInstance()
        {
            sInstance = null;
        }

        public bool IsAvailable => mIcon != null && mRoot != null;

        public Transform AttachmentParent => mIcon != null ? (mIcon.parent ?? mRoot) : mRoot;

        public SpriteMask Mask => null;

        public bool TryWorldToLocal(Vector3 worldPosition, out Vector3 localPosition)
        {
            if (mRoot == null)
            {
                localPosition = Vector3.zero;
                return false;
            }

            localPosition = mRoot.InverseTransformPoint(worldPosition);
            return true;
        }

        public bool TryGetFollowTarget(out Transform followTarget)
        {
            followTarget = mIcon;
            return followTarget != null;
        }

        public bool TryGetSortingBounds(out VfxSortingBounds bounds)
        {
            bounds = new VfxSortingBounds("Main", SortMinOrder, SortMaxOrder);
            return true;
        }

        public Vector3 ResolveSinkWorldPosition()
        {
            var position = mIcon != null ? mIcon.position : Vector3.zero;
            position.z = 0f;
            return position;
        }

        public bool TryGetCoinSprite(out Sprite sprite)
        {
            sprite = null;
            if (mIcon != null)
            {
                sprite = mIcon.GetComponent<SpriteRenderer>()?.sprite;
            }

            return sprite != null;
        }

        public Vector3 ResolveDefaultOriginWorld()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                var depth = camera.orthographic
                    ? Mathf.Abs(camera.transform.position.z)
                    : camera.nearClipPlane;
                var world = camera.ScreenToWorldPoint(
                    new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, depth));
                world.z = 0f;
                return world;
            }

            var fallback = ResolveSinkWorldPosition();
            fallback.y += 1f;
            return fallback;
        }

        public void PunchIcon()
        {
            if (mIcon == null)
            {
                return;
            }

            CaptureBaseScale();
            var now = Time.unscaledTime;
            if (now - mLastPunchUnscaledTime > PunchStackWindow)
            {
                mPunch = 0f;
            }

            mLastPunchUnscaledTime = now;
            mPunch = Mathf.Min(MaxPunchScale - 1f, mPunch + PunchPerCoin);
            KillSettle();
            mIcon.localScale = mBaseScale * (1f + mPunch);
            ScheduleSettleAfterWindow();
        }

        public void SnapIconToBase()
        {
            if (mIcon == null)
            {
                return;
            }

            CaptureBaseScale();
            mPunch = 0f;
            KillSettle();
            mIcon.localScale = mBaseScale;
        }

        public void SettleIconToBase()
        {
            if (mIcon == null)
            {
                return;
            }

            CaptureBaseScale();
            mPunch = 0f;
            KillSettle();
            if (!Application.isPlaying)
            {
                mIcon.localScale = mBaseScale;
                return;
            }

            mSettleTween = mIcon
                .DOScale(mBaseScale, Mathf.Max(0.05f, SettleDuration))
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(mIcon.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (mIcon != null)
                    {
                        mIcon.localScale = mBaseScale;
                    }
                });
        }

        private void ScheduleSettleAfterWindow()
        {
            KillSettle();
            if (!Application.isPlaying)
            {
                return;
            }

            mSettleTween = DOTween.Sequence()
                .AppendInterval(PunchStackWindow)
                .AppendCallback(SettleIconToBase)
                .SetUpdate(true)
                .SetLink(mIcon.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void KillSettle()
        {
            if (mSettleTween != null && mSettleTween.IsActive())
            {
                mSettleTween.Kill();
            }

            mSettleTween = null;
        }

        private void CaptureBaseScale()
        {
            if (mIcon == null || mHasBaseScale)
            {
                return;
            }

            mBaseScale = mIcon.localScale;
            if (mBaseScale == Vector3.zero)
            {
                mBaseScale = Vector3.one;
            }

            mHasBaseScale = true;
        }
    }
}
