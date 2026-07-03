using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 双段无缝背景向左无限滚动。挂在 BG1/BG2 的父对象上。
    /// </summary>
    public sealed class InfiniteScrollingBackground : MonoBehaviour
    {
        [SerializeField] private Transform segmentA;
        [SerializeField] private Transform segmentB;
        [SerializeField] private float scrollSpeed = 2f;

        private float _segmentWidth;

        private void Awake()
        {
            ResolveSegments();
            CacheSegmentWidth();
        }

        private void OnValidate()
        {
            scrollSpeed = Mathf.Max(0f, scrollSpeed);
        }

        private void Update()
        {
            if (segmentA == null || segmentB == null || _segmentWidth <= 0f)
            {
                return;
            }

            var delta = scrollSpeed * Time.deltaTime;
            var offset = Vector3.left * delta;

            segmentA.localPosition += offset;
            segmentB.localPosition += offset;

            RepositionIfNeeded(segmentA, segmentB);
            RepositionIfNeeded(segmentB, segmentA);
        }

        private void RepositionIfNeeded(Transform segment, Transform other)
        {
            var pos = segment.localPosition;
            if (pos.x <= -_segmentWidth)
            {
                pos.x = other.localPosition.x + _segmentWidth;
                segment.localPosition = pos;
            }
        }

        private void ResolveSegments()
        {
            if (segmentA != null && segmentB != null)
            {
                return;
            }

            foreach (Transform child in transform)
            {
                if (segmentA == null && child.name == "BG1")
                {
                    segmentA = child;
                }
                else if (segmentB == null && child.name == "BG2")
                {
                    segmentB = child;
                }
            }
        }

        private void CacheSegmentWidth()
        {
            if (segmentA == null || segmentB == null)
            {
                return;
            }

            var renderer = segmentA.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                _segmentWidth = renderer.size.x * segmentA.localScale.x;
            }
            else
            {
                _segmentWidth = Mathf.Abs(segmentB.localPosition.x - segmentA.localPosition.x);
            }
        }
    }
}