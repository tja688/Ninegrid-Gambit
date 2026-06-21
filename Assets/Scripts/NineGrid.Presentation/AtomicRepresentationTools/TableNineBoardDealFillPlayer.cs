using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Presentation.AtomicRepresentationTools
{
    /// <summary>
    /// 九宫格发牌 + 牌堆递进补位：按绕圈顺序（跳过格5）快速发牌，每发一张牌堆剩余牌丝滑补位。
    /// PlayDealOne / PlayDealAll 供 UnityEvent 测试单发与群发。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineBoardDealFillPlayer : MonoBehaviour
    {
        private static readonly int[] DefaultDealOrder = { 1, 2, 3, 6, 9, 8, 7, 4 };

        private struct DeckSpreadLayout
        {
            public Vector3 OriginWorld;
            public Vector3 SpreadAxisWorld;
            public float BaseSpacing;
            public float MaxVisibleSpan;
            public int VisibleCapacity;
        }

        [Header("Anchors")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform deckRoot;
        [SerializeField] private Transform cardContainer;

        [Header("Card Visual")]
        [SerializeField] private GameObject cardPrototype;

        [Header("Test")]
        [SerializeField, Min(0)] private int testDeckCount = 8;
        [SerializeField] private int[] dealOrder = DefaultDealOrder;

        [Header("Deal Motion")]
        [SerializeField, Min(0.01f)] private float dealDuration = 0.22f;
        [SerializeField, Min(0f)] private float dealStepInterval = 0.1f;
        [SerializeField] private Ease dealEase = Ease.OutCubic;
        [SerializeField, Min(0f)] private float dealArcHeight = 0.35f;
        [SerializeField, Min(1f)] private float dealLandScalePunch = 1.04f;
        [SerializeField, Min(0.01f)] private float dealLandScaleDuration = 0.12f;

        [Header("Deck Refill")]
        [SerializeField, Min(0.01f)] private float deckShiftDuration = 0.18f;
        [SerializeField] private Ease deckShiftEase = Ease.OutQuad;

        [Header("Sorting")]
        [SerializeField] private int deckTopSortingOrder = 120;
        [SerializeField] private int deckBottomSortingOrder = 80;
        [SerializeField] private int flyingSortingOrder = 200;
        [SerializeField] private int boardSortingOrder = 50;

        [Header("Options")]
        [SerializeField] private bool ignoreTimeScale;

        private readonly List<GameObject> deckCards = new();
        private readonly Dictionary<int, GameObject> boardCards = new();
        private readonly Dictionary<int, Transform> slotAnchors = new();

        private Sequence batchSequence;
        private DeckSpreadLayout deckLayout;
        private int nextDealOrderIndex;
        private bool deckLayoutReady;

        private int[] ResolvedDealOrder =>
            dealOrder == null || dealOrder.Length == 0 ? DefaultDealOrder : dealOrder;

        private Transform ResolvedContainer => cardContainer != null ? cardContainer : transform;

        private void OnDisable()
        {
            StopAndClear();
        }

        private void OnValidate()
        {
            testDeckCount = Mathf.Max(0, testDeckCount);
            dealDuration = Mathf.Max(0.01f, dealDuration);
            dealStepInterval = Mathf.Max(0f, dealStepInterval);
            deckShiftDuration = Mathf.Max(0.01f, deckShiftDuration);
            dealLandScaleDuration = Mathf.Max(0.01f, dealLandScaleDuration);
            dealLandScalePunch = Mathf.Max(1f, dealLandScalePunch);

            if (deckBottomSortingOrder > deckTopSortingOrder)
            {
                deckBottomSortingOrder = deckTopSortingOrder;
            }
        }

        [ContextMenu("Reset And Prepare Deck")]
        public void ResetAndPrepareDeck()
        {
            ResetAndPrepareDeck(testDeckCount);
        }

        public void ResetAndPrepareDeck(int count)
        {
            StopAndClear();
            count = Mathf.Max(0, count);

            if (count == 0 || !TryBuildDeckLayout(out deckLayout))
            {
                return;
            }

            deckLayoutReady = true;
            nextDealOrderIndex = 0;

            GameObject prototype = ResolveCardPrototype();
            if (prototype == null)
            {
                Debug.LogWarning($"{nameof(TableNineBoardDealFillPlayer)} on '{name}' could not resolve card prototype.", this);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 deckPos = ResolveDeckWorldPosition(i, count);
                GameObject card = Instantiate(prototype, deckPos, Quaternion.identity, ResolvedContainer);
                card.name = $"DeckCard_{i}";
                card.SetActive(true);
                card.transform.position = deckPos;
                ApplySortingOrder(card, ResolveDeckSortingOrder(i, count));
                deckCards.Add(card);
            }
        }

        [ContextMenu("Play Deal One")]
        public void PlayDealOne()
        {
            EnsureDeckPrepared();
            ExecuteDealStep();
        }

        [ContextMenu("Play Deal All")]
        public void PlayDealAll()
        {
            EnsureDeckPrepared();

            if (batchSequence != null && batchSequence.IsActive())
            {
                batchSequence.Kill();
            }

            int steps = Mathf.Min(deckCards.Count, GetRemainingDealSlotCount());
            if (steps <= 0)
            {
                return;
            }

            batchSequence = DOTween.Sequence().SetTarget(this);
            ApplyTweenUpdate(batchSequence);

            for (int i = 0; i < steps; i++)
            {
                batchSequence.AppendCallback(ExecuteDealStep);
                if (i < steps - 1)
                {
                    batchSequence.AppendInterval(dealStepInterval);
                }
            }
        }

        [ContextMenu("Stop And Clear")]
        public void StopAndClear()
        {
            if (batchSequence != null && batchSequence.IsActive())
            {
                batchSequence.Kill();
            }

            batchSequence = null;
            DOTween.Kill(this);

            DestroyCards(deckCards);
            foreach (KeyValuePair<int, GameObject> pair in boardCards)
            {
                DestroyCard(pair.Value);
            }

            boardCards.Clear();
            slotAnchors.Clear();
            nextDealOrderIndex = 0;
            deckLayoutReady = false;
        }

        public void SetTestDeckCount(int count)
        {
            testDeckCount = Mathf.Max(0, count);
        }

        private void EnsureDeckPrepared()
        {
            if (deckCards.Count > 0)
            {
                return;
            }

            ResetAndPrepareDeck(testDeckCount);
        }

        private void ExecuteDealStep()
        {
            if (deckCards.Count == 0 || !TryGetNextTargetSlot(out int slotIndex, out Transform slotAnchor))
            {
                return;
            }

            if (!deckLayoutReady && !TryBuildDeckLayout(out deckLayout))
            {
                return;
            }

            deckLayoutReady = true;

            GameObject card = deckCards[0];
            deckCards.RemoveAt(0);

            if (card == null)
            {
                ShiftDeckCards(deckCards.Count);
                return;
            }

            Transform cardTransform = card.transform;
            Vector3 targetWorld = slotAnchor.position;

            ApplySortingOrder(card, flyingSortingOrder);

            Tween moveTween = dealArcHeight > Mathf.Epsilon
                ? cardTransform
                    .DOJump(targetWorld, dealArcHeight, 1, dealDuration)
                    .SetEase(dealEase)
                    .SetTarget(this)
                : cardTransform
                    .DOMove(targetWorld, dealDuration)
                    .SetEase(dealEase)
                    .SetTarget(this);

            ApplyTweenUpdate(moveTween);

            moveTween.OnComplete(() =>
            {
                ApplySortingOrder(card, boardSortingOrder);
                PlayLandPunch(cardTransform);
            });

            boardCards[slotIndex] = card;
            ShiftDeckCards(deckCards.Count);
        }

        private void ShiftDeckCards(int totalInDeck)
        {
            for (int i = 0; i < deckCards.Count; i++)
            {
                GameObject card = deckCards[i];
                if (card == null)
                {
                    continue;
                }

                Vector3 target = ResolveDeckWorldPosition(i, totalInDeck);
                ApplySortingOrder(card, ResolveDeckSortingOrder(i, totalInDeck));

                Tween shiftTween = card.transform
                    .DOMove(target, deckShiftDuration)
                    .SetEase(deckShiftEase)
                    .SetTarget(this);

                ApplyTweenUpdate(shiftTween);
            }
        }

        private void PlayLandPunch(Transform cardTransform)
        {
            Vector3 baseScale = cardTransform.localScale;
            Sequence punch = DOTween.Sequence().SetTarget(this);
            ApplyTweenUpdate(punch);

            punch.Append(cardTransform.DOScale(baseScale * dealLandScalePunch, dealLandScaleDuration * 0.45f).SetEase(Ease.OutQuad));
            punch.Append(cardTransform.DOScale(baseScale, dealLandScaleDuration * 0.55f).SetEase(Ease.InOutSine));
        }

        private bool TryGetNextTargetSlot(out int slotIndex, out Transform slotAnchor)
        {
            slotIndex = 0;
            slotAnchor = null;

            if (ResolvedDealOrder.Length == 0)
            {
                return false;
            }

            while (nextDealOrderIndex < ResolvedDealOrder.Length)
            {
                int candidate = ResolvedDealOrder[nextDealOrderIndex];
                nextDealOrderIndex++;

                if (candidate == 5 || boardCards.ContainsKey(candidate))
                {
                    continue;
                }

                if (!TryResolveSlotAnchor(candidate, out slotAnchor))
                {
                    continue;
                }

                slotIndex = candidate;
                return true;
            }

            return false;
        }

        private int GetRemainingDealSlotCount()
        {
            int remaining = 0;
            int[] order = ResolvedDealOrder;
            for (int i = nextDealOrderIndex; i < order.Length; i++)
            {
                int slot = order[i];
                if (slot != 5 && !boardCards.ContainsKey(slot))
                {
                    remaining++;
                }
            }

            return remaining;
        }

        private bool TryResolveSlotAnchor(int slotIndex, out Transform anchor)
        {
            if (slotAnchors.TryGetValue(slotIndex, out anchor) && anchor != null)
            {
                return true;
            }

            Transform root = boardRoot != null ? boardRoot : transform;
            string[] candidateNames =
            {
                $"slot{slotIndex}",
                $"slot{slotIndex}_Player",
            };

            for (int i = 0; i < candidateNames.Length; i++)
            {
                Transform found = root.Find(candidateNames[i]);
                if (found != null)
                {
                    slotAnchors[slotIndex] = found;
                    anchor = found;
                    return true;
                }
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string childName = child.name.Trim();
                if (childName.StartsWith($"slot{slotIndex}", StringComparison.OrdinalIgnoreCase))
                {
                    slotAnchors[slotIndex] = child;
                    anchor = child;
                    return true;
                }
            }

            anchor = null;
            return false;
        }

        private bool TryBuildDeckLayout(out DeckSpreadLayout spreadLayout)
        {
            spreadLayout = default;

            Transform root = deckRoot != null ? deckRoot : transform;
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
                Debug.LogWarning($"{nameof(TableNineBoardDealFillPlayer)} on '{name}' needs deck anchor '0'.", this);
                return false;
            }

            if (cardPrototype == null)
            {
                cardPrototype = slotZero.gameObject;
            }

            Vector3 originLocal = slotZero.localPosition;
            Vector3 spreadStepLocal = slotOne != null
                ? slotOne.localPosition - originLocal
                : Vector3.right * 0.125f;

            float spacing = spreadStepLocal.magnitude;
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

            Transform root = deckRoot != null ? deckRoot : transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && int.TryParse(child.name.Trim(), out int slotIndex) && slotIndex == 0)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private Vector3 ResolveDeckWorldPosition(int deckIndex, int totalInDeck)
        {
            float spacing = ResolveAdaptiveSpacing(totalInDeck);
            return deckLayout.OriginWorld + deckLayout.SpreadAxisWorld * (spacing * deckIndex);
        }

        private int ResolveDeckSortingOrder(int deckIndex, int totalInDeck)
        {
            if (totalInDeck <= 1)
            {
                return deckTopSortingOrder;
            }

            float t = deckIndex / (float)(totalInDeck - 1);
            return Mathf.RoundToInt(Mathf.Lerp(deckTopSortingOrder, deckBottomSortingOrder, t));
        }

        private float ResolveAdaptiveSpacing(int totalInDeck)
        {
            if (totalInDeck <= 1)
            {
                return 0f;
            }

            if (deckLayout.VisibleCapacity <= 2 || deckLayout.MaxVisibleSpan <= deckLayout.BaseSpacing + Mathf.Epsilon || totalInDeck >= deckLayout.VisibleCapacity)
            {
                return deckLayout.BaseSpacing;
            }

            float expandedSpacing = deckLayout.MaxVisibleSpan / (totalInDeck - 1);
            float smallCountBlend = 1f - Mathf.InverseLerp(2f, deckLayout.VisibleCapacity, totalInDeck);
            smallCountBlend *= smallCountBlend;
            return Mathf.Lerp(deckLayout.BaseSpacing, expandedSpacing, smallCountBlend);
        }

        private void ApplyTweenUpdate(Tween tween)
        {
            if (ignoreTimeScale)
            {
                tween.SetUpdate(true);
            }
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

        private static void DestroyCards(List<GameObject> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                DestroyCard(cards[i]);
            }

            cards.Clear();
        }

        private static void DestroyCard(GameObject card)
        {
            if (card == null)
            {
                return;
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
    }
}
