using System.Collections;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Diagnostics;
using NineGrid.Presentation.Registry;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 中心批次播放器：订阅 <see cref="IPresentationSyncSystem"/> 活跃批次，
    /// 路由至域适配器，批末对齐快照并发送 <see cref="PresentationFinishedCommand"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PresentationBatchPlayer : MonoBehaviour, IController
    {
        [SerializeField] private TableNineViewRegistry viewRegistry;
        [SerializeField] private TableNineBoardAdaptor boardAdaptor;
        [SerializeField] private TableNineCardDeckAdaptor deckAdaptor;
        [SerializeField] private TableNineItemAdaptor itemAdaptor;
        [SerializeField] private TableNineEffectAdaptor effectAdaptor;
        [SerializeField] private TableNineStatusAdaptor statusAdaptor;
        [SerializeField] private TableNineOverlayAdaptor overlayAdaptor;

        private Coroutine mPlaybackCoroutine;
        private int mLastPlayedBatchId;
        private int mCurrentInstructionIndex = -1;
        private string mCurrentInstructionKind = string.Empty;
        private float mBatchPlayStartTime;

        public bool IsPlaybackActive => mPlaybackCoroutine != null;
        public int LastPlayedBatchId => mLastPlayedBatchId;
        public int CurrentInstructionIndex => mCurrentInstructionIndex;
        public string CurrentInstructionKind => mCurrentInstructionKind;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        public void PlayDispatchResult(CoreCommandDispatchResult result)
        {
            if (result == null || !result.BatchOpened || result.Batch == null)
            {
                return;
            }

            EnsureReferences();
            if (mPlaybackCoroutine != null)
            {
                StopCoroutine(mPlaybackCoroutine);
            }

            mPlaybackCoroutine = StartCoroutine(PlayBatchCoroutine(result.Batch));
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var sync = this.GetSystem<IPresentationSyncSystem>();
            if (!sync.IsInputLocked || sync.ActiveBatch == null)
            {
                return;
            }

            if (sync.ActiveBatchId == mLastPlayedBatchId || mPlaybackCoroutine != null)
            {
                return;
            }

            EnsureReferences();
            PresentationTrace.Log(
                PresentationTraceChannel.Batch,
                PresentationTraceLevel.Info,
                "BATCH_PLAY_FALLBACK",
                ("batch", sync.ActiveBatchId),
                ("lastPlayed", mLastPlayedBatchId));
            mPlaybackCoroutine = StartCoroutine(PlayBatchCoroutine(sync.ActiveBatch));
        }

        private IEnumerator PlayBatchCoroutine(PresentationBatch batch)
        {
            if (batch == null)
            {
                yield break;
            }

            mLastPlayedBatchId = batch.BatchId;
            mBatchPlayStartTime = Time.realtimeSinceStartup;
            mCurrentInstructionIndex = -1;
            mCurrentInstructionKind = string.Empty;

            var phase = this.GetModel<RunModel>().Phase.Value;
            PresentationTrace.SetContext(batch.BatchId, phase.ToString(), string.Empty);
            PresentationTrace.Log(
                PresentationTraceChannel.Batch,
                PresentationTraceLevel.Info,
                "BATCH_PLAY_BEGIN",
                ("batch", batch.BatchId),
                ("instr", batch.Instructions.Count),
                ("seqFrom", batch.FromSequence),
                ("seqTo", batch.ToSequence),
                ("requiresAck", batch.RequiresAcknowledgement),
                ("phase", phase));

            BoardBatchPlan boardPlan = BoardBatchPlan.Build(batch.Instructions);
            DeckBatchPlan deckPlan = DeckBatchPlan.Build(batch.Instructions);

            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                PresentationInstruction instruction = batch.Instructions[i];
                mCurrentInstructionIndex = i;
                mCurrentInstructionKind = instruction.Kind.ToString();

                float instrStart = Time.realtimeSinceStartup;
                string adaptorName = null;
                bool handled = false;

                if (statusAdaptor != null && statusAdaptor.CanHandle(instruction))
                {
                    adaptorName = "Status";
                    handled = true;
                    LogInstrBegin(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName);
                    yield return statusAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        batch.Snapshot);
                    LogInstrEnd(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName, instrStart);
                }

                if (overlayAdaptor != null && overlayAdaptor.CanHandle(instruction))
                {
                    adaptorName = "Overlay";
                    handled = true;
                    LogInstrBegin(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName);
                    yield return overlayAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        batch.Snapshot);
                    LogInstrEnd(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName, instrStart);
                }
                else if (deckAdaptor != null && deckAdaptor.CanHandle(instruction))
                {
                    adaptorName = "Deck";
                    handled = true;
                    LogInstrBegin(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName);
                    yield return deckAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        deckPlan,
                        i,
                        batch.Snapshot);
                    LogInstrEnd(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName, instrStart);
                }
                else if (itemAdaptor != null && itemAdaptor.CanHandle(instruction))
                {
                    adaptorName = "Item";
                    handled = true;
                    LogInstrBegin(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName);
                    yield return itemAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        batch.Snapshot);
                    LogInstrEnd(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName, instrStart);
                }
                else if (effectAdaptor != null && effectAdaptor.CanHandle(instruction))
                {
                    adaptorName = "Effect";
                    handled = true;
                    LogInstrBegin(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName);
                    yield return effectAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        batch.Snapshot);
                    LogInstrEnd(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName, instrStart);
                }
                else if (boardAdaptor != null && boardAdaptor.CanHandle(instruction))
                {
                    adaptorName = "Board";
                    handled = true;
                    LogInstrBegin(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName);
                    yield return boardAdaptor.PlayInstruction(
                        instruction,
                        batch.Instructions,
                        boardPlan,
                        batch.Snapshot);
                    LogInstrEnd(batch.BatchId, i, batch.Instructions.Count, instruction, adaptorName, instrStart);
                }

                if (!handled)
                {
                    PresentationTrace.Log(
                        PresentationTraceChannel.Batch,
                        PresentationTraceLevel.Warn,
                        "BATCH_INSTR_UNHANDLED",
                        ("batch", batch.BatchId),
                        ("idx", i + 1),
                        ("kind", instruction.Kind),
                        ("seq", instruction.Sequence));
                }
            }

            if (viewRegistry != null && batch.Snapshot != null)
            {
                if (deckAdaptor != null)
                {
                    deckAdaptor.AlignDeckFromSnapshot(batch.Snapshot);
                }

                if (statusAdaptor != null)
                {
                    statusAdaptor.AlignFromSnapshot(batch.Snapshot);
                }

                viewRegistry.AlignBoardFromSnapshot(batch.Snapshot);
                PresentationTrace.Log(
                    PresentationTraceChannel.Batch,
                    PresentationTraceLevel.Trace,
                    "BATCH_SNAP_ALIGN",
                    ("batch", batch.BatchId));
            }

            if (batch.RequiresAcknowledgement)
            {
                this.SendCommand(new PresentationFinishedCommand(batch.BatchId));
                PresentationTrace.Log(
                    PresentationTraceChannel.Batch,
                    PresentationTraceLevel.Info,
                    "BATCH_ACK_SENT",
                    ("batch", batch.BatchId));
            }
            else
            {
                PresentationTrace.Log(
                    PresentationTraceChannel.Batch,
                    PresentationTraceLevel.Info,
                    "BATCH_ACK_SKIPPED",
                    ("batch", batch.BatchId));
            }

            var totalMs = (Time.realtimeSinceStartup - mBatchPlayStartTime) * 1000f;
            PresentationTrace.Log(
                PresentationTraceChannel.Batch,
                PresentationTraceLevel.Info,
                "BATCH_PLAY_END",
                ("batch", batch.BatchId),
                ("totalMs", totalMs.ToString("F0")));

            mCurrentInstructionIndex = -1;
            mCurrentInstructionKind = string.Empty;
            mPlaybackCoroutine = null;
        }

        private static void LogInstrBegin(
            int batchId,
            int index,
            int total,
            PresentationInstruction instruction,
            string adaptorName)
        {
            PresentationTrace.Log(
                PresentationTraceChannel.Batch,
                PresentationTraceLevel.Trace,
                "INSTR_BEGIN",
                ("batch", batchId),
                ("idx", index + 1),
                ("total", total),
                ("kind", instruction.Kind),
                ("adaptor", adaptorName),
                ("seq", instruction.Sequence));
        }

        private static void LogInstrEnd(
            int batchId,
            int index,
            int total,
            PresentationInstruction instruction,
            string adaptorName,
            float instrStart)
        {
            var durationMs = (Time.realtimeSinceStartup - instrStart) * 1000f;
            PresentationTrace.Log(
                PresentationTraceChannel.Batch,
                PresentationTraceLevel.Trace,
                "INSTR_END",
                ("batch", batchId),
                ("idx", index + 1),
                ("total", total),
                ("kind", instruction.Kind),
                ("adaptor", adaptorName),
                ("durationMs", durationMs.ToString("F0")));
        }

        private void EnsureReferences()
        {
            if (viewRegistry == null)
            {
                viewRegistry = GetComponent<TableNineViewRegistry>();
            }

            if (boardAdaptor == null)
            {
                boardAdaptor = GetComponent<TableNineBoardAdaptor>();
            }

            if (deckAdaptor == null)
            {
                deckAdaptor = GetComponent<TableNineCardDeckAdaptor>();
            }

            if (itemAdaptor == null)
            {
                itemAdaptor = GetComponent<TableNineItemAdaptor>();
            }

            if (effectAdaptor == null)
            {
                effectAdaptor = GetComponent<TableNineEffectAdaptor>();
            }

            if (statusAdaptor == null)
            {
                statusAdaptor = GetComponent<TableNineStatusAdaptor>();
            }

            if (overlayAdaptor == null)
            {
                overlayAdaptor = GetComponent<TableNineOverlayAdaptor>();
            }

            if (boardAdaptor != null && viewRegistry != null && boardAdaptor.ViewRegistry == null)
            {
                // Serialized wiring is preferred; runtime fallback only for empty scenes.
            }
        }

        private void OnDisable()
        {
            if (mPlaybackCoroutine != null)
            {
                var sync = this.GetSystem<IPresentationSyncSystem>();
                PresentationTrace.Log(
                    PresentationTraceChannel.Batch,
                    PresentationTraceLevel.Error,
                    "BATCH_PLAY_ABORT",
                    ("reason", "OnDisable"),
                    ("batch", sync != null ? sync.ActiveBatchId : 0),
                    ("lastPlayed", mLastPlayedBatchId),
                    ("instrIdx", mCurrentInstructionIndex),
                    ("instrKind", mCurrentInstructionKind));
                StopCoroutine(mPlaybackCoroutine);
                mPlaybackCoroutine = null;
            }
        }
    }
}
