using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 非战斗场地对象悬停 → 简要解释文字框。战斗真卡不挂本代理（右键详述另责）。
    /// M1 挂场地图标；就地选项卡 / 商店货架由 M2 Spawn 时复用本代理。
    /// 配置 <see cref="WalkBoardSlot"/> 后，跳格开启时单击提交 BoardWalk（图标 collider 高于空槽代理）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BoardBriefTipHitProxy : MonoBehaviour, IPointerHitTarget
    {
        // 高于 GroundCardHitProxy(30) 空槽(10)；低于半黑屏 UiOverlay(100)。
        // Walk 图标固定 SortOrder，避免 SortingGroup 低于空槽时抢走 BoardWalk。
        private const int TypePriority = 35;
        private const int WalkIconSortOrder = 50;

        [SerializeField] private string tipText = string.Empty;

        private BoxCollider2D mCollider;
        private int mHoverGeneration;
        private int mWalkBoardSlot;

        public string TipText
        {
            get => tipText;
            set => tipText = value ?? string.Empty;
        }

        /// <summary>1–9：跳格目标格；0 表示不转发点击。</summary>
        public int WalkBoardSlot => mWalkBoardSlot;

        public Collider2D HitCollider =>
            mCollider != null ? mCollider : (mCollider = GetComponent<BoxCollider2D>());

        public int HitSortOrder =>
            mWalkBoardSlot >= SlotId.MinBoardIndex && mWalkBoardSlot <= SlotId.MaxBoardIndex
                ? WalkIconSortOrder
                : ResolveHitSortOrder();

        public int HitTypePriority => TypePriority;

        public void Configure(string tip, Vector2? colliderSize = null)
        {
            Configure(tip, walkBoardSlot: 0, colliderSize);
        }

        public void Configure(string tip, int walkBoardSlot, Vector2? colliderSize = null)
        {
            TipText = tip;
            mWalkBoardSlot = walkBoardSlot;
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
            // 场地图标：点上去 → BoardWalk；驻留 1s 由 RoomIconBoardPresenter / 房内 Presenter 提交。
            // 就地选项/货架另挂专用 HitProxy，不会配 WalkBoardSlot。
            if (mWalkBoardSlot < SlotId.MinBoardIndex || mWalkBoardSlot > SlotId.MaxBoardIndex)
            {
                return;
            }

            if (BoardWalkInputHook.IsEnabled == null || !BoardWalkInputHook.IsEnabled())
            {
                Debug.LogWarning(
                    "[BoardBriefTip] BoardWalk click ignored: walk disabled slot="
                    + mWalkBoardSlot
                    + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                    + " owner=" + PresentationInputGates.CurrentOwner);
                return;
            }

            if (BoardWalkInputHook.TrySubmitBoardWalk == null)
            {
                Debug.LogWarning("[BoardBriefTip] BoardWalkInputHook.TrySubmitBoardWalk 未装配。");
                return;
            }

            Debug.Log(
                "[BoardBriefTip] BoardWalk submit slot=" + mWalkBoardSlot
                + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                + " owner=" + PresentationInputGates.CurrentOwner);
            var accepted = BoardWalkInputHook.TrySubmitBoardWalk(mWalkBoardSlot);
            if (!accepted)
            {
                Debug.LogWarning(
                    "[BoardBriefTip] BoardWalk rejected/buffered-fail slot=" + mWalkBoardSlot
                    + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                    + " owner=" + PresentationInputGates.CurrentOwner);
            }
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
