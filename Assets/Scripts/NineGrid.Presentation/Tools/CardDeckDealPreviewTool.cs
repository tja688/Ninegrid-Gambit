using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Performance;
using UnityEngine;

namespace NineGrid.Presentation.Tools
{
    /// <summary>
    /// 牌堆发牌/补牌预览壳：按 9 向外圈 8 槽发牌，按 0 单张补入下一空槽。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardDeckDealPreviewTool : MonoBehaviour
    {
        [Header("Hotkeys")]
        [SerializeField] private KeyCode dealKey = KeyCode.Alpha9;
        [SerializeField] private KeyCode substituteKey = KeyCode.Alpha0;

        [Header("Ring")]
        [SerializeField] private Transform ringSlotRoot;
        [SerializeField] private Transform[] ringSlots;
        [SerializeField] private int[] standardBoardRingIndices = { 0, 1, 2, 5, 8, 7, 6, 3 };
        [SerializeField, Min(1)] private int dealCardCount = 8;

        [Header("Preview Actors")]
        [SerializeField] private Transform previewActorsRoot;
        [SerializeField] private GameObject cardPreviewPrefab;
        [SerializeField] private int baseSortingOrder = 6;

        [Header("Performances")]
        [SerializeField] private CardDeckDealCardsPerformance dealPerformance;
        [SerializeField] private CardDeckSubstitutePerformance substitutePerformance;

        private readonly List<Transform> previewActors = new();
        private readonly List<Transform> resolvedRingSlots = new();
        private readonly List<int> occupiedSlotIndices = new();

        private Transform ResolvedActorsRoot => previewActorsRoot != null ? previewActorsRoot : transform;

        private void Awake()
        {
            EnsurePerformanceReferences();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(dealKey))
            {
                PlayDealStep();
                return;
            }

            if (Input.GetKeyDown(substituteKey))
            {
                PlaySubstituteStep();
            }
        }

        [ContextMenu("Play Deal Step (Same As Key 9)")]
        public void PlayDealStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(CardDeckDealPreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            if (IsAnyPerformancePlaying())
            {
                return;
            }

            EnsurePerformanceReferences();
            ClearPreviewActors(resetOccupancy: true);

            if (!TryResolveRingSlots())
            {
                Debug.LogWarning($"[{nameof(CardDeckDealPreviewTool)}] ring slots could not be resolved.", this);
                return;
            }

            int count = Mathf.Min(dealCardCount, resolvedRingSlots.Count);
            var cards = new List<Transform>(count);
            var slots = new List<Transform>(count);
            Transform parent = ResolvedActorsRoot;
            Vector3 deckPosition = substitutePerformance.ResolveDeckWorldPosition(parent);

            for (var i = 0; i < count; i++)
            {
                Transform slot = resolvedRingSlots[i];
                if (slot == null)
                {
                    continue;
                }

                Transform card = CreatePreviewActor(parent, previewActors.Count);
                card.position = deckPosition;
                previewActors.Add(card);
                cards.Add(card);
                slots.Add(slot);
                occupiedSlotIndices.Add(i);
            }

            dealPerformance.Play(cards, slots);
        }

