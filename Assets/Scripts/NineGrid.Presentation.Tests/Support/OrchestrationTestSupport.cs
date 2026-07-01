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

    internal sealed class RecordingReactionBinding : IReactionBinding
    {
        public RecordingReactionBinding(ReactionId id)
        {
            Id = id;
        }

        public ReactionId Id { get; }
        public int PlayCount { get; private set; }
        public FlowPayload LastPayload { get; private set; }

        public void Play(IViewRegistry registry, FlowPayload payload)
        {
            PlayCount++;
            LastPayload = payload;
        }

        public void Stop() { }
    }

    internal sealed class RecordingReconcilable : IReconcilable
    {
        public int ApplyCount { get; private set; }
        public CoreViewSnapshot LastSnapshot { get; private set; }

        public void ApplySnapshot(CoreViewSnapshot snapshot)
        {
            ApplyCount++;
            LastSnapshot = snapshot;
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
    }
}
