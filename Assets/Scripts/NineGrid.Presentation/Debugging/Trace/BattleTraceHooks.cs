using System;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Orchestration.Bindings;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Trace
{
    public static class BattleTraceHooks
    {
        private static IArchitecture sArchitecture;
        private static TableNineViewRegistry sViewRegistry;
        private static InGameInteractionCoordinator sCoordinator;

        public static void Configure(
            IArchitecture architecture,
            TableNineViewRegistry viewRegistry,
            InGameInteractionCoordinator coordinator)
        {
            sArchitecture = architecture;
            sViewRegistry = viewRegistry;
            sCoordinator = coordinator;
        }

        public static void WrapFlowRegistry(FlowRegistry registry)
        {
            if (registry == null || !BattleTraceSession.IsActive)
            {
                return;
            }

            registry.WrapAll(BattleTraceFlowBinding.Wrap);
        }

        public static void RecordCommand(string commandName, bool batchOpened, bool inputLockedBefore, bool inputLockedAfter)
        {
            if (!BattleTraceSession.IsActive)
            {
                return;
            }

            BattleTraceSession.Current?.RecordCommand(commandName, batchOpened, inputLockedBefore, inputLockedAfter);
        }

        public static void RecordPreBatchSnapshot(int batchId)
        {
            CaptureSnapshot("pre_batch_" + batchId);
        }

        public static void RecordPostBatchSnapshot(int batchId)
        {
            CaptureSnapshot("post_batch_" + batchId);
            RunInvariantCheck();
        }

        public static void OnBatchStart(PresentationBatch batch, PresentationPlan plan)
        {
            if (!BattleTraceSession.IsActive || batch == null)
            {
                return;
            }

            BattleTraceSession session = BattleTraceSession.Current;
            session?.RecordBatchStart(
                batch.BatchId,
                batch.FromSequence,
                batch.ToSequence,
                batch.Instructions?.Count ?? 0);
            RecordPreBatchSnapshot(batch.BatchId);
            BattleTracePlanAnalyzer.RecordPlan(plan, session);
        }

        public static void OnBatchEnd(PresentationBatch batch)
        {
            if (!BattleTraceSession.IsActive || batch == null)
            {
                return;
            }

            BattleTraceSession.Current?.RecordBatchEnd(batch.BatchId);
        }

        public static void OnBatchPlaybackSettled(int batchId)
        {
            if (!BattleTraceSession.IsActive)
            {
                return;
            }

            RecordPostBatchSnapshot(batchId);
        }

        public static void RecordFlowResolve(
            FlowId flowId,
            FlowHandle handle,
            FlowPayload payload,
            int actionId,
            string explicitReason = null)
        {
            if (!BattleTraceSession.IsActive)
            {
                return;
            }

            string status;
            string reason = explicitReason ?? string.Empty;
            if (FlowBindingFallback.IsSilentFallback(handle))
            {
                status = "fallback_noop";
                if (string.IsNullOrEmpty(reason))
                {
                    reason = "InstantAlign fallback";
                }
            }
            else if (handle?.DirectedFlow == null)
            {
                status = "missing_flow";
                reason = string.IsNullOrEmpty(reason) ? "DirectedFlow is null" : reason;
            }
            else if (handle.IsPlaying)
            {
                status = "playing";
            }
            else
            {
                status = "instant_complete";
            }

            BattleTraceSession.Current?.RecordFlowResolve(flowId, status, reason, payload, actionId);
        }

        public static void RecordFlowResolve(
            FlowId flowId,
            string status,
            string reason,
            FlowPayload payload,
            int actionId = 0)
        {
            if (!BattleTraceSession.IsActive)
            {
                return;
            }

            BattleTraceSession.Current?.RecordFlowResolve(flowId, status, reason, payload, actionId);
        }

        public static void RecordFlowLifecycle(FlowId flowId, string phase)
        {
            if (!BattleTraceSession.IsActive)
            {
                return;
            }

            BattleTraceSession.Current?.RecordFlowLifecycle(flowId, phase, Time.realtimeSinceStartup);
        }

        public static void RecordActorSpawn(int uid, string defId, string detail = null)
        {
            if (!BattleTraceSession.IsActive)
            {
                return;
            }

            BattleTraceSession.Current?.RecordActorLifecycle("ActorSpawn", uid, defId, detail);
        }

        public static void RecordActorDespawn(int uid, string detail = null)
        {
            if (!BattleTraceSession.IsActive)
            {
                return;
            }

            BattleTraceSession.Current?.RecordActorLifecycle("ActorDespawn", uid, string.Empty, detail);
        }

        public static void RecordLayout(
            string op,
            string trigger,
            System.Collections.Generic.IReadOnlyList<int> uids,
            float duration,
            bool draggingExcluded)
        {
            if (!BattleTraceSession.IsActive)
            {
                return;
            }

            BattleTraceSession.Current?.RecordLayout(op, trigger, uids, duration, draggingExcluded);
        }

        public static void RecordInteraction(
            string eventName,
            int uid,
            string fsmState,
            bool inputLocked,
            Transform actor)
        {
            if (!BattleTraceSession.IsActive || !BattleTraceSession.ActiveIncludeInteraction)
            {
                return;
            }

            bool actorActive = actor != null && actor.gameObject.activeSelf;
            BattleTraceSession.Current?.RecordInteraction(eventName, uid, fsmState, inputLocked, actorActive);
        }

        private static void CaptureSnapshot(string tag)
        {
            if (!BattleTraceSession.IsActive || sArchitecture == null || sViewRegistry == null)
            {
                return;
            }

            ZoneSnapshotData data = ZoneSnapshotCapture.Capture(sArchitecture, sViewRegistry, sCoordinator);
            BattleTraceSession.Current?.RecordSnapshot(tag, data);
        }

        private static void RunInvariantCheck()
        {
            if (!BattleTraceSession.IsActive || sArchitecture == null || sViewRegistry == null)
            {
                return;
            }

            ZoneSnapshotData data = ZoneSnapshotCapture.Capture(sArchitecture, sViewRegistry, sCoordinator);
            ZoneInvariantChecker.CheckAndRecord(data, sViewRegistry, BattleTraceSession.Current);
        }
    }
}
