using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Core;
using NineGrid.Presentation.Diagnostics;
using NineGrid.Presentation.FSM;
using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Registry;
using UnityEngine;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 场地适配器：认领 Move/Swap/Rotate/Damage/Kill/Remove/Mark，
    /// 调度已落地的棋盘战斗表演黑盒。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineBoardAdaptor : MonoBehaviour
    {
        private static readonly HashSet<PresentationInstructionKind> HandledKinds = new()
        {
            PresentationInstructionKind.MoveCard,
            PresentationInstructionKind.SwapCards,
            PresentationInstructionKind.RotateBoard,
            PresentationInstructionKind.ShowDamage,
            PresentationInstructionKind.KillCard,
            PresentationInstructionKind.RemoveCard,
            PresentationInstructionKind.MarkBoard,
        };

        [Header("Registry")]
        [SerializeField] private TableNineViewRegistry viewRegistry;
        [SerializeField] private TableNineActorFactory actorFactory;

        [Header("Performances")]
        [SerializeField] private BoardRotatePerformance boardRotatePerformance;
        [SerializeField] private CardAttackPerformance cardAttackPerformance;
        [SerializeField] private CardKillPerformance cardKillPerformance;
        [SerializeField] private CounterattackPerformance counterattackPerformance;
        [SerializeField] private CounterattackKillPerformance counterattackKillPerformance;
        [SerializeField] private CardShakePerformance cardShakePerformance;

        [Header("Simple Motion")]
        [SerializeField, Min(0.01f)] private float cardMoveDuration = 0.35f;
        [SerializeField] private Ease cardMoveEase = Ease.OutQuad;
        [SerializeField, Min(0.01f)] private float cardRemoveFadeDuration = 0.25f;

        private readonly List<Transform> mActorBuffer = new();
        private readonly List<Transform> mTargetBuffer = new();
        public TableNineViewRegistry ViewRegistry => viewRegistry;

        public bool CanHandle(PresentationInstruction instruction)
        {
            return instruction != null && HandledKinds.Contains(instruction.Kind);
        }

        public IEnumerator PlayInstruction(
            PresentationInstruction instruction,
            IReadOnlyList<PresentationInstruction> batchInstructions,
            BoardBatchPlan plan,
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
                case PresentationInstructionKind.MoveCard:
                    if (plan != null && plan.ShouldSkipMove(evt))
                    {
                        yield break;
                    }

                    yield return PlayMoveCard(evt, snapshot);
                    break;
                case PresentationInstructionKind.SwapCards:
                    yield return PlaySwapCards(evt, snapshot);
                    break;
                case PresentationInstructionKind.RotateBoard:
                    yield return PlayRotateBoard(evt, snapshot);
                    break;
                case PresentationInstructionKind.ShowDamage:
                    if (plan != null && plan.ShouldSkipDamage(evt))
                    {
                        yield break;
                    }

                    yield return PlayShowDamage(evt, snapshot);
                    break;
                case PresentationInstructionKind.KillCard:
                    yield return PlayKillCard(evt, snapshot);
                    break;
                case PresentationInstructionKind.RemoveCard:
                    if (plan != null && plan.ShouldSkipRemove(evt))
                    {
                        yield break;
                    }

                    yield return PlayRemoveCard(evt);
                    break;
                case PresentationInstructionKind.MarkBoard:
                    yield return PlayMarkBoard(evt, snapshot);
                    break;
            }
        }

        private IEnumerator PlayMoveCard(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            Transform actor;
            if (!viewRegistry.TryGetActor(evt.CardUid, out actor) || actor == null)
            {
                yield break;
            }

            Transform anchor;
            if (!viewRegistry.TryGetSlotAnchor(evt.ToSlot, out anchor) || anchor == null)
            {
                yield break;
            }

            Tween tween = actor
                .DOMove(anchor.position, cardMoveDuration)
                .SetEase(cardMoveEase)
                .SetTarget(actor);
            yield return tween.WaitForCompletion();
        }

        private IEnumerator PlaySwapCards(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            Transform leftAnchor;
            Transform rightAnchor;

            if (!viewRegistry.TryGetSlotAnchor(evt.FromSlot, out leftAnchor)
                || !viewRegistry.TryGetSlotAnchor(evt.ToSlot, out rightAnchor)
                || leftAnchor == null
                || rightAnchor == null)
            {
                yield break;
            }

            int uidEndsAtFrom = FindCardUidAtSlot(snapshot, evt.FromSlot);
            int uidEndsAtTo = FindCardUidAtSlot(snapshot, evt.ToSlot);

            Transform actorEndsAtFrom = null;
            Transform actorEndsAtTo = null;
            bool hasFrom = uidEndsAtFrom > 0
                && viewRegistry.TryGetActor(uidEndsAtFrom, out actorEndsAtFrom)
                && actorEndsAtFrom != null;
            bool hasTo = uidEndsAtTo > 0
                && viewRegistry.TryGetActor(uidEndsAtTo, out actorEndsAtTo)
                && actorEndsAtTo != null;

            if (!hasFrom && !hasTo)
            {
                yield break;
            }

            var sequence = DOTween.Sequence().SetTarget(this);
            if (hasFrom)
            {
                actorEndsAtFrom.position = rightAnchor.position;
                sequence.Join(actorEndsAtFrom.DOMove(leftAnchor.position, cardMoveDuration).SetEase(cardMoveEase));
            }

            if (hasTo)
            {
                actorEndsAtTo.position = leftAnchor.position;
                sequence.Join(actorEndsAtTo.DOMove(rightAnchor.position, cardMoveDuration).SetEase(cardMoveEase));
            }

            yield return sequence.WaitForCompletion();
        }

        private IEnumerator PlayRotateBoard(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            mActorBuffer.Clear();
            mTargetBuffer.Clear();

            IReadOnlyList<SlotId> ring = viewRegistry.BoardRingPathSlots;
            bool clockwise = evt.Amount >= 0;
            int ringCount = ring.Count;
            var ringAnchors = new Transform[ringCount];

            for (var i = 0; i < ringCount; i++)
            {
                Transform anchor;
                if (viewRegistry.TryGetSlotAnchor(ring[i], out anchor) && anchor != null)
                {
                    ringAnchors[i] = anchor;
                }
            }

            for (var i = 0; i < ringCount; i++)
            {
                Transform anchor = ringAnchors[i];
                if (anchor == null)
                {
                    continue;
                }

                int sourceRingIndex = clockwise
                    ? (i + 1) % ringCount
                    : (i + ringCount - 1) % ringCount;
                int uid = FindCardUidAtSlot(snapshot, ring[sourceRingIndex]);

                Transform actor;
                if (uid <= 0 || !viewRegistry.TryGetActor(uid, out actor) || actor == null)
                {
                    continue;
                }

                int targetRingIndex = clockwise
                    ? (i + 1) % ringCount
                    : (i + ringCount - 1) % ringCount;
                Transform targetAnchor = ringAnchors[targetRingIndex];
                if (targetAnchor == null)
                {
                    continue;
                }

                actor.position = anchor.position;
                mActorBuffer.Add(actor);
                mTargetBuffer.Add(targetAnchor);
            }

            if (mActorBuffer.Count == 0)
            {
                yield break;
            }

            boardRotatePerformance.Play(mActorBuffer, mTargetBuffer);
            yield return WaitWhilePlaying(() => boardRotatePerformance.IsPlaying, boardRotatePerformance.TotalDuration);
        }

        private IEnumerator PlayShowDamage(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            Transform actor;
            Transform target;
            if (!TryResolveCombatPair(evt, snapshot, out actor, out target))
            {
                if (viewRegistry.TryGetActor(evt.TargetUid, out target) && target != null)
                {
                    EnsureShakePerformance(target);
                    cardShakePerformance.StartShake();
                    yield return WaitWhilePlaying(() => cardShakePerformance.IsShaking, 0.5f);
                }

                yield break;
            }

            CardBattleDirection boardDirection = ResolveBoardDirection(
                snapshot,
                evt.ActorUid,
                evt.TargetUid,
                evt.FromSlot);
            bool actorIsAvatar = evt.ActorUid == snapshot.AvatarUid;

            if (actorIsAvatar)
            {
                cardAttackPerformance.Play(actor, target, boardDirection);
                yield return WaitWhilePlaying(() => cardAttackPerformance.IsPlaying, cardAttackPerformance.TotalDuration);
            }
            else
            {
                counterattackPerformance.Play(actor, target, boardDirection);
                yield return WaitWhilePlaying(() => counterattackPerformance.IsPlaying, counterattackPerformance.TotalDuration);
            }
        }

        private IEnumerator PlayKillCard(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            Transform killer;
            Transform target;
            if (!TryResolveCombatPair(evt, snapshot, out killer, out target))
            {
                yield break;
            }

            CardBattleDirection boardDirection = ResolveBoardDirection(
                snapshot,
                evt.ActorUid,
                evt.TargetUid,
                evt.FromSlot);
            bool targetIsAvatar = evt.TargetUid == snapshot.AvatarUid;

            if (targetIsAvatar)
            {
                counterattackKillPerformance.Play(killer, target, boardDirection);
                yield return WaitWhilePlaying(() => counterattackKillPerformance.IsPlaying, counterattackKillPerformance.TotalDuration);
            }
            else
            {
                cardKillPerformance.Play(killer, target, boardDirection);
                yield return WaitWhilePlaying(() => cardKillPerformance.IsPlaying, cardKillPerformance.TotalDuration);
            }

            ReleaseActor(evt.TargetUid);
        }

        private IEnumerator PlayRemoveCard(CoreGameEvent evt)
        {
            Transform actor;
            if (!viewRegistry.TryGetActor(evt.CardUid, out actor) || actor == null)
            {
                yield break;
            }

            SpriteRenderer[] renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                ReleaseActor(evt.CardUid);
                yield break;
            }

            var sequence = DOTween.Sequence().SetTarget(this);
            for (var i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color baseColor = renderer.color;
                sequence.Join(DOTween.To(
                    () => renderer != null ? renderer.color.a : 1f,
                    alpha =>
                    {
                        if (renderer != null)
                        {
                            Color color = baseColor;
                            color.a = alpha;
                            renderer.color = color;
                        }
                    },
                    0f,
                    cardRemoveFadeDuration));
            }

            yield return sequence.WaitForCompletion();
            ReleaseActor(evt.CardUid);
        }

        private void ReleaseActor(int cardUid)
        {
            EnsureActorFactory();
            if (actorFactory != null)
            {
                actorFactory.Release(cardUid);
                return;
            }

            Transform actor;
            if (viewRegistry.TryGetActor(cardUid, out actor) && actor != null)
            {
                actor.gameObject.SetActive(false);
            }

            viewRegistry.UnregisterActor(cardUid);
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

        private IEnumerator PlayMarkBoard(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            // 标记尚无独立表演黑盒：批内即时对齐槽位权威态。
            viewRegistry.AlignBoardFromSnapshot(snapshot);
            yield break;
        }

        private bool TryResolveCombatPair(
            CoreGameEvent evt,
            CoreViewSnapshot snapshot,
            out Transform actor,
            out Transform target)
        {
            actor = null;
            target = null;

            if (evt.ActorUid > 0 && viewRegistry.TryGetActor(evt.ActorUid, out actor) && actor != null
                && evt.TargetUid > 0 && viewRegistry.TryGetActor(evt.TargetUid, out target) && target != null)
            {
                return true;
            }

            if (evt.ActorUid == snapshot.AvatarUid)
            {
                viewRegistry.TryGetAvatarActor(snapshot, out actor);
            }

            if (evt.TargetUid > 0)
            {
                viewRegistry.TryGetActor(evt.TargetUid, out target);
            }

            return actor != null && target != null;
        }

        private static CardBattleDirection ResolveBoardDirection(
            CoreViewSnapshot snapshot,
            int actorUid,
            int targetUid,
            SlotId targetFallbackSlot = default)
        {
            SlotId actorSlot = FindSlotForUid(snapshot, actorUid);
            SlotId targetSlot = FindSlotForUid(snapshot, targetUid);
            if (!targetSlot.IsBoardSlot && targetFallbackSlot.IsBoardSlot)
            {
                targetSlot = targetFallbackSlot;
            }

            if (!actorSlot.IsBoardSlot && snapshot != null && snapshot.AvatarUid == actorUid)
            {
                actorSlot = snapshot.AvatarSlot;
            }

            if (!actorSlot.IsBoardSlot || !targetSlot.IsBoardSlot)
            {
                return CardBattleDirection.Right;
            }

            Vector2 delta = ResolveSlotWorldDelta(actorSlot, targetSlot);
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? CardBattleDirection.Right : CardBattleDirection.Left;
            }

            return delta.y >= 0f ? CardBattleDirection.Up : CardBattleDirection.Down;
        }

        private static Vector2 ResolveSlotWorldDelta(SlotId from, SlotId to)
        {
            int rowDelta = to.Row - from.Row;
            int colDelta = to.Column - from.Column;
            return new Vector2(colDelta, -rowDelta);
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

            if (snapshot.AvatarUid == cardUid)
            {
                return snapshot.AvatarSlot;
            }

            return SlotId.None;
        }

        private static int FindCardUidAtSlot(CoreViewSnapshot snapshot, SlotId slot)
        {
            if (snapshot == null || !slot.IsBoardSlot)
            {
                return 0;
            }

            BoardSlotView slotView = snapshot.GetSlot(slot);
            return slotView != null ? slotView.CardUid : 0;
        }

        private void EnsureShakePerformance(Transform target)
        {
            if (cardShakePerformance == null)
            {
                cardShakePerformance = GetComponent<CardShakePerformance>();
            }

            if (cardShakePerformance == null)
            {
                cardShakePerformance = gameObject.AddComponent<CardShakePerformance>();
            }
        }

        private void EnsureReferences()
        {
            if (viewRegistry == null)
            {
                viewRegistry = GetComponentInParent<TableNineViewRegistry>();
            }

            if (boardRotatePerformance == null)
            {
                boardRotatePerformance = GetComponent<BoardRotatePerformance>();
            }

            if (cardAttackPerformance == null)
            {
                cardAttackPerformance = GetComponent<CardAttackPerformance>();
            }

            if (cardKillPerformance == null)
            {
                cardKillPerformance = GetComponent<CardKillPerformance>();
            }

            if (counterattackPerformance == null)
            {
                counterattackPerformance = GetComponent<CounterattackPerformance>();
            }

            if (counterattackKillPerformance == null)
            {
                counterattackKillPerformance = GetComponent<CounterattackKillPerformance>();
            }
        }

        private static IEnumerator WaitWhilePlaying(Func<bool> isPlaying, float timeoutSeconds, string performanceName = "Unknown")
        {
            return AdaptorPlaybackTrace.WaitWhilePlaying(
                nameof(TableNineBoardAdaptor),
                performanceName,
                isPlaying,
                timeoutSeconds);
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds, string performanceName = "Unknown")
        {
            return AdaptorPlaybackTrace.WaitUntilOrTimeout(
                nameof(TableNineBoardAdaptor),
                performanceName,
                condition,
                timeoutSeconds);
        }
    }
}
