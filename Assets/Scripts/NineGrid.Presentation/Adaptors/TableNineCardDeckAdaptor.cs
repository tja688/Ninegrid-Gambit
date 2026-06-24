using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Core;
using NineGrid.Presentation.FSM;
using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Registry;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 牌组适配器：认领 Spawn/Deal/FillSlots，调度牌堆入场、开局发牌、单张补牌与声明式补位重布局。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineCardDeckAdaptor : MonoBehaviour, IController
    {
        public enum DeckAdaptorPhase
        {
            Idle,
            Entering,
            Relayouting,
            Dealing,
            Substituting,
        }

        private static readonly HashSet<PresentationInstructionKind> HandledKinds = new()
        {
            PresentationInstructionKind.SpawnCard,
            PresentationInstructionKind.DealCard,
            PresentationInstructionKind.FillSlots,
            PresentationInstructionKind.ShowDrawPileExhausted,
        };

        [Header("Registry")]
        [SerializeField] private TableNineViewRegistry viewRegistry;
        [SerializeField] private TableNineActorFactory actorFactory;

        [Header("Layout")]
        [SerializeField] private Transform deckAnchorRoot;
        [SerializeField] private CardDeckLayoutSolver deckLayoutSolver = new();

        [Header("Performances")]
        [SerializeField] private CardDeckEntryPerformance entryPerformance;
        [SerializeField] private CardDeckDealCardsPerformance dealPerformance;
        [SerializeField] private CardDeckSubstitutePerformance substitutePerformance;

        [Header("Relayout Motion")]
        [SerializeField, Min(0.01f)] private float relayoutDuration = 0.3f;
        [SerializeField] private Ease relayoutEase = Ease.OutQuint;

        private readonly List<int> mDeckCardUids = new();
        private readonly List<Transform> mActorBuffer = new();
        private readonly List<Transform> mSlotBuffer = new();
        private readonly List<DeckCardLayoutTarget> mLayoutBuffer = new();
        private readonly List<int> mReconstructionBuffer = new();

        private DeckAdaptorPhase mPhase = DeckAdaptorPhase.Idle;

        public TableNineViewRegistry ViewRegistry => viewRegistry;
        public IReadOnlyList<int> DeckCardUids => mDeckCardUids;
        public DeckAdaptorPhase Phase => mPhase;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        public bool CanHandle(PresentationInstruction instruction)
        {
            return instruction != null && HandledKinds.Contains(instruction.Kind);
        }

        public IEnumerator PlayInstruction(
            PresentationInstruction instruction,
            IReadOnlyList<PresentationInstruction> batchInstructions,
            DeckBatchPlan plan,
            int instructionIndex,
            CoreViewSnapshot snapshot)
        {
            if (!CanHandle(instruction))
            {
                yield break;
            }

            EnsureReferences();
            CoreGameEvent evt = instruction.Event;
            if (evt == null)
            {
                yield break;
            }

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.SpawnCard:
                    yield return PlaySpawnCard(evt, snapshot);
                    break;
                case PresentationInstructionKind.DealCard:
                    if (DeckBatchPlan.IsOpeningDeal(evt))
                    {
                        yield return PlayOpeningDeckEntry(batchInstructions, plan, snapshot);
                        break;
                    }

                    if (plan != null && plan.ShouldSkipDeal(evt, instructionIndex))
                    {
                        yield break;
                    }

                    if (plan != null && plan.IsBatchDealAction(evt.ActionId))
                    {
                        yield return PlayBatchDeal(evt.ActionId, plan, snapshot);
                    }
                    else
                    {
                        yield return PlaySingleSubstitute(evt, snapshot);
                    }

                    break;
                case PresentationInstructionKind.FillSlots:
                    yield return PlayDeckRelayout();
                    break;
                case PresentationInstructionKind.ShowDrawPileExhausted:
                    yield break;
            }
        }

        public void AlignDeckFromSnapshot(CoreViewSnapshot snapshot)
        {
            EnsureReferences();
            RebuildDeckMembership(snapshot, null, null);
            ApplyInstantLayout();
        }

        private IEnumerator PlayOpeningDeckEntry(
            IReadOnlyList<PresentationInstruction> batchInstructions,
            DeckBatchPlan plan,
            CoreViewSnapshot snapshot)
        {
            RebuildDeckMembership(snapshot, batchInstructions, plan);

            for (var i = 0; i < mDeckCardUids.Count; i++)
            {
                EnsureDeckActor(mDeckCardUids[i], snapshot);
            }

            if (mDeckCardUids.Count == 0)
            {
                yield break;
            }

            mActorBuffer.Clear();
            for (var i = 0; i < mDeckCardUids.Count; i++)
            {
                Transform actor;
                if (viewRegistry.TryGetActor(mDeckCardUids[i], out actor) && actor != null)
                {
                    mActorBuffer.Add(actor);
                }
            }

            if (mActorBuffer.Count == 0)
            {
                yield break;
            }

            Vector3 deckOrigin = ResolveDeckOriginWorldPosition();
            for (var i = 0; i < mActorBuffer.Count; i++)
            {
                mActorBuffer[i].position = deckOrigin;
            }

            mPhase = DeckAdaptorPhase.Entering;
            if (entryPerformance == null)
            {
                ApplyInstantLayout();
                mPhase = DeckAdaptorPhase.Idle;
                yield break;
            }

            entryPerformance.Play(mActorBuffer);
            yield return WaitWhilePlaying(() => entryPerformance.IsPlaying, entryPerformance.TotalDuration);

            ApplyInstantLayout();
            mPhase = DeckAdaptorPhase.Idle;
        }

        private IEnumerator PlaySpawnCard(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            if (!IsDrawPileSpawn(evt, snapshot))
            {
                yield break;
            }

            EnsureDeckActor(evt.CardUid, snapshot);
            if (!mDeckCardUids.Contains(evt.CardUid))
            {
                mDeckCardUids.Add(evt.CardUid);
            }

            yield return PlayDeckRelayout();
        }

        private IEnumerator PlayBatchDeal(int actionId, DeckBatchPlan plan, CoreViewSnapshot snapshot)
        {
            IReadOnlyList<DeckBatchPlan.DealEntry> group = plan.GetDealGroup(actionId);
            if (group == null || group.Count == 0)
            {
                yield break;
            }

            mActorBuffer.Clear();
            mSlotBuffer.Clear();

            for (var i = 0; i < group.Count; i++)
            {
                DeckBatchPlan.DealEntry entry = group[i];
                Transform actor = EnsureDeckActor(entry.CardUid, snapshot);
                Transform slot;
                if (!viewRegistry.TryGetSlotAnchor(entry.ToSlot, out slot) || slot == null)
                {
                    continue;
                }

                mActorBuffer.Add(actor);
                mSlotBuffer.Add(slot);
                RemoveFromDeck(entry.CardUid);
                viewRegistry.RegisterActor(entry.CardUid, actor, ViewActorZone.Board);
            }

            if (mActorBuffer.Count == 0)
            {
                yield break;
            }

            mPhase = DeckAdaptorPhase.Dealing;
            if (dealPerformance == null)
            {
                mPhase = DeckAdaptorPhase.Idle;
                yield break;
            }

            dealPerformance.Play(mActorBuffer, mSlotBuffer);
            yield return WaitWhilePlaying(() => dealPerformance.IsPlaying, dealPerformance.TotalDuration);
            mPhase = DeckAdaptorPhase.Idle;
        }

        private IEnumerator PlaySingleSubstitute(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            if (evt.CardUid <= 0)
            {
                yield break;
            }

            Transform actor = EnsureDeckActor(evt.CardUid, snapshot);
            Transform slot;
            if (!viewRegistry.TryGetSlotAnchor(evt.ToSlot, out slot) || slot == null)
            {
                yield break;
            }

            RemoveFromDeck(evt.CardUid);
            viewRegistry.RegisterActor(evt.CardUid, actor, ViewActorZone.Board);

            mPhase = DeckAdaptorPhase.Substituting;
            if (substitutePerformance == null)
            {
                mPhase = DeckAdaptorPhase.Idle;
                yield break;
            }

            substitutePerformance.Play(actor, slot);
            yield return WaitWhilePlaying(() => substitutePerformance.IsPlaying, substitutePerformance.TotalDuration);
            mPhase = DeckAdaptorPhase.Idle;
        }

        private IEnumerator PlayDeckRelayout()
        {
            if (mDeckCardUids.Count == 0)
            {
                yield break;
            }

            deckLayoutSolver.BuildLayout(mDeckCardUids.Count, mLayoutBuffer);
            if (mLayoutBuffer.Count == 0)
            {
                yield break;
            }

            var sequence = DOTween.Sequence().SetTarget(this);
            mPhase = DeckAdaptorPhase.Relayouting;

            for (var i = 0; i < mDeckCardUids.Count; i++)
            {
                int uid = mDeckCardUids[i];
                Transform actor;
                if (!viewRegistry.TryGetActor(uid, out actor) || actor == null)
                {
                    continue;
                }

                DeckCardLayoutTarget target = mLayoutBuffer[i];
                SelectionOptionVisual.ApplySortingOrder(actor, target.SortingOrder);
                sequence.Join(actor.DOMove(target.WorldPosition, relayoutDuration).SetEase(relayoutEase));
            }

            if (!sequence.IsActive())
            {
                mPhase = DeckAdaptorPhase.Idle;
                yield break;
            }

            yield return sequence.WaitForCompletion();
            mPhase = DeckAdaptorPhase.Idle;
        }

        private void ApplyInstantLayout()
        {
            deckLayoutSolver.BuildLayout(mDeckCardUids.Count, mLayoutBuffer);
            for (var i = 0; i < mDeckCardUids.Count; i++)
            {
                int uid = mDeckCardUids[i];
                Transform actor;
                if (!viewRegistry.TryGetActor(uid, out actor) || actor == null)
                {
                    continue;
                }

                DeckCardLayoutTarget target = mLayoutBuffer[i];
                actor.position = target.WorldPosition;
                SelectionOptionVisual.ApplySortingOrder(actor, target.SortingOrder);
                viewRegistry.RegisterActor(uid, actor, ViewActorZone.Deck);
            }
        }

        private void RebuildDeckMembership(
            CoreViewSnapshot snapshot,
            IReadOnlyList<PresentationInstruction> batchInstructions,
            DeckBatchPlan plan)
        {
            var deck = this.GetModel<DeckModel>();
            mDeckCardUids.Clear();

            if (plan != null && batchInstructions != null)
            {
                plan.CollectDealtUidsInOrder(batchInstructions, mReconstructionBuffer);
                for (var i = 0; i < mReconstructionBuffer.Count; i++)
                {
                    int uid = mReconstructionBuffer[i];
                    if (!mDeckCardUids.Contains(uid))
                    {
                        mDeckCardUids.Add(uid);
                    }
                }
            }

            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                int uid = deck.DrawPileUids[i];
                if (!mDeckCardUids.Contains(uid))
                {
                    mDeckCardUids.Add(uid);
                }
            }
        }

        private Transform EnsureDeckActor(int cardUid, CoreViewSnapshot snapshot)
        {
            EnsureActorFactory();
            if (actorFactory != null)
            {
                return actorFactory.Acquire(cardUid, ViewActorZone.Deck, GetArchitecture(), snapshot);
            }

            Transform actor;
            if (viewRegistry.TryGetActor(cardUid, out actor) && actor != null)
            {
                return actor;
            }

            actor = SelectionOptionVisual.CreatePreviewCard(
                viewRegistry.ActorLayer,
                mDeckCardUids.Count,
                null,
                Vector3.zero,
                0f,
                deckLayoutSolver.BaseSortingOrder);
            actor.name = $"DeckCard_{cardUid}";
            viewRegistry.RegisterActor(cardUid, actor, ViewActorZone.Deck);
            return actor;
        }

        private void EnsureActorFactory()
        {
            if (actorFactory == null)
            {
                actorFactory = GetComponent<TableNineActorFactory>();
                if (actorFactory == null)
                {
                    actorFactory = GetComponentInParent<TableNineActorFactory>();
                }
            }
        }

        private void RemoveFromDeck(int cardUid)
        {
            mDeckCardUids.Remove(cardUid);
        }

        private Vector3 ResolveDeckOriginWorldPosition()
        {
            if (substitutePerformance != null)
            {
                return substitutePerformance.ResolveDeckWorldPosition(viewRegistry.ActorLayer);
            }

            return viewRegistry.ActorLayer.position;
        }

        private static bool IsDrawPileSpawn(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            if (evt == null || evt.CardUid <= 0)
            {
                return false;
            }

            if (evt.ToSlot.IsBoardSlot)
            {
                return false;
            }

            return FindSlotForUid(snapshot, evt.CardUid) == SlotId.None;
        }

        private static SlotId FindSlotForUid(CoreViewSnapshot snapshot, int cardUid)
        {
            if (snapshot == null || cardUid <= 0)
            {
                return SlotId.None;
            }

            for (var i = 0; i < snapshot.BoardSlots.Count; i++)
            {
                BoardSlotView slotView = snapshot.BoardSlots[i];
                if (slotView.CardUid == cardUid)
                {
                    return slotView.Slot;
                }
            }

            return SlotId.None;
        }

        private void EnsureReferences()
        {
            if (viewRegistry == null)
            {
                viewRegistry = GetComponentInParent<TableNineViewRegistry>();
            }

            if (entryPerformance == null)
            {
                entryPerformance = GetComponentInChildren<CardDeckEntryPerformance>(true);
            }

            if (dealPerformance == null)
            {
                dealPerformance = GetComponentInChildren<CardDeckDealCardsPerformance>(true);
            }

            if (substitutePerformance == null)
            {
                substitutePerformance = GetComponentInChildren<CardDeckSubstitutePerformance>(true);
            }

            if (deckAnchorRoot != null)
            {
                deckLayoutSolver.ResolveFromReferenceAnchors(deckAnchorRoot);
            }
            else if (entryPerformance != null)
            {
                deckLayoutSolver.ResolveFromReferenceAnchors(entryPerformance.transform);
            }
        }

        private static IEnumerator WaitWhilePlaying(Func<bool> isPlaying, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (isPlaying() && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            mPhase = DeckAdaptorPhase.Idle;
        }
    }
}