        [ContextMenu("Play Substitute Step (Same As Key 0)")]
        public void PlaySubstituteStep()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(CardDeckDealPreviewTool)}] Enter Play Mode first.", this);
                return;
            }

            if (IsAnyPerformancePlaying())
            {
                return;
            }

            EnsurePerformanceReferences();

            if (!TryResolveRingSlots())
            {
                Debug.LogWarning($"[{nameof(CardDeckDealPreviewTool)}] ring slots could not be resolved.", this);
                return;
            }

            if (!TryFindNextEmptySlotIndex(out int slotIndex))
            {
                Debug.LogWarning($"[{nameof(CardDeckDealPreviewTool)}] no empty ring slot for substitute.", this);
                return;
            }

            Transform slot = resolvedRingSlots[slotIndex];
            Transform parent = ResolvedActorsRoot;
            Transform card = CreatePreviewActor(parent, previewActors.Count);
            card.position = substitutePerformance.ResolveDeckWorldPosition(parent);
            previewActors.Add(card);
            occupiedSlotIndices.Add(slotIndex);

            substitutePerformance.Play(card, slot);
        }

        [ContextMenu("Clear Preview Actors")]
        public void ClearPreviewActors()
        {
            ClearPreviewActors(resetOccupancy: true);
        }

        private void ClearPreviewActors(bool resetOccupancy)
        {
            dealPerformance?.StopAndRestore();
            substitutePerformance?.StopAndRestore();

            for (var i = previewActors.Count - 1; i >= 0; i--)
            {
                Transform actor = previewActors[i];
                if (actor == null)
                {
                    continue;
                }

                actor.DOKill();
                Destroy(actor.gameObject);
            }

            previewActors.Clear();

            if (resetOccupancy)
            {
                occupiedSlotIndices.Clear();
            }
        }

        private bool TryFindNextEmptySlotIndex(out int slotIndex)
        {
            for (var i = 0; i < resolvedRingSlots.Count; i++)
            {
                if (!occupiedSlotIndices.Contains(i))
                {
                    slotIndex = i;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        private bool IsAnyPerformancePlaying()
        {
            return (dealPerformance != null && dealPerformance.IsPlaying)
                || (substitutePerformance != null && substitutePerformance.IsPlaying);
        }

        private bool TryResolveRingSlots()
        {
            resolvedRingSlots.Clear();

            if (ringSlots != null && ringSlots.Length > 0)
            {
                for (var i = 0; i < ringSlots.Length; i++)
                {
                    if (ringSlots[i] != null)
                    {
                        resolvedRingSlots.Add(ringSlots[i]);
                    }
                }

                return resolvedRingSlots.Count > 0;
            }

            if (ringSlotRoot == null)
            {
                return false;
            }

            if (standardBoardRingIndices != null && standardBoardRingIndices.Length > 0)
            {
                for (var i = 0; i < standardBoardRingIndices.Length; i++)
                {
                    int childIndex = standardBoardRingIndices[i];
                    if (childIndex < 0 || childIndex >= ringSlotRoot.childCount)
                    {
                        continue;
                    }

                    resolvedRingSlots.Add(ringSlotRoot.GetChild(childIndex));
                }

                if (resolvedRingSlots.Count > 0)
                {
                    return true;
                }
            }

            for (var i = 0; i < ringSlotRoot.childCount; i++)
            {
                resolvedRingSlots.Add(ringSlotRoot.GetChild(i));
            }

            return resolvedRingSlots.Count > 0;
        }

        private Transform CreatePreviewActor(Transform parent, int index)
        {
            Transform actor = SelectionOptionVisual.CreatePreviewCard(
                parent,
                index,
                cardPreviewPrefab,
                Vector3.zero,
                0f,
                baseSortingOrder + index);
            actor.name = $"DeckDealPreview_{index + 1}";
            return actor;
        }

        private void EnsurePerformanceReferences()
        {
            if (substitutePerformance == null)
            {
                substitutePerformance = GetComponent<CardDeckSubstitutePerformance>();
            }

            if (dealPerformance == null)
            {
                dealPerformance = GetComponent<CardDeckDealCardsPerformance>();
            }

            if (substitutePerformance == null)
            {
                substitutePerformance = gameObject.AddComponent<CardDeckSubstitutePerformance>();
            }

            if (dealPerformance == null)
            {
                dealPerformance = gameObject.AddComponent<CardDeckDealCardsPerformance>();
            }
        }

        private void OnDestroy()
        {
            ClearPreviewActors(resetOccupancy: true);
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                dealPerformance?.StopAndRestore();
                substitutePerformance?.StopAndRestore();
            }
        }
    }
}
