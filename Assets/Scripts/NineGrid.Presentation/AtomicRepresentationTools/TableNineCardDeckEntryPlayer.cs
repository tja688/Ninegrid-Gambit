using System;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.AtomicRepresentationTools
{
    /// <summary>
    /// 牌堆入场：卡牌依次从同一屏幕外牌叠抽入，按 0→1→2… 间隔顺序落位（由示例锚点推算后续槽位，无上限）。
    /// 图层按数量动态分配：第一张最高，最后一张最低。
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
            public float Spacing;
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
        [SerializeField, Min(0.1f)] private float offScreenDistance = 3f;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.3f;
        [SerializeField, Min(0f)] private float entryInterval = 0.15f;
        [SerializeField, Min(0f)] private float entryPeelSpacing = 0.12f;
        [SerializeField] private Ease moveEase = Ease.OutQuad;
        [SerializeField] private bool ignoreTimeScale;

        [Header("Sorting")]
        [SerializeField] private int topSortingOrder = 100;
        [SerializeField] private int bottomSortingOrder = 0;

        private readonly System.Collections.Generic.List<GameObject> spawnedCards = new();
        private Sequence activeSequence;
        private DeckSpreadLayout layout;
        private GameObject slotZeroPrototype;

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
            entryPeelSpacing = Mathf.Max(0f, entryPeelSpacing);

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
            if (!isActiveAndEnabled)
            {
                return;
            }

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

            Vector3 peelAxis = ResolvePeelAxis();
            activeSequence = DOTween.Sequence().SetTarget(this);

            if (ignoreTimeScale)
            {
                activeSequence.SetUpdate(true);
            }

            for (int i = 0; i < count; i++)
            {
                int cardIndex = i;
                Vector3 targetWorld = ResolveTargetWorldPosition(cardIndex);
                Vector3 startWorld = ResolveEntrySpawnWorld(cardIndex, peelAxis);
                float startDelay = cardIndex * entryInterval;
                int sortingOrder = ResolveSortingOrder(cardIndex, count);

                activeSequence.InsertCallback(startDelay, () =>
                {
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
                });
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
        }

        public void SetTestCardCount(int count)
        {
            testCardCount = Mathf.Max(0, count);
        }

        private bool TryBuildSpreadLayout(out DeckSpreadLayout spreadLayout)
        {
            spreadLayout = default;

            Transform root = cardDecksRoot != null ? cardDecksRoot : transform;
            Transform slotZero = null;
            Transform slotOne = null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string childName = child.name.Trim();
                if (!int.TryParse(childName, out int slotIndex))
                {
                    continue;
                }

                if (slotIndex == 0)
                {
                    slotZero = child;
                }
                else if (slotIndex == 1)
                {
                    slotOne = child;
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

            spreadLayout = new DeckSpreadLayout
            {
                OriginWorld = root.TransformPoint(originLocal),
                SpreadAxisWorld = root.TransformDirection(spreadDirLocal).normalized,
                Spacing = spacing,
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

        private Vector3 ResolveTargetWorldPosition(int cardIndex)
        {
            return layout.OriginWorld + layout.SpreadAxisWorld * (layout.Spacing * cardIndex);
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

        private Vector3 ResolveEntrySpawnWorld(int cardIndex, Vector3 peelAxis)
        {
            return layout.OriginWorld + ResolveEntryOffset() + peelAxis * (entryPeelSpacing * cardIndex);
        }

        private Vector3 ResolvePeelAxis()
        {
            Vector3 offset = ResolveEntryOffset();
            if (offset.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.right;
            }

            return offset.normalized;
        }

        private Vector3 ResolveEntryOffset()
        {
            return entryDirection switch
            {
                EntryDirection.FromRight => Vector3.right * offScreenDistance,
                EntryDirection.FromLeft => Vector3.left * offScreenDistance,
                EntryDirection.FromTop => Vector3.up * offScreenDistance,
                EntryDirection.FromBottom => Vector3.down * offScreenDistance,
                _ => Vector3.right * offScreenDistance,
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
