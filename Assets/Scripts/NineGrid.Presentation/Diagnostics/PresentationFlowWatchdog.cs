using System.Text;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Adaptors;
using NineGrid.Presentation.FSM;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Diagnostics
{
    /// <summary>
    /// 表现层流程不变量看门狗：自动检测输入锁死锁、批次孤儿、覆盖层/节点推进卡住。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public sealed class PresentationFlowWatchdog : MonoBehaviour, IController
    {
        [Header("References (optional, auto-resolve)")]
        [SerializeField] private PresentationSyncTraceBridge syncBridge;
        [SerializeField] private PresentationBatchPlayer batchPlayer;
        [SerializeField] private InGameFlowShellFsm flowShell;
        [SerializeField] private TableNineNodeFlowCoordinator nodeFlowCoordinator;
        [SerializeField] private BoardInteractionFsm boardInteractionFsm;
        [SerializeField] private ItemCardInteractionFsm itemCardInteractionFsm;
        [SerializeField] private SelectionOverlayFsm selectionOverlayFsm;
        [SerializeField] private SelectionOverlayController overlayController;

        private float mOrphanBatchSince = -1f;
        private float mAwaitingDismissSince = -1f;
        private float mOverlayBusySince = -1f;
        private float mAutoStartSince = -1f;
        private bool mW01Reported;
        private bool mW03Reported;
        private bool mW04Reported;
        private bool mW06Reported;
        private float mNextW05LogTime;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            var config = PresentationTrace.Config;
            var sync = this.GetSystem<IPresentationSyncSystem>();
            var phase = this.GetModel<RunModel>().Phase.Value;
            var screen = flowShell != null ? flowShell.Screen.ToString() : string.Empty;
            PresentationTrace.SetContext(sync.ActiveBatchId, phase.ToString(), screen);

            CheckW01_InputLockStall(sync, config);
            CheckW02_OrphanBatch(sync, config);
            CheckW03_AwaitingDismiss(config);
            CheckW04_AutoStartStall(config);
            CheckW05_PhaseFsmMismatch(phase);
            CheckW06_OverlayBusy(config);
        }

        private void CheckW01_InputLockStall(IPresentationSyncSystem sync, PresentationTraceConfig config)
        {
            if (!sync.IsInputLocked)
            {
                mW01Reported = false;
                return;
            }

            var held = syncBridge != null ? syncBridge.GetLockHeldSeconds() : 0f;
            if (held < config.InputLockStallSeconds)
            {
                return;
            }

            if (mW01Reported)
            {
                return;
            }

            mW01Reported = true;
            var snapshot = BuildSnapshotJson(sync);
            var dumpPath = PresentationTrace.WriteStallDump(
                "W01",
                "Input lock held too long",
                snapshot);
            PresentationTrace.Log(
                PresentationTraceChannel.Watchdog,
                PresentationTraceLevel.Stall,
                "STALL",
                ("rule", "W01"),
                ("lockHeld", held.ToString("F1")),
                ("batch", sync.ActiveBatchId),
                ("dump", dumpPath));
        }

        private void CheckW02_OrphanBatch(IPresentationSyncSystem sync, PresentationTraceConfig config)
        {
            var isOrphan = sync.ActiveBatch != null
                && batchPlayer != null
                && !batchPlayer.IsPlaybackActive
                && batchPlayer.LastPlayedBatchId != sync.ActiveBatchId;

            if (!isOrphan)
            {
                mOrphanBatchSince = -1f;
                return;
            }

            if (mOrphanBatchSince < 0f)
            {
                mOrphanBatchSince = Time.realtimeSinceStartup;
                return;
            }

            var elapsed = Time.realtimeSinceStartup - mOrphanBatchSince;
            if (elapsed < config.OrphanBatchFrameSeconds)
            {
                return;
            }

            PresentationTrace.Log(
                PresentationTraceChannel.Watchdog,
                PresentationTraceLevel.Error,
                "ORPHAN_BATCH",
                ("rule", "W02"),
                ("batch", sync.ActiveBatchId),
                ("lastPlayed", batchPlayer.LastPlayedBatchId),
                ("elapsed", elapsed.ToString("F2")));
            mOrphanBatchSince = -1f;
        }

        private void CheckW03_AwaitingDismiss(PresentationTraceConfig config)
        {
            var awaiting = selectionOverlayFsm != null && selectionOverlayFsm.AwaitingKernelDismiss;
            if (!awaiting)
            {
                mAwaitingDismissSince = -1f;
                mW03Reported = false;
                return;
            }

            if (mAwaitingDismissSince < 0f)
            {
                mAwaitingDismissSince = Time.realtimeSinceStartup;
                return;
            }

            var held = Time.realtimeSinceStartup - mAwaitingDismissSince;
            if (held < config.AwaitingDismissStallSeconds || mW03Reported)
            {
                return;
            }

            mW03Reported = true;
            var sync = this.GetSystem<IPresentationSyncSystem>();
            var dumpPath = PresentationTrace.WriteStallDump("W03", "awaitingKernelDismiss stuck", BuildSnapshotJson(sync));
            PresentationTrace.Log(
                PresentationTraceChannel.Watchdog,
                PresentationTraceLevel.Stall,
                "STALL",
                ("rule", "W03"),
                ("held", held.ToString("F1")),
                ("dump", dumpPath));
        }

        private void CheckW04_AutoStartStall(PresentationTraceConfig config)
        {
            var active = nodeFlowCoordinator != null && nodeFlowCoordinator.IsAutoStartActive;
            if (!active)
            {
                mAutoStartSince = -1f;
                mW04Reported = false;
                return;
            }

            if (mAutoStartSince < 0f)
            {
                mAutoStartSince = Time.realtimeSinceStartup;
                return;
            }

            var held = Time.realtimeSinceStartup - mAutoStartSince;
            if (held < config.AutoStartStallSeconds || mW04Reported)
            {
                return;
            }

            mW04Reported = true;
            var sync = this.GetSystem<IPresentationSyncSystem>();
            var dumpPath = PresentationTrace.WriteStallDump("W04", "Node auto-start stuck", BuildSnapshotJson(sync));
            PresentationTrace.Log(
                PresentationTraceChannel.Watchdog,
                PresentationTraceLevel.Stall,
                "STALL",
                ("rule", "W04"),
                ("held", held.ToString("F1")),
                ("dump", dumpPath));
        }

        private void CheckW05_PhaseFsmMismatch(GamePhase phase)
        {
            var phaseSystem = this.GetSystem<IPhaseSystem>();
            if (flowShell == null)
            {
                return;
            }

            if (phase == GamePhase.InteractionLoop
                && flowShell.Screen == FlowShellScreen.NodePlaying
                && phaseSystem.CanExecute(GameCommandKind.Attack)
                && boardInteractionFsm != null
                && !this.GetSystem<IPresentationSyncSystem>().IsInputLocked
                && !boardInteractionFsm.CanAcceptBoardInput())
            {
                if (Time.realtimeSinceStartup >= mNextW05LogTime)
                {
                    mNextW05LogTime = Time.realtimeSinceStartup + 2f;
                    PresentationTrace.Log(
                        PresentationTraceChannel.Watchdog,
                        PresentationTraceLevel.Warn,
                        "PHASE_FSM_MISMATCH",
                        ("rule", "W05"),
                        ("phase", phase),
                        ("screen", flowShell.Screen),
                        ("boardFsm", boardInteractionFsm.State),
                        ("canSend", flowShell.CanSendBoardCommand));
                }
            }
        }

        private void CheckW06_OverlayBusy(PresentationTraceConfig config)
        {
            var busy = overlayController != null && overlayController.IsBusy;
            if (!busy)
            {
                mOverlayBusySince = -1f;
                mW06Reported = false;
                return;
            }

            if (mOverlayBusySince < 0f)
            {
                mOverlayBusySince = Time.realtimeSinceStartup;
                return;
            }

            var held = Time.realtimeSinceStartup - mOverlayBusySince;
            if (held < config.OverlayBusyWarnSeconds || mW06Reported)
            {
                return;
            }

            mW06Reported = true;
            PresentationTrace.Log(
                PresentationTraceChannel.Watchdog,
                PresentationTraceLevel.Warn,
                "OVERLAY_BUSY",
                ("rule", "W06"),
                ("held", held.ToString("F1")),
                ("session", overlayController.SessionKind));
        }

        private string BuildSnapshotJson(IPresentationSyncSystem sync)
        {
            var builder = new StringBuilder(512);
            builder.Append('{');
            AppendJsonPair(builder, "phase", this.GetModel<RunModel>().Phase.Value.ToString(), isFirst: true);
            if (flowShell != null)
            {
                AppendJsonPair(builder, "screen", flowShell.Screen.ToString());
                AppendJsonPair(builder, "appState", flowShell.AppState.ToString());
                AppendJsonPair(builder, "nodeSessionActive", flowShell.NodeSessionActive);
                AppendJsonPair(builder, "canSendBoardCommand", flowShell.CanSendBoardCommand);
            }

            AppendJsonPair(builder, "batchId", sync.ActiveBatchId);
            AppendJsonPair(builder, "inputLocked", sync.IsInputLocked);
            if (syncBridge != null)
            {
                AppendJsonPair(builder, "lockHeldSeconds", syncBridge.GetLockHeldSeconds().ToString("F2"));
            }

            if (batchPlayer != null)
            {
                AppendJsonPair(builder, "playbackActive", batchPlayer.IsPlaybackActive);
                AppendJsonPair(builder, "lastPlayedBatchId", batchPlayer.LastPlayedBatchId);
                AppendJsonPair(builder, "currentInstrIndex", batchPlayer.CurrentInstructionIndex);
                AppendJsonPair(builder, "currentInstrKind", batchPlayer.CurrentInstructionKind);
            }

            if (boardInteractionFsm != null)
            {
                AppendJsonPair(builder, "boardFsmState", boardInteractionFsm.State.ToString());
            }

            if (itemCardInteractionFsm != null)
            {
                AppendJsonPair(builder, "itemFsmState", itemCardInteractionFsm.State.ToString());
            }

            if (selectionOverlayFsm != null)
            {
                AppendJsonPair(builder, "awaitingKernelDismiss", selectionOverlayFsm.AwaitingKernelDismiss);
                AppendJsonPair(builder, "overlayPointerEnabled", selectionOverlayFsm.IsPointerInputEnabled);
            }

            if (nodeFlowCoordinator != null)
            {
                AppendJsonPair(builder, "autoStartActive", nodeFlowCoordinator.IsAutoStartActive);
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendJsonPair(StringBuilder builder, string key, object value, bool isFirst = false)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(key).Append("\":");
            if (value is bool boolValue)
            {
                builder.Append(boolValue ? "true" : "false");
            }
            else if (value is int intValue)
            {
                builder.Append(intValue);
            }
            else
            {
                builder.Append('"').Append(value != null ? value.ToString().Replace("\"", "\\\"") : string.Empty).Append('"');
            }
        }

        private void ResolveReferences()
        {
            if (syncBridge == null)
            {
                syncBridge = FindFirstObjectByType<PresentationSyncTraceBridge>();
            }

            if (batchPlayer == null)
            {
                batchPlayer = FindFirstObjectByType<PresentationBatchPlayer>();
            }

            if (flowShell == null)
            {
                flowShell = FindFirstObjectByType<InGameFlowShellFsm>();
            }

            if (nodeFlowCoordinator == null)
            {
                nodeFlowCoordinator = FindFirstObjectByType<TableNineNodeFlowCoordinator>();
            }

            if (boardInteractionFsm == null)
            {
                boardInteractionFsm = FindFirstObjectByType<BoardInteractionFsm>();
            }

            if (itemCardInteractionFsm == null)
            {
                itemCardInteractionFsm = FindFirstObjectByType<ItemCardInteractionFsm>();
            }

            if (selectionOverlayFsm == null)
            {
                selectionOverlayFsm = FindFirstObjectByType<SelectionOverlayFsm>();
            }

            if (overlayController == null)
            {
                overlayController = FindFirstObjectByType<SelectionOverlayController>();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !PresentationTrace.Config.ShowRuntimeHud || flowShell == null)
            {
                return;
            }

            var sync = this.GetSystem<IPresentationSyncSystem>();
            var boardState = boardInteractionFsm != null ? boardInteractionFsm.State.ToString() : "-";
            var hud = string.Format(
                "Phase={0} | Screen={1} | Lock={2} | B#{3} | BoardFsm={4}",
                this.GetModel<RunModel>().Phase.Value,
                flowShell.Screen,
                sync.IsInputLocked,
                sync.ActiveBatchId,
                boardState);
            GUI.Label(new Rect(8f, 8f, 900f, 24f), hud);
        }
    }
}
