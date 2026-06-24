using UnityEngine;

namespace NineGrid.Presentation.Performance
{
    /// <summary>
    /// 记录 Standard Card 预制体各 SpriteRenderer 相对锚点（面图层）的 sortingOrder 差值，
    /// 供运行时整体平移图层时保持框/面/图标/数值的相对前后关系。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardSortingLayerProfile : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer anchorRenderer;
        [SerializeField] private SpriteRenderer[] trackedRenderers;
        [SerializeField] private int[] sortingOffsets;

        private void Awake()
        {
            CaptureIfNeeded();
        }

        public void CaptureIfNeeded()
        {
            if (sortingOffsets != null && sortingOffsets.Length > 0)
            {
                return;
            }

            trackedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (trackedRenderers.Length == 0)
            {
                return;
            }

            if (anchorRenderer == null)
            {
                anchorRenderer = GetComponent<SpriteRenderer>();
                if (anchorRenderer == null)
                {
                    Transform face = transform.Find("Standard Card");
                    anchorRenderer = face != null ? face.GetComponent<SpriteRenderer>() : trackedRenderers[0];
                }
            }

            int anchorOrder = anchorRenderer != null ? anchorRenderer.sortingOrder : trackedRenderers[0].sortingOrder;
            sortingOffsets = new int[trackedRenderers.Length];
            for (var i = 0; i < trackedRenderers.Length; i++)
            {
                SpriteRenderer renderer = trackedRenderers[i];
                sortingOffsets[i] = renderer != null ? renderer.sortingOrder - anchorOrder : 0;
            }

            if (trackedRenderers.Length > 1 && AllOffsetsZero(sortingOffsets))
            {
                for (var i = 0; i < trackedRenderers.Length; i++)
                {
                    sortingOffsets[i] = ResolveStandardCardOffset(trackedRenderers[i]);
                }
            }
        }

        private static bool AllOffsetsZero(int[] offsets)
        {
            for (var i = 0; i < offsets.Length; i++)
            {
                if (offsets[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ResolveStandardCardOffset(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return 0;
            }

            string objectName = renderer.gameObject.name.Trim();
            if (objectName == "Card Frame")
            {
                return -1;
            }

            if (objectName == "Standard Card" || objectName == "Armor")
            {
                return 0;
            }

            return 1;
        }

        public void ApplyBaseOrder(int baseOrder)
        {
            CaptureIfNeeded();
            if (trackedRenderers == null || sortingOffsets == null)
            {
                return;
            }

            for (var i = 0; i < trackedRenderers.Length; i++)
            {
                SpriteRenderer renderer = trackedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingOrder = baseOrder + sortingOffsets[i];
            }
        }

        public int GetAnchorSortingOrder()
        {
            CaptureIfNeeded();
            return anchorRenderer != null ? anchorRenderer.sortingOrder : 0;
        }
    }
}
