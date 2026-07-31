using UnityEngine;

namespace NineGrid.Flow
{
    public enum UiOverlayHitAction
    {
        Swallow = 0,
        CloseCardInspect = 1,
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
        [SerializeField] private int hitTypePriority = 100;

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
                    // 半黑屏：右键详述打开时点菜单外（BG 图范围外）也退出。
                    if (CardInspectOverlayPresenter.IsOpen)
                    {
                        CardInspectOverlayPresenter.CloseIfOpen();
                    }

                    break;
                default:
                    break;
            }
        }
    }
}
