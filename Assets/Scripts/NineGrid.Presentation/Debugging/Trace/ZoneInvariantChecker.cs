using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Trace
{
    public static class ZoneInvariantChecker
    {
        public static void CheckAndRecord(
            ZoneSnapshotData data,
            TableNineViewRegistry viewRegistry,
            BattleTraceSession session)
        {
            if (session == null || data == null || viewRegistry == null)
            {
                return;
            }

            CheckBoardGhosts(data, viewRegistry, session);
            CheckHandGhosts(data, viewRegistry, session);
            CheckHandOrder(data, session);
            CheckInactiveActors(data, viewRegistry, session);
            CheckRegistryDuplicates(viewRegistry, session);
        }

        private static void CheckBoardGhosts(
            ZoneSnapshotData data,
            TableNineViewRegistry viewRegistry,
            BattleTraceSession session)
        {
            foreach (var pair in data.Board)
            {
                int slotIndex = pair.Key;
                int uid = pair.Value;
                if (uid <= 0)
                {
                    continue;
                }

                Transform actor = viewRegistry.ResolveActor(uid);
                if (actor == null)
                {
                    session.RecordViolation(
                        "BOARD_GHOST",
                        "Snapshot slot " + slotIndex + " expects uid " + uid + " but registry has no actor",
                        "error",
                        slotIndex,
                        uid,
                        null);
                }
            }
        }

        private static void CheckHandGhosts(
            ZoneSnapshotData data,
            TableNineViewRegistry viewRegistry,
            BattleTraceSession session)
        {
            for (var i = 0; i < data.Hand.Count; i++)
            {
                int uid = data.Hand[i];
                Transform actor = viewRegistry.ResolveActor(uid);
                if (actor == null)
                {
                    session.RecordViolation(
                        "HAND_GHOST",
                        "ItemSlotUids contains uid " + uid + " but registry has no actor",
                        "error",
                        null,
                        uid,
                        null);
                }
            }
        }

        private static void CheckHandOrder(ZoneSnapshotData data, BattleTraceSession session)
        {
            if (data.HandActorOrder.Count == 0 || data.Hand.Count == 0)
            {
                return;
            }

            if (data.Hand.Count != data.HandActorOrder.Count)
            {
                session.RecordViolation(
                    "HAND_ORDER_MISMATCH",
                    "ItemSlot count=" + data.Hand.Count + " but coordinator hand actors=" + data.HandActorOrder.Count,
                    "warning");
                return;
            }

            for (var i = 0; i < data.Hand.Count; i++)
            {
                if (data.Hand[i] != data.HandActorOrder[i])
                {
                    session.RecordViolation(
                        "HAND_ORDER_MISMATCH",
                        "Expected hand order [" + string.Join(",", data.Hand)
                        + "] but coordinator order [" + string.Join(",", data.HandActorOrder) + "]",
                        "warning");
                    return;
                }
            }
        }

        private static void CheckInactiveActors(
            ZoneSnapshotData data,
            TableNineViewRegistry viewRegistry,
            BattleTraceSession session)
        {
            var liveUids = new HashSet<int>();
            foreach (var pair in data.Board)
            {
                if (pair.Value > 0)
                {
                    liveUids.Add(pair.Value);
                }
            }

            for (var i = 0; i < data.Hand.Count; i++)
            {
                liveUids.Add(data.Hand[i]);
            }

            foreach (int uid in liveUids)
            {
                Transform actor = viewRegistry.ResolveActor(uid);
                if (actor != null && !actor.gameObject.activeSelf)
                {
                    session.RecordViolation(
                        "ACTOR_INACTIVE",
                        "Snapshot expects uid " + uid + " to be visible but actor is inactive",
                        "error",
                        null,
                        uid,
                        null);
                }
            }
        }

        private static void CheckRegistryDuplicates(
            TableNineViewRegistry viewRegistry,
            BattleTraceSession session)
        {
            var uidToName = new Dictionary<int, string>();
            viewRegistry.ForEachActor((uid, actor) =>
            {
                if (uid <= 0 || actor == null)
                {
                    return;
                }

                if (uidToName.TryGetValue(uid, out string existing))
                {
                    session.RecordViolation(
                        "REGISTRY_DUPLICATE_UID",
                        "Uid " + uid + " bound to both " + existing + " and " + actor.name,
                        "error",
                        null,
                        uid,
                        null);
                }
                else
                {
                    uidToName[uid] = actor.name;
                }
            });
        }
    }
}
