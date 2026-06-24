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
        Deck,
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

        private void Awake()
        {
            EnsureHandLayoutBindings();
        }

        /// <summary>
        /// 将手牌布局解析到场景内 HandCardAnchors（Inspector 未绑定时运行时兜底）。
        /// </summary>
        public void EnsureHandLayoutBindings()
        {
            if (handLayoutSolver == null)
            {
                return;
            }

            Transform resolvedHandRoot;
            Transform[] referenceAnchors;
            if (!HandCardLayoutBindingUtility.TryResolve(transform, out resolvedHandRoot, out referenceAnchors))
            {
                handLayoutSolver.ResolveFromReferenceAnchors();
                return;
            }

            if (handRoot == null)
            {
                handRoot = resolvedHandRoot;
            }

            if (referenceAnchors.Length > 0)
            {
                handLayoutSolver.SetReferenceAnchors(referenceAnchors);
            }
            else
            {
                handLayoutSolver.ResolveFromReferenceAnchors();
            }
        }

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

        public bool TryGetActorZone(int cardUid, out ViewActorZone zone)
        {
            return mActorZones.TryGetValue(cardUid, out zone);
        }

        /// <summary>
        /// 从射线命中 Transform 向上解析场地演员或槽位锚点。
        /// </summary>
        public bool TryResolveBoardPointerHit(
            Transform hitTransform,
            out SlotId slot,
            out int cardUid,
            out Transform actor)
        {
            slot = SlotId.None;
            cardUid = 0;
            actor = null;

            if (hitTransform == null)
            {
                return false;
            }

            Transform current = hitTransform;
            while (current != null)
            {
                foreach (KeyValuePair<int, Transform> entry in mActors)
                {
                    Transform registered = entry.Value;
                    if (registered == null)
                    {
                        continue;
                    }

                    if (current == registered || current.IsChildOf(registered))
                    {
                        cardUid = entry.Key;
                        actor = registered;
                        return true;
                    }
                }

                if (TryGetSlotForAnchorTransform(current, out slot))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
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

        private bool TryGetSlotForAnchorTransform(Transform anchorTransform, out SlotId slot)
        {
            slot = SlotId.None;
            if (anchorTransform == null)
            {
                return false;
            }

            if (boardSlotAnchors != null)
            {
                for (var i = 0; i < boardSlotAnchors.Length; i++)
                {
                    Transform anchor = boardSlotAnchors[i];
                    if (anchor == null)
                    {
                        continue;
                    }

                    if (anchorTransform == anchor || anchorTransform.IsChildOf(anchor))
                    {
                        slot = SlotId.Board(i + 1);
                        return true;
                    }
                }
            }

            if (boardSlotRoot == null)
            {
                return false;
            }

            for (var i = 0; i < boardSlotRoot.childCount; i++)
            {
                Transform child = boardSlotRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (anchorTransform != child && !anchorTransform.IsChildOf(child))
                {
                    continue;
                }

                if (TryParseSlotName(child.name, out int boardIndex))
                {
                    slot = SlotId.Board(boardIndex);
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseSlotName(string name, out int boardIndex)
        {
            boardIndex = 0;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("slot", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string suffix = name.Substring(4);
            int underscore = suffix.IndexOf('_');
            if (underscore >= 0)
            {
                suffix = suffix.Substring(0, underscore);
            }

            return int.TryParse(suffix, out boardIndex)
                && boardIndex >= SlotId.MinBoardIndex
                && boardIndex <= SlotId.MaxBoardIndex;
        }

        private void OnValidate()
        {
            handLayoutSolver?.ResolveFromReferenceAnchors();
        }
    }
}
