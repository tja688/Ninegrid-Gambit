using System.Collections.Generic;
using System.Text;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Trace
{
    public sealed class ZoneSnapshotData
    {
        public Dictionary<int, int> Board { get; } = new();
        public List<int> Hand { get; } = new();
        public List<int> Registry { get; } = new();
        public List<int> HandActorOrder { get; } = new();
        public string Phase { get; set; } = string.Empty;
        public int NodeIndex { get; set; }
    }

    public static class ZoneSnapshotCapture
    {
        public static ZoneSnapshotData Capture(
            IArchitecture architecture,
            TableNineViewRegistry viewRegistry,
            InGameInteractionCoordinator coordinator = null)
        {
            var data = new ZoneSnapshotData();
            CoreViewSnapshot snapshot = architecture != null
                ? CoreViewSnapshotFactory.Capture(architecture)
                : null;

            if (snapshot != null)
            {
                data.Phase = snapshot.Run?.Phase.ToString() ?? string.Empty;
                data.NodeIndex = snapshot.Run?.NodeIndex ?? 0;

                if (snapshot.Board?.Slots != null)
                {
                    IReadOnlyList<BoardSlotView> slots = snapshot.Board.Slots;
                    for (var i = 0; i < slots.Count; i++)
                    {
                        BoardSlotView slotView = slots[i];
                        if (slotView.Slot.IsBoardSlot)
                        {
                            data.Board[slotView.Slot.Index] = slotView.CardUid;
                        }
                    }
                }

                if (snapshot.Deck?.ItemSlotUids != null)
                {
                    IReadOnlyList<int> itemUids = snapshot.Deck.ItemSlotUids;
                    for (var i = 0; i < itemUids.Count; i++)
                    {
                        int uid = itemUids[i];
                        if (uid > 0)
                        {
                            data.Hand.Add(uid);
                        }
                    }
                }
            }

            if (viewRegistry != null)
            {
                viewRegistry.CopyRegisteredUids(data.Registry);
            }

            if (coordinator?.HandActors != null)
            {
                IReadOnlyList<Transform> handActors = coordinator.HandActors;
                for (var i = 0; i < handActors.Count; i++)
                {
                    Transform actor = handActors[i];
                    int uid = ResolveUid(actor);
                    if (uid > 0)
                    {
                        data.HandActorOrder.Add(uid);
                    }
                }
            }

            return data;
        }

        public static void AppendJson(StringBuilder builder, ZoneSnapshotData data)
        {
            if (data == null)
            {
                BattleTraceJsonWriter.AppendString(builder, "phase", string.Empty);
                return;
            }

            BattleTraceJsonWriter.AppendString(builder, "phase", data.Phase);
            builder.Append(',');
            BattleTraceJsonWriter.AppendNumber(builder, "node", data.NodeIndex);
            builder.Append(',');
            BattleTraceJsonWriter.AppendBoardMap(builder, "board", data.Board);
            builder.Append(',');
            BattleTraceJsonWriter.AppendIntArray(builder, "hand", data.Hand);
            builder.Append(',');
            BattleTraceJsonWriter.AppendIntArray(builder, "registry", data.Registry);
            builder.Append(',');
            BattleTraceJsonWriter.AppendIntArray(builder, "handActorOrder", data.HandActorOrder);
        }

        public static int ResolveUid(Transform actor)
        {
            if (actor == null)
            {
                return 0;
            }

            TableNineActorBinding binding = actor.GetComponent<TableNineActorBinding>();
            return binding != null ? binding.CardUid : 0;
        }
    }
}
