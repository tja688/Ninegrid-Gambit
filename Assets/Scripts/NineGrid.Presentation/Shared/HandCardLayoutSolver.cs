using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Shared
{
    [Serializable]
    public struct HandCardLayoutTarget
    {
        public Vector3 LocalPosition;
        public int SortingOrder;
    }

    /// <summary>
    /// 手牌数据驱动布局：最左锚点固定，间距受 5 张预设首末锚点夹制，避免动态排位越界。
    /// </summary>
    [Serializable]
    [MovedFrom(true, "NineGrid.Presentation.Performance.Layout", null, "HandCardLayoutSolver")]
    public sealed class HandCardLayoutSolver
    {
        [Header("Reference Anchors (5-card preset)")]
        [SerializeField] private Transform[] referenceAnchors = Array.Empty<Transform>();

        [Header("Layout")]
        [SerializeField] private Vector3 leftAnchorLocal = new(-1.28125f, 0f, 0f);
        [SerializeField] private Vector3 rightAnchorLocal = Vector3.zero;
        [SerializeField, Min(0.01f)] private float cardWidth = 1.625f;
        [SerializeField, Range(0f, 0.95f)] private float minOverlapRatio = 1f / 3f;
        [SerializeField, Min(1)] private int maxCardCount = 5;
        [SerializeField] private int baseSortingOrder = 1;

        public Vector3 LeftAnchorLocal => leftAnchorLocal;
        public Vector3 RightAnchorLocal => rightAnchorLocal;
        public float CardWidth => cardWidth;

        public void ResolveFromReferenceAnchors()
        {
            if (referenceAnchors == null || referenceAnchors.Length == 0)
            {
                return;
            }

            Transform left = referenceAnchors[0];
            if (left != null)
            {
                leftAnchorLocal = left.localPosition;
            }

            if (referenceAnchors.Length >= maxCardCount)
            {
                Transform right = referenceAnchors[maxCardCount - 1];
                if (right != null)
                {
                    rightAnchorLocal = right.localPosition;
                }
            }
        }

        public float ComputeSpacing(int count)
        {
            count = Mathf.Clamp(count, 1, Mathf.Max(1, maxCardCount));
            if (count <= 1)
            {
                return 0f;
            }

            float spacingAt5 = ResolveSpacingAtMax();
            float spacingAt2 = cardWidth * (1f - minOverlapRatio);

            float spacing;
            if (count <= 2)
            {
                spacing = spacingAt2;
            }
            else if (count >= maxCardCount)
            {
                spacing = spacingAt5;
            }
            else
            {
                float t = (count - 2f) / Mathf.Max(1f, maxCardCount - 2);
                spacing = Mathf.Lerp(spacingAt2, spacingAt5, t);
            }

            float maxSpacingWithinAnchors = ResolveMaxSpacingWithinAnchors(count);
            return Mathf.Min(spacing, maxSpacingWithinAnchors);
        }

        public void BuildLayout(int count, IList<HandCardLayoutTarget> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            count = Mathf.Clamp(count, 0, Mathf.Max(1, maxCardCount));
            if (count == 0)
            {
                return;
            }

            float spacing = ComputeSpacing(count);
            for (var i = 0; i < count; i++)
            {
                var localPosition = leftAnchorLocal + new Vector3(spacing * i, 0f, 0f);
                int sortingOrder = baseSortingOrder + (count - 1 - i);
                results.Add(new HandCardLayoutTarget
                {
                    LocalPosition = localPosition,
                    SortingOrder = sortingOrder,
                });
            }
        }

        public Vector3 GetLocalPosition(int index, int count)
        {
            float spacing = ComputeSpacing(count);
            return leftAnchorLocal + new Vector3(spacing * index, 0f, 0f);
        }

        public int GetSortingOrder(int index, int count)
        {
            return baseSortingOrder + (count - 1 - index);
        }

        private float ResolveSpacingAtMax()
        {
            if (referenceAnchors != null && referenceAnchors.Length >= maxCardCount)
            {
                Transform left = referenceAnchors[0];
                Transform right = referenceAnchors[maxCardCount - 1];
                if (left != null && right != null && maxCardCount > 1)
                {
                    return (right.localPosition.x - left.localPosition.x) / (maxCardCount - 1);
                }
            }

            return 0.3203125f;
        }

        private float ResolveMaxSpacingWithinAnchors(int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            float span = rightAnchorLocal.x - leftAnchorLocal.x;
            if (span <= 0f)
            {
                span = ResolveSpacingAtMax() * Mathf.Max(1, maxCardCount - 1);
            }

            return Mathf.Max(0f, span / (count - 1));
        }

        public void SetReferenceAnchors(Transform[] anchors)
        {
            referenceAnchors = anchors ?? Array.Empty<Transform>();
            ResolveFromReferenceAnchors();
        }
    }
}
