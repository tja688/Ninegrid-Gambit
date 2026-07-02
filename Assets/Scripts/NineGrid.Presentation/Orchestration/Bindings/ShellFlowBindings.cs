using System;
using System.Collections.Generic;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Flow.Shell;
using NineGrid.Presentation.Shell;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class InGameUiEntranceFlowBinding : IFlowBinding
    {
        private readonly InGameUiFlow mFlow;

        public InGameUiEntranceFlowBinding(InGameUiFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.InGameUiEntrance;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            mFlow.PlayEntrance();
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }

    public sealed class InGameUiExitFlowBinding : IFlowBinding
    {
        private readonly InGameUiFlow mFlow;

        public InGameUiExitFlowBinding(InGameUiFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.InGameUiExit;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            mFlow.PlayExit();
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }

    public sealed class RoomChoiceInFlowBinding : IFlowBinding
    {
        private readonly RoomChoiceFlow mFlow;
        private readonly RoomChoiceScreenPresenter mPresenter;

        public RoomChoiceInFlowBinding(RoomChoiceFlow flow, RoomChoiceScreenPresenter presenter)
        {
            mFlow = flow;
            mPresenter = presenter;
        }

        public FlowId Id => FlowId.RoomChoiceIn;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null || mPresenter == null || mFlow.EntrancePlayed)
            {
                return SnapshotHandle(onMarker);
            }

            if (!mPresenter.TryBuildEntranceMoves(out List<SelectionPresentation.RoomMove> moves))
            {
                return SnapshotHandle(onMarker);
            }

            mFlow.PlayEntrance(moves);
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }

        private static FlowHandle SnapshotHandle(Action<string> onMarker)
        {
            return FlowBindingFallback.SnapshotHandle(onMarker);
        }
    }

    public sealed class RoomChoiceOutFlowBinding : IFlowBinding
    {
        private readonly RoomChoiceFlow mFlow;
        private readonly RoomChoiceScreenPresenter mPresenter;

        public RoomChoiceOutFlowBinding(RoomChoiceFlow flow, RoomChoiceScreenPresenter presenter)
        {
            mFlow = flow;
            mPresenter = presenter;
        }

        public FlowId Id => FlowId.RoomChoiceOut;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null || mPresenter == null)
            {
                return SnapshotHandle(onMarker);
            }

            int selectedIndex = payload != null ? payload.Index : 0;
            if (!mPresenter.TryBuildExitActors(selectedIndex, out Transform selected, out Transform end, out Transform unselected))
            {
                return SnapshotHandle(onMarker);
            }

            mFlow.PlayExit(selected, end, unselected);
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }

        private static FlowHandle SnapshotHandle(Action<string> onMarker)
        {
            return FlowBindingFallback.SnapshotHandle(onMarker);
        }
    }
}
