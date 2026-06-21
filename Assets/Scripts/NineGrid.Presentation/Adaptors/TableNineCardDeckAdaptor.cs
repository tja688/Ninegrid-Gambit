using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 牌堆场域占位适配器。当前仅落实场域内第一层子对象的显示层级规则：
    /// 兄弟顺序越靠前，显示层级越高；越靠后，显示层级越低。
    /// 后续内核事件接入与表演驱动在此扩展。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class TableNineCardDeckAdaptor : MonoBehaviour
    {
        [Header("Field")]
        [SerializeField] private Transform fieldRoot;
        [SerializeField] private string sortingLayerName;

        [Header("Display Order")]
        [SerializeField] private int topSortingOrder = 100;
        [SerializeField, Min(1)] private int sortingOrderStep = 1;

        private Transform ResolvedFieldRoot => fieldRoot != null ? fieldRoot : transform;

        private void OnEnable()
        {
            ApplyFieldRules();
        }

        private void OnTransformChildrenChanged()
        {
            if (fieldRoot != null && fieldRoot != transform)
            {
                return;
            }

            ApplyFieldRules();
        }

        private void OnValidate()
        {
            sortingOrderStep = Mathf.Max(1, sortingOrderStep);
            ApplyFieldRules();
        }

        [ContextMenu("Apply Field Rules")]
        public void ApplyFieldRules()
        {
            Transform root = ResolvedFieldRoot;
            if (root == null)
            {
                return;
            }

            int childCount = root.childCount;
            for (var i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                int sortingOrder = topSortingOrder - i * sortingOrderStep;
                ApplyDisplayOrder(child, sortingOrder);
            }
        }

        private void ApplyDisplayOrder(Transform child, int sortingOrder)
        {
            if (child == null)
            {
                return;
            }

            var spriteRenderer = child.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                if (!string.IsNullOrEmpty(sortingLayerName))
                {
                    spriteRenderer.sortingLayerName = sortingLayerName;
                }

                spriteRenderer.sortingOrder = sortingOrder;
            }

            var sortingGroup = child.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                if (!string.IsNullOrEmpty(sortingLayerName))
                {
                    sortingGroup.sortingLayerName = sortingLayerName;
                }

                sortingGroup.sortingOrder = sortingOrder;
            }
        }
    }
}
