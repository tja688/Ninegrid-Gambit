using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.FSM;
using UnityEngine;

namespace NineGrid.Presentation.Registry
{
    public enum ViewActorZone
    {
        Board,
        Hand,
        Other
    }

    /// <summary>
    /// CardUid → 演员、SlotId → 锚点解析；批末用 <see cref="CoreViewSnapshot"/> 对齐场地演员。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineViewRegistry : MonoBehaviour
    {
        private static readonly SlotId[] BoardRingPath =
        {
            SlotId.Board(1),
            SlotId.Board(2),
            SlotId.Board(3),
            SlotId.Board(6),
            SlotId.Board(9),
            SlotId.Board(8),
            SlotId.Board(7),
            SlotId.Board(4)
        };

        [Header("Anchors")]
        [SerializeField] private Transform boardSlotRoot;
        [SerializeField] private Transform[] boardSlotAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform avatarAnchor;

        [Header("Actors")]
        [SerializeField] private Transform actorLayer;
        [SerializeField] private Transform handRoot;
        [SerializeField] private HandCardLayoutSolver handLayoutSolver = new();

        private readonly Dictionary<int, Transform> mActors = new();
        private readonly Dictionary<int, ViewActorZone> mActorZones = new();
        private readonly List<int> mHandCardUids = new();

        public Transform ActorLayer => actorLayer != null ? actorLayer : transform;
        public Transform HandRoot => handRoot != null ? handRoot : transform;
        public HandCardLayoutSolver HandLayoutSolver => handLayoutSolver;

        public IReadOnlyList<SlotId> BoardRingPathSlots => BoardRingPath;

        public void RegisterActor(int cardUid, Transform actor, ViewActorZone zone = ViewActorZone.Board)
        {
            if (cardUid <= 0 || actor == null)
            {
                return;
            }

            mActors[cardUid] = actor;
            mActorZones[cardUid] = zone;

            if (zone == ViewActorZone.Hand && !mHandCardUids.Contains(cardUid))
            {
                mHandCardUids.Add(cardUid);
            }
        }

        public void UnregisterActor(int cardUid)
        {
            mActors.Remove(cardUid);
            mActorZones.Remove(cardUid);
            mHandCardUids.Remove(cardUid);
        }

        public void SyncHandCards(IReadOnlyList<int> orderedUids)
        {
            mHandCardUids.Clear();
            if (orderedUids == null)
            {
                return;
            }

            for (var i = 0; i < orderedUids.Count; i++)
            {
                if (orderedUids[i] > 0)
                {
                    mHandCardUids.Add(orderedUids[i]);
                }
            }
        }

        public bool TryGetActor(int cardUid, out Transform actor)
        {
            return mActors.TryGetValue(cardUid, out actor);
        }

        public bool TryGetSlotAnchor(SlotId slot, out Transform anchor)
        {
            anchor = null;
            if (slot.IsAvatar)
            {
                if (avatarAnchor != null)
                {
                    anchor = avatarAnchor;
                    return true;
                }

                return TryGetSlotAnchor(SlotId.Board(5), out anchor);
            }

            if (!slot.IsBoardSlot)
            {
                return false;
            }

            int boardIndex = slot.Index;
            if (boardSlotAnchors != null && boardIndex >= 1 && boardIndex <= boardSlotAnchors.Length)
            {
                anchor = boardSlotAnchors[boardIndex - 1];
                if (anchor != null)
                {
                    return true;
                }
            }

            if (boardSlotRoot == null)
            {
                return false;
            }

            string slotName = "slot" + boardIndex;
            for (var i = 0; i < boardSlotRoot.childCount; i++)
            {
                Transform child = boardSlotRoot.GetChild(i);
                if (child.name == slotName || child.name.StartsWith(slotName + "_", StringComparison.Ordinal))
                {
                    anchor = child;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetAvatarActor(CoreViewSnapshot snapshot, out Transform actor)
        {
            actor = null;
            if (snapshot == null || snapshot.AvatarUid <= 0)
            {
                return false;
            }

            return TryGetActor(snapshot.AvatarUid, out actor);
        }

        public IReadOnlyList<int> HandCardUids => mHandCardUids;

        public void CollectHandActors(List<Transform> actors)
        {
            actors.Clear();
            for (var i = 0; i < mHandCardUids.Count; i++)
            {
                Transform actor;
                if (TryGetActor(mHandCardUids[i], out actor) && actor != null)
                {
                    actors.Add(actor);
                }
            }
        }

        public void AppendHandCard(int cardUid)
        {
            if (cardUid <= 0 || mHandCardUids.Contains(cardUid))
            {
                return;
            }

            mHandCardUids.Add(cardUid);
        }

        public void CollectRingBoardActors(CoreViewSnapshot snapshot, List<Transform> actors, List<Transform> ringAnchors)
        {
            actors.Clear();
            ringAnchors.Clear();

            for (var i = 0; i < BoardRingPath.Length; i++)
            {
                SlotId slot = BoardRingPath[i];
                Transform anchor;
                if (!TryGetSlotAnchor(slot, out anchor) || anchor == null)
                {
                    continue;
                }

                ringAnchors.Add(anchor);

                int uid = 0;
                if (snapshot != null)
                {
                    BoardSlotView slotView = snapshot.GetSlot(slot);
                    uid = slotView != null ? slotView.CardUid : 0;
                }

                Transform actor;
                if (uid > 0 && TryGetActor(uid, out actor) && actor != null)
                {
                    actors.Add(actor);
                }
            }
        }

        public void AlignBoardFromSnapshot(CoreViewSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            for (var i = 0; i < snapshot.BoardSlots.Count; i++)
            {
                BoardSlotView slotView = snapshot.BoardSlots[i];
                Transform actor;
                if (slotView.CardUid <= 0 || !TryGetActor(slotView.CardUid, out actor) || actor == null)
                {
                    continue;
                }

                Transform anchor;
                if (!TryGetSlotAnchor(slotView.Slot, out anchor) || anchor == null)
                {
                    continue;
                }

                actor.position = anchor.position;
                actor.localRotation = anchor.localRotation;
                mActorZones[slotView.CardUid] = ViewActorZone.Board;
            }
        }

        private void OnValidate()
        {
            handLayoutSolver?.ResolveFromReferenceAnchors();
        }
    }
}
