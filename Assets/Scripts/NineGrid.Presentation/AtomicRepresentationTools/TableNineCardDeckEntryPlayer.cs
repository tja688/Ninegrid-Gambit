using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.AtomicRepresentationTools
{
    /// <summary>
    /// 牌堆入场：卡牌在屏幕外同向叠成牌叠，依次滑入 0→1→2… 槽位（由 CardDecks 锚点推算后续槽位，无上限）。
    /// 手感基准：<see cref="CardDeckEntryExample"/> Timeline（duration 0.3 / interval 0.1 / OutQuart）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineCardDeckEntryPlayer : MonoBehaviour
    {
        public enum EntryDirection
        {
            FromRight = 0,
            FromLeft = 1,
            FromTop = 2,
            FromBottom = 3,
        }

        private struct DeckSpreadLayout
        {
            public Vector3 OriginWorld;
            public Vector3 SpreadAxisWorld;
            public float BaseSpacing;
            public float MaxVisibleSpan;
            public int VisibleCapacity;
        }

        [Header("Anchors")]
        [SerializeField] private Transform cardDecksRoot;
        [SerializeField] private Transform cardContainer;

        [Header("Card Visual")]
        [SerializeField] private GameObject cardPrototype;
        [SerializeField] private bool hidePrototypeOnPlay = true;

        [Header("Test")]
        [SerializeField, Min(0)] private int testCardCount = 5;

        [Header("Motion")]
        [SerializeField] private EntryDirection entryDirection = EntryDirection.FromRight;
        [SerializeField, Min(0.1f)] private float offScreenDistance = 3.59375f;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.3f;
        [SerializeField, Min(0f)] private float entryInterval = 0.1f;
        [SerializeField] private Ease moveEase = Ease.OutQuart;
        [SerializeField] private bool ignoreTimeScale;

        [Header("Sorting")]
        [SerializeField] private int topSortingOrder = 100;
        [SerializeField] private int bottomSortingOrder = 0;

        private readonly List<GameObject> spawnedCards = new();
        private readonly Dictionary<int, Transform> slotAnchors = new();

        private Sequence activeSequence;
        private DeckSpreadLayout layout;
        private GameObject slotZeroPrototype;
        private Vector2 runtimeEntryDirection = Vector2.right;

        private Transform ResolvedContainer => cardContainer != null ? cardContainer : transform;

        private void OnDisable()
        {
            StopAndClear();
        }

        private void OnValidate()
        {
            testCardCount = Mathf.Max(0, testCardCount);
            offScreenDistance = Mathf.Max(0.1f, offScreenDistance);
            moveDuration = Mathf.Max(0.01f, moveDuration);
            entryInterval = Mathf.Max(0f, entryInterval);

            if (bottomSortingOrder > topSortingOrder)
            {
                bottomSortingOrder = topSortingOrder;
            }
        }

        [ContextMenu("Play")]
        public void Play()
        {
            PlayWithCount(testCardCount);
        }

        public void PlayWithCount(int count)
        {
            PlayWithCount(count, ResolveEntryDirectionVector(entryDirection));
        }

        public void PlayWithCount(int count, EntryDirection direction)
        {
            PlayWithCount(count, ResolveEntryDirectionVector(direction));
        }

        public void PlayWithCount(int count, float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            PlayWithCount(count, new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)));
        }

        public void PlayWithCount(int count, Vector2 direction)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector2.right;
            }

            runtimeEntryDirection = direction.normalized;
            count = Mathf.Max(0, count);
            StopAndClear();

            if (count == 0 || !TryBuildSpreadLayout(out layout))
            {
                return;
            }

            GameObject prototype = ResolveCardPrototype();
            if (prototype == null)
            {
                Debug.LogWarning($"{nameof(TableNineCardDeckEntryPlayer)} on '{name}' could not resolve card prototype.", this);
                return;
            }

            if (hidePrototypeOnPlay && cardPrototype != null && cardPrototype == prototype)
            {
                cardPrototype.SetActive(false);
            }

            activeSequence = DOTween.Sequence().SetTarget(this);
            if (ignoreTimeScale)
            {
                activeSequence.SetUpdate(true);
            }

            Vector3 entryOffset = ResolveEntryOffset();

            for (int i = 0; i < count; i++)
            {
                int cardIndex = i;
                Vector3 targetWorld = ResolveTargetWorldPosition(cardIndex, count);
                Vector3 startWorld = targetWorld + entryOffset;
                int sortingOrder = ResolveSortingOrder(cardIndex, count);

                GameObject card = Instantiate(prototype, startWorld, Quaternion.identity, ResolvedContainer);
                card.name = $"EntryCard_{cardIndex}";
                card.SetActive(true);
                ApplySortingOrder(card, sortingOrder);

                Transform cardTransform = card.transform;
                cardTransform.position = startWorld;
                spawnedCards.Add(card);

                Tween moveTween = cardTransform
                    .DOMove(targetWorld, moveDuration)
                    .SetEase(moveEase)
                    .SetTarget(this);

                if (ignoreTimeScale)
                {
                    moveTween.SetUpdate(true);
                }

                activeSequence.Insert(cardIndex * entryInterval, moveTween);
            }
        }

        [ContextMenu("Stop And Clear")]
        public void StopAndClear()
        {
            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.Kill();
            }

            activeSequence = null;
            DOTween.Kill(this);

            for (int i = 0; i < spawnedCards.Count; i++)
            {
                GameObject card = spawnedCards[i];
                if (card == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(card);
                }
                else
                {
                    DestroyImmediate(card);
                }
            }

            spawnedCards.Clear();
            slotAnchors.Clear();
        }

        public void SetTestCardCount(int count)
        {
            testCardCount = Mathf.Max(0, count);
        }

        public void SetEntryDirection(EntryDirection direction)
        {
            entryDirection = direction;
            runtimeEntryDirection = ResolveEntryDirectionVector(direction);
        }

        private bool TryBuildSpreadLayout(out DeckSpreadLayout spreadLayout)
        {
            spreadLayout = default;
            slotAnchors.Clear();

            Transform root = cardDecksRoot != null ? cardDecksRoot : transform;
            Transform slotZero = null;
            Transform slotOne = null;
            Transform slotEnd = null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string childName = child.name.Trim();
                if (int.TryParse(childName, out int slotIndex))
                {
                    slotAnchors[slotIndex] = child;

                    if (slotIndex == 0)
                    {
                        slotZero = child;
                    }
                    else if (slotIndex == 1)
                    {
                        slotOne = child;
                    }
                }
                else if (string.Equals(childName, "end", StringComparison.OrdinalIgnoreCase))
                {
                    slotEnd = child;
                }
            }

            if (slotZero == null)
            {
                Debug.LogWarning($"{nameof(TableNineCardDeckEntryPlayer)} on '{name}' needs anchor '0' under '{root.name}'.", this);
                return false;
            }

            slotZeroPrototype = slotZero.gameObject;

            Vector3 originLocal = slotZero.localPosition;
            Vector3 spreadStepLocal;
            float spacing;

            if (slotOne != null)
            {
                spreadStepLocal = slotOne.localPosition - originLocal;
                spacing = spreadStepLocal.magnitude;
            }
            else
            {
                spreadStepLocal = Vector3.right * 0.125f;
                spacing = spreadStepLocal.magnitude;
            }

            if (spacing <= Mathf.Epsilon)
            {
                spreadStepLocal = Vector3.right * 0.125f;
                spacing = spreadStepLocal.magnitude;
            }

            Vector3 spreadDirLocal = spreadStepLocal / spacing;
            float maxVisibleSpan = spacing;
            int visibleCapacity = 2;

            if (slotEnd != null)
            {
                float projectedSpan = Vector3.Dot(slotEnd.localPosition - originLocal, spreadDirLocal);
                if (projectedSpan > spacing + Mathf.Epsilon)
                {
                    maxVisibleSpan = projectedSpan;
                    visibleCapacity = Mathf.Max(2, Mathf.RoundToInt(projectedSpan / spacing) + 1);
                }
            }

            spreadLayout = new DeckSpreadLayout
            {
                OriginWorld = root.TransformPoint(originLocal),
                SpreadAxisWorld = root.TransformDirection(spreadDirLocal).normalized,
                BaseSpacing = spacing,
                MaxVisibleSpan = maxVisibleSpan,
                VisibleCapacity = visibleCapacity,
            };

            return true;
        }

        private GameObject ResolveCardPrototype()
        {
            if (cardPrototype != null)
            {
                return cardPrototype;
            }

            return slotZeroPrototype;
        }

        private Vector3 ResolveTargetWorldPosition(int cardIndex, int totalCount)
        {
            if (cardIndex == 0 && slotAnchors.TryGetValue(0, out Transform anchor) && anchor != null)
            {
                return anchor.position;
            }

            float spacing = ResolveAdaptiveSpacing(totalCount);
            return layout.OriginWorld + layout.SpreadAxisWorld * (spacing * cardIndex);
        }

        private int ResolveSortingOrder(int cardIndex, int totalCount)
        {
            if (totalCount <= 1)
            {
                return topSortingOrder;
            }

            float t = cardIndex / (float)(totalCount - 1);
            return Mathf.RoundToInt(Mathf.Lerp(topSortingOrder, bottomSortingOrder, t));
        }

        private float ResolveAdaptiveSpacing(int totalCount)
        {
            if (totalCount <= 1)
            {
                return 0f;
            }

            if (layout.VisibleCapacity <= 2 || layout.MaxVisibleSpan <= layout.BaseSpacing + Mathf.Epsilon || totalCount >= layout.VisibleCapacity)
            {
                return layout.BaseSpacing;
            }

            float expandedSpacing = layout.MaxVisibleSpan / (totalCount - 1);
            float smallCountBlend = 1f - Mathf.InverseLerp(2f, layout.VisibleCapacity, totalCount);
            smallCountBlend *= smallCountBlend;
            return Mathf.Lerp(layout.BaseSpacing, expandedSpacing, smallCountBlend);
        }

        private Vector3 ResolveEntryOffset()
        {
            Vector3 planarDirection = new Vector3(runtimeEntryDirection.x, runtimeEntryDirection.y, 0f);
            if (planarDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                planarDirection = Vector3.right;
            }

            return planarDirection.normalized * offScreenDistance;
        }

        private static Vector2 ResolveEntryDirectionVector(EntryDirection direction)
        {
            return direction switch
            {
                EntryDirection.FromRight => Vector2.right,
                EntryDirection.FromLeft => Vector2.left,
                EntryDirection.FromTop => Vector2.up,
                EntryDirection.FromBottom => Vector2.down,
                _ => Vector2.right,
            };
        }

        private static void ApplySortingOrder(GameObject card, int sortingOrder)
        {
            SpriteRenderer[] renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.sortingOrder = sortingOrder;
                }
            }
        }
    }
}
