using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow
{
    /// <summary>
    /// 遗物/技能图标槽命中代理：挂在槽位节点，由 Manager ApplyDefIds 写入 DefId。
    /// 遗物槽右键丢弃经 <see cref="PointerHitRouter"/> → RelicHudHook（#98）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ContentIconSlotHitProxy : MonoBehaviour, IPointerHitTarget
    {
        private const int TypePriority = 25;

        [Tooltip("运行时由 Relic/Skill Manager 写入；留空表示空槽。")]
        [SerializeField] private string defId;

        private BoxCollider2D mCollider;

        public string DefId
        {
            get => defId;
            set => defId = value ?? string.Empty;
        }

        public Collider2D HitCollider =>
            mCollider != null ? mCollider : (mCollider = GetComponent<BoxCollider2D>());

        public int HitSortOrder
        {
            get
            {
                var group = GetComponent<SortingGroup>();
                if (group != null)
                {
                    return group.sortingOrder;
                }

                var renderer = GetComponent<SpriteRenderer>();
                return renderer != null ? renderer.sortingOrder : 0;
            }
        }

        public int HitTypePriority => TypePriority;

        private void Awake()
        {
            EnsureCollider();
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
        }

        private void EnsureCollider()
        {
            mCollider = GetComponent<BoxCollider2D>();
            if (mCollider == null)
            {
                mCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            mCollider.isTrigger = true;
            if (mCollider.size.sqrMagnitude > 0.01f)
            {
                return;
            }

            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                mCollider.size = renderer.sprite.bounds.size;
            }
            else
            {
                mCollider.size = new Vector2(0.8f, 0.8f);
            }
        }
    }
}
