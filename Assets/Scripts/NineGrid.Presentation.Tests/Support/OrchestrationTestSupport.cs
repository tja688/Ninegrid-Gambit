using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Orchestration;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Support
{
    internal sealed class NullViewRegistry : IViewRegistry
    {
        public Transform ResolveActor(int cardUid) => null;
        public Transform ResolveAnchor(SlotId slot) => null;
        public void RegisterActor(int cardUid, Transform actor) { }
        public void RegisterAnchor(SlotId slot, Transform anchor) { }
    }

    internal sealed class RecordingFlowBinding : IFlowBinding
    {
        public RecordingFlowBinding(FlowId id, bool invokeImpactMarker = false)
        {
            Id = id;
            InvokeImpactMarker = invokeImpactMarker;
        }

        public FlowId Id { get; }
        public bool InvokeImpactMarker { get; }
        public int PlayCount { get; private set; }
        public FlowPayload LastPayload { get; private set; }

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            PlayCount++;
            LastPayload = payload;
            if (InvokeImpactMarker)
            {
                onMarker?.Invoke(FlowMarkers.Impact);
            }

            return new FlowHandle(new InstantDirectedFlow(), onMarker);
        }

        public void Stop() { }

        private sealed class InstantDirectedFlow : IDirectedFlow
        {
            public bool IsPlaying => false;
            public float ExpectedDuration => 0f;
            public void StopAndRestore() { }
        }
    }

    internal static class OrchestrationTestSnapshots
    {
        public static CoreViewSnapshot Minimal(int avatarUid = 1)
        {
            return new CoreViewSnapshot(
                1,
                new RunView(GamePhase.InteractionLoop, 0, 1, RoomKind.None, 0UL),
                new PlayerView(0, 3, new string[0], new string[0], CardStatView.Empty),
                new BoardView(new BoardSlotView[0], avatarUid, SlotId.Avatar),
                new DeckView(new int[0], new int[0], new int[0], new int[0]),
                new ChoiceView(PendingChoiceKind.None, string.Empty, new RewardEntry[0], new RoomKind[0], RoomKind.None),
                new Dictionary<int, CardView>());
        }

        public static CoreViewSnapshot WithBoardCombatants(int avatarUid = 1, int monsterUid = 2, SlotId monsterSlot = default)
        {
            if (monsterSlot.IsNone)
            {
                monsterSlot = SlotId.Board(2);
            }

            var cards = new Dictionary<int, CardView>
            {
                {
                    avatarUid,
                    new CardView(
                        avatarUid,
                        "avatar.default",
                        CardKind.Avatar,
                        ZoneId.Avatar,
                        SlotId.Avatar,
                        CardStatView.Empty,
                        new string[0])
                },
                {
                    monsterUid,
                    new CardView(
                        monsterUid,
                        "monster.pickpocket",
                        CardKind.Monster,
                        ZoneId.Board,
                        monsterSlot,
                        CardStatView.Empty,
                        new string[0])
                },
            };

            return new CoreViewSnapshot(
                1,
                new RunView(GamePhase.InteractionLoop, 0, 1, RoomKind.None, 0UL),
                new PlayerView(0, 3, new string[0], new string[0], CardStatView.Empty),
                new BoardView(
                    new[]
                    {
                        new BoardSlotView(
                            monsterSlot,
                            monsterUid,
                            "monster.pickpocket",
                            CardKind.Monster,
                            1,
                            1,
                            1,
                            1,
                            0,
                            0,
                            0,
                            0,
                            false),
                    },
                    avatarUid,
                    SlotId.Avatar),
                new DeckView(new int[0], new int[0], new int[0], new int[0]),
                new ChoiceView(PendingChoiceKind.None, string.Empty, new RewardEntry[0], new RoomKind[0], RoomKind.None),
                cards);
        }
    }
}
