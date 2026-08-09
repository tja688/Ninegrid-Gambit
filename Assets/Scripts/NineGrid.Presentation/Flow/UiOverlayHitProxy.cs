using NineGrid.Flow.BattleInfoPreview;
using UnityEngine;

namespace NineGrid.Flow
{
    public enum UiOverlayHitAction
    {
        /// <summary>吞点击；若详述开着则只关详述（面板内不关战斗信息预览）。</summary>
        Swallow = 0,
        CloseCardInspect = 1,
        /// <summary>半黑屏：详述优先关；详述已关且预览开则关预览。</summary>
        DimmerBackground = 2,
    }

    /// <summary>
    /// 局内 UI 叠层命中代理：半黑屏吞点击 / 关闭按钮关右键详述。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class UiOverlayHitProxy : MonoBehaviour, IPointerHitTarget
    {
        [SerializeField] private UiOverlayHitAction action = UiOverlayHitAction.Swallow;
        [SerializeField] private int hitSortOrder = BattleUiDimmerOverlay.HitSort;
        [SerializeField] private int hitTypePriority = PointerHitSurfacePriorities.Overlay;

        private BoxCollider2D _collider;

        public Collider2D HitCollider => _collider != null ? _collider : (_collider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => hitSortOrder;

        public int HitTypePriority => hitTypePriority;

        public void Configure(UiOverlayHitAction hitAction, int sort, int typePriority)
        {
            action = hitAction;
            hitSortOrder = sort;
            hitTypePriority = typePriority;
        }

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            if (_collider != null)
            {
                _collider.isTrigger = false;
            }
        }

        private void OnEnable()
        {
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
        }

        public void HandlePointerEnter()
        {
        }

        public void HandlePointerExit()
        {
        }

        public void HandlePointerDown()
        {
            switch (action)
            {
                case UiOverlayHitAction.CloseCardInspect:
                    CardInspectOverlayPresenter.CloseIfOpen();
                    break;
                case UiOverlayHitAction.Swallow:
                    if (CardInspectOverlayPresenter.IsOpen)
                    {
                        CardInspectOverlayPresenter.CloseIfOpen();
                    }

                    break;
                case UiOverlayHitAction.DimmerBackground:
                    if (CardInspectOverlayPresenter.IsOpen)
                    {
                        CardInspectOverlayPresenter.CloseIfOpen();
                        break;
                    }

                    if (BattleInfoPreviewPresenter.IsOpen)
                    {
                        BattleInfoPreviewPresenter.RequestDismiss();
                    }

                    break;
                default:
                    break;
            }
        }
    }
}
