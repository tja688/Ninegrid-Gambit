using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    [Serializable]
    public struct DeckCardLayoutTarget
    {
        public Vector3 WorldPosition;
        public int SortingOrder;
    }

    /// <summary>
    /// 牌堆数据驱动布局：给定 n 张牌返回每张标准位与排序；最左（index 0）层级最高。
    /// </summary>
    [Serializable]
    public sealed class CardDeckLayoutSolver
    {
        [Header("Reference Anchors")]
        [SerializeField] private Transform[] referenceAnchors = Array.Empty<Transform>();

        [Header("Sorting")]
        [SerializeField] private int baseSortingOrder = 3;

        public int BaseSortingOrder => baseSortingOrder;

        public void ResolveFromReferenceAnchors(Transform anchorRoot)
        {
            if (anchorRoot == null)
            {
                return;
            }

            int childCount = anchorRoot.childCount;
            if (childCount == 0)
            {
                return;
            }

            referenceAnchors = new Transform[childCount];
            for (var i = 0; i < childCount; i++)
            {
                referenceAnchors[i] = anchorRoot.GetChild(i);
            }
        }

        public void SetReferenceAnchors(Transform[] anchors)
        {
            referenceAnchors = anchors ?? Array.Empty<Transform>();
        }

        public int GetSortingOrder(int index, int count)
        {
            if (count <= 0)
            {
                return baseSortingOrder;
            }

            index = Mathf.Clamp(index, 0, count - 1);
            return baseSortingOrder + (count - 1 - index);
        }

        public Vector3 GetWorldPosition(int index, int count)
        {
            if (count <= 0 || referenceAnchors == null || referenceAnchors.Length == 0)
            {
                return Vector3.zero;
            }

            index = Mathf.Clamp(index, 0, count - 1);
            if (index < referenceAnchors.Length && referenceAnchors[index] != null)
            {
                return referenceAnchors[index].position;
            }

            Transform last = referenceAnchors[referenceAnchors.Length - 1];
            if (last == null)
            {
                return Vector3.zero;
            }

            if (referenceAnchors.Length == 1)
            {
                return last.position;
            }

            Transform prev = referenceAnchors[referenceAnchors.Length - 2];
            if (prev == null)
            {
                return last.position;
            }

            Vector3 step = last.position - prev.position;
            int overflow = index - (referenceAnchors.Length - 1);
            return last.position + step * overflow;
        }

        public void BuildLayout(int count, IList<DeckCardLayoutTarget> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (count <= 0)
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                results.Add(new DeckCardLayoutTarget
                {
                    WorldPosition = GetWorldPosition(i, count),
                    SortingOrder = GetSortingOrder(i, count),
                });
            }
        }
    }
}
